using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Aquarium.Engine.Render;

internal sealed unsafe class D3D12FieldTexture2D : IDisposable
{
    public ID3D12Resource Resource { get; }

    public int Width { get; }

    public int Height { get; }

    public Format Format { get; }

    public bool AllowsUnorderedAccess { get; }

    public ResourceStates State { get; private set; } = ResourceStates.CopyDest;

    private readonly ID3D12Resource? uploadResource;

    private D3D12FieldTexture2D(
        ID3D12Resource resource,
        ID3D12Resource? uploadResource,
        int width,
        int height,
        Format format,
        bool allowsUnorderedAccess,
        string name)
    {
        Resource = resource;
        this.uploadResource = uploadResource;
        Width = width;
        Height = height;
        Format = format;
        AllowsUnorderedAccess = allowsUnorderedAccess;
        Resource.Name = name;
    }

    public static D3D12FieldTexture2D CreateFallbackRamp(
        ID3D12Device device,
        ID3D12GraphicsCommandList commandList,
        string name)
    {
        const int width = 256;
        var rgba = new byte[width * 4];
        for (var x = 0; x < width; x++)
        {
            var value = (byte)x;
            var offset = x * 4;
            rgba[offset + 0] = value;
            rgba[offset + 1] = value;
            rgba[offset + 2] = value;
            rgba[offset + 3] = 255;
        }

        return CreateRgba8(device, commandList, rgba, width, 1, name);
    }

    public static D3D12FieldTexture2D LoadLocalAsset(
        ID3D12Device device,
        ID3D12GraphicsCommandList commandList,
        AquariumFieldResourceDeclaration declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration.SourceUri))
        {
            throw new InvalidOperationException($"Texture2D field resource `{declaration.ResourceKey}` is missing SourceUri.");
        }

        var path = declaration.SourceUri;
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Texture2D field resource `{declaration.ResourceKey}` local asset does not exist.", path);
        }

        var rgba = LoadRgba8(path, out var width, out var height);
        return CreateRgba8(
            device,
            commandList,
            rgba,
            width,
            height,
            $"Aquarium D3D12 Field Texture2D {declaration.ResourceKey}");
    }

    public static bool TryCreateEmpty(
        ID3D12Device device,
        AquariumFieldResourceDeclaration declaration,
        out D3D12FieldTexture2D texture)
    {
        texture = null!;
        if (!D3D12FieldTextureFormat.TryFormat(declaration.Format, out var format))
        {
            return false;
        }

        var width = Math.Max(1, declaration.Width);
        var height = Math.Max(1, declaration.Height);
        var allowsUnorderedAccess = declaration.Access == AquariumFieldShaderAccess.UnorderedAccess;
        var resource = device.CreateCommittedResource(
            HeapType.Default,
            ResourceDescription.Texture2D(
                format,
                (uint)width,
                (uint)height,
                1,
                1,
                1,
                0,
                allowsUnorderedAccess ? ResourceFlags.AllowUnorderedAccess : ResourceFlags.None),
            allowsUnorderedAccess ? ResourceStates.UnorderedAccess : ResourceStates.PixelShaderResource,
            null);

        texture = new D3D12FieldTexture2D(
            resource,
            uploadResource: null,
            width,
            height,
            format,
            allowsUnorderedAccess,
            $"Aquarium D3D12 Field Texture2D {declaration.ResourceKey}");
        texture.State = allowsUnorderedAccess ? ResourceStates.UnorderedAccess : ResourceStates.PixelShaderResource;
        return true;
    }

    public static bool TryOpenShared(
        ID3D12Device device,
        AquariumFieldResourceDeclaration declaration,
        out D3D12FieldTexture2D texture)
    {
        texture = null!;
        if (declaration.NativeHandle == IntPtr.Zero ||
            declaration.Width <= 0 ||
            declaration.Height <= 0 ||
            !D3D12FieldTextureFormat.TryFormat(declaration.Format, out var format))
        {
            return false;
        }

        try
        {
            var resource = device.OpenSharedHandle<ID3D12Resource>(declaration.NativeHandle);
            texture = new D3D12FieldTexture2D(
                resource,
                uploadResource: null,
                declaration.Width,
                declaration.Height,
                format,
                allowsUnorderedAccess: false,
                $"Aquarium D3D12 Shared Field Texture2D {declaration.ResourceKey}");
            texture.State = ResourceStates.PixelShaderResource;
            return true;
        }
        catch (SharpGenException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void CreateShaderResourceView(ID3D12Device device, D3D12DescriptorSlot descriptor)
    {
        device.CreateShaderResourceView(
            Resource,
            new ShaderResourceViewDescription
            {
                Format = Format,
                ViewDimension = ShaderResourceViewDimension.Texture2D,
                Shader4ComponentMapping = ShaderComponentMapping.Default,
                Texture2D = new Texture2DShaderResourceView { MipLevels = 1 },
            },
            descriptor.Cpu);
    }

    public bool TryCreateUnorderedAccessView(ID3D12Device device, D3D12DescriptorSlot descriptor)
    {
        if (!AllowsUnorderedAccess)
        {
            return false;
        }

        device.CreateUnorderedAccessView(
            Resource,
            null,
            new UnorderedAccessViewDescription
            {
                Format = Format,
                ViewDimension = UnorderedAccessViewDimension.Texture2D,
                Texture2D = new Texture2DUnorderedAccessView(),
            },
            descriptor.Cpu);
        return true;
    }

    public void Transition(ID3D12GraphicsCommandList commandList, ResourceStates nextState)
    {
        if (State == nextState)
        {
            return;
        }

        commandList.ResourceBarrier(ResourceBarrier.BarrierTransition(Resource, State, nextState));
        State = nextState;
    }

    public void Dispose()
    {
        uploadResource?.Dispose();
        Resource.Dispose();
    }

    private static D3D12FieldTexture2D CreateRgba8(
        ID3D12Device device,
        ID3D12GraphicsCommandList commandList,
        byte[] rgba,
        int width,
        int height,
        string name)
    {
        var texture = device.CreateCommittedResource(
            HeapType.Default,
            ResourceDescription.Texture2D(
                Format.R8G8B8A8_UNorm,
                (uint)width,
                (uint)height,
                1,
                1,
                1,
                0,
                ResourceFlags.None),
            ResourceStates.CopyDest,
            null);

        var rowBytes = checked(width * 4);
        var rowPitch = (int)Align(rowBytes, D3D12.TextureDataPitchAlignment);
        var uploadBytes = checked(rowPitch * height);
        var upload = device.CreateCommittedResource(
            HeapType.Upload,
            ResourceDescription.Buffer((ulong)uploadBytes),
            ResourceStates.GenericRead,
            null);
        upload.Name = $"{name} Upload";

        var mapped = upload.Map<byte>(0);
        try
        {
            for (var row = 0; row < height; row++)
            {
                rgba.AsSpan(row * rowBytes, rowBytes).CopyTo(new Span<byte>(mapped + (row * rowPitch), rowBytes));
            }
        }
        finally
        {
            upload.Unmap(0, null);
        }

        var source = new TextureCopyLocation(
            upload,
            new PlacedSubresourceFootPrint
            {
                Offset = 0,
                Footprint = new SubresourceFootPrint(Format.R8G8B8A8_UNorm, (uint)width, (uint)height, 1, (uint)rowPitch),
            });
        var destination = new TextureCopyLocation(texture, 0);
        commandList.CopyTextureRegion(destination, 0, 0, 0, source, null);
        var fieldTexture = new D3D12FieldTexture2D(
            texture,
            upload,
            width,
            height,
            Format.R8G8B8A8_UNorm,
            allowsUnorderedAccess: false,
            name: name);
        fieldTexture.Transition(commandList, ResourceStates.PixelShaderResource);
        return fieldTexture;
    }

    private static byte[] LoadRgba8(string path, out int width, out int height)
    {
#pragma warning disable CA1416
        using var source = new Bitmap(path);
        width = source.Width;
        height = source.Height;
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(source, 0, 0, width, height);
        }

        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var rgba = new byte[checked(width * height * 4)];
            for (var y = 0; y < height; y++)
            {
                var sourceRow = (byte*)data.Scan0 + y * data.Stride;
                var targetRow = y * width * 4;
                for (var x = 0; x < width; x++)
                {
                    var sourceOffset = x * 4;
                    var targetOffset = targetRow + sourceOffset;
                    rgba[targetOffset + 0] = sourceRow[sourceOffset + 2];
                    rgba[targetOffset + 1] = sourceRow[sourceOffset + 1];
                    rgba[targetOffset + 2] = sourceRow[sourceOffset + 0];
                    rgba[targetOffset + 3] = sourceRow[sourceOffset + 3];
                }
            }

            return rgba;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
#pragma warning restore CA1416
    }

    private static long Align(long value, long alignment)
    {
        return (value + alignment - 1) & ~(alignment - 1);
    }

}
