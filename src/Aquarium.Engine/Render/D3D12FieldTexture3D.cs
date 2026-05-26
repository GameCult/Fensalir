using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Aquarium.Engine.Render;

internal sealed class D3D12FieldTexture3D : IDisposable
{
    private D3D12FieldTexture3D(
        ID3D12Resource resource,
        int width,
        int height,
        int depth,
        Format format,
        string name)
    {
        Resource = resource;
        Width = width;
        Height = height;
        Depth = depth;
        Format = format;
        Resource.Name = name;
    }

    public ID3D12Resource Resource { get; }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public Format Format { get; }

    public ResourceStates State { get; private set; } = ResourceStates.PixelShaderResource;

    public static bool TryCreateEmpty(
        ID3D12Device device,
        AquariumFieldResourceDeclaration declaration,
        out D3D12FieldTexture3D texture)
    {
        texture = null!;
        if (!D3D12FieldTextureFormat.TryFormat(declaration.Format, out var format))
        {
            return false;
        }

        var width = Math.Max(1, declaration.Width);
        var height = Math.Max(1, declaration.Height);
        var depth = Math.Max(1, declaration.DepthOrCount);
        var resource = device.CreateCommittedResource(
            HeapType.Default,
            ResourceDescription.Texture3D(
                format,
                (uint)width,
                (uint)height,
                (ushort)Math.Min(depth, ushort.MaxValue),
                1,
                ResourceFlags.None),
            ResourceStates.PixelShaderResource,
            null);

        texture = new D3D12FieldTexture3D(
            resource,
            width,
            height,
            depth,
            format,
            $"Aquarium D3D12 Field VolumeTexture {declaration.ResourceKey}");
        return true;
    }

    public void CreateShaderResourceView(ID3D12Device device, D3D12DescriptorSlot descriptor)
    {
        device.CreateShaderResourceView(
            Resource,
            new ShaderResourceViewDescription
            {
                Format = Format,
                ViewDimension = ShaderResourceViewDimension.Texture3D,
                Shader4ComponentMapping = ShaderComponentMapping.Default,
                Texture3D = new Texture3DShaderResourceView { MipLevels = 1 },
            },
            descriptor.Cpu);
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
        Resource.Dispose();
    }
}
