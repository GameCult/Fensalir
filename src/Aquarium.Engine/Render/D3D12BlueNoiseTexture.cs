using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Aquarium.Engine.Render;

internal sealed unsafe class D3D12BlueNoiseTexture : IDisposable
{
    public const int Size = 64;

    public ID3D12Resource Resource { get; }

    public ResourceStates State { get; private set; } = ResourceStates.CopyDest;

    private D3D12BlueNoiseTexture(ID3D12Resource resource, string name)
    {
        Resource = resource;
        Resource.Name = name;
    }

    public static D3D12BlueNoiseTexture Create(
        ID3D12Device device,
        ID3D12GraphicsCommandList commandList,
        string name,
        out ID3D12Resource uploadResource)
    {
        var data = CreateFarthestPointThresholdTile(Size);
        var texture = device.CreateCommittedResource(
            HeapType.Default,
            ResourceDescription.Texture2D(
                Format.R8_UNorm,
                Size,
                Size,
                1,
                1,
                1,
                0,
                ResourceFlags.None),
            ResourceStates.CopyDest,
            null);
        var blueNoise = new D3D12BlueNoiseTexture(texture, name);

        var rowPitch = (int)Align(Size, D3D12.TextureDataPitchAlignment);
        uploadResource = device.CreateCommittedResource(
            HeapType.Upload,
            ResourceDescription.Buffer((ulong)(rowPitch * Size)),
            ResourceStates.GenericRead,
            null);
        uploadResource.Name = $"{name} Upload";

        var mapped = uploadResource.Map<byte>(0);
        try
        {
            for (var row = 0; row < Size; row++)
            {
                data.AsSpan(row * Size, Size).CopyTo(new Span<byte>(mapped + (row * rowPitch), Size));
            }
        }
        finally
        {
            uploadResource.Unmap(0, null);
        }

        var source = new TextureCopyLocation(
            uploadResource,
            new PlacedSubresourceFootPrint
            {
                Offset = 0,
                Footprint = new SubresourceFootPrint(Format.R8_UNorm, Size, Size, 1, (uint)rowPitch),
            });
        var destination = new TextureCopyLocation(texture, 0);
        commandList.CopyTextureRegion(destination, 0, 0, 0, source, null);
        blueNoise.Transition(commandList, ResourceStates.PixelShaderResource);
        return blueNoise;
    }

    public void CreateShaderResourceView(ID3D12Device device, D3D12DescriptorSlot descriptor)
    {
        device.CreateShaderResourceView(
            Resource,
            new ShaderResourceViewDescription
            {
                Format = Format.R8_UNorm,
                ViewDimension = ShaderResourceViewDimension.Texture2D,
                Shader4ComponentMapping = ShaderComponentMapping.Default,
                Texture2D = new Texture2DShaderResourceView { MipLevels = 1 },
            },
            descriptor.Cpu);
    }

    public void Dispose()
    {
        Resource.Dispose();
    }

    private void Transition(ID3D12GraphicsCommandList commandList, ResourceStates nextState)
    {
        if (State == nextState)
        {
            return;
        }

        commandList.ResourceBarrier(ResourceBarrier.BarrierTransition(Resource, State, nextState));
        State = nextState;
    }

    private static byte[] CreateFarthestPointThresholdTile(int size)
    {
        var count = checked(size * size);
        var selected = new bool[count];
        var nearestDistanceSquared = new int[count];
        Array.Fill(nearestDistanceSquared, int.MaxValue);

        var rankByIndex = new int[count];
        var current = ((size * 19) + 7) % count;
        for (var rank = 0; rank < count; rank++)
        {
            selected[current] = true;
            rankByIndex[current] = rank;
            var cx = current % size;
            var cy = current / size;

            for (var index = 0; index < count; index++)
            {
                if (selected[index])
                {
                    nearestDistanceSquared[index] = -1;
                    continue;
                }

                var x = index % size;
                var y = index / size;
                var dx = Math.Abs(x - cx);
                var dy = Math.Abs(y - cy);
                dx = Math.Min(dx, size - dx);
                dy = Math.Min(dy, size - dy);
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared < nearestDistanceSquared[index])
                {
                    nearestDistanceSquared[index] = distanceSquared;
                }
            }

            var best = -1;
            var bestScore = int.MinValue;
            for (var index = 0; index < count; index++)
            {
                if (selected[index])
                {
                    continue;
                }

                var score = nearestDistanceSquared[index] * 4096 + TieBreak(index);
                if (score > bestScore)
                {
                    best = index;
                    bestScore = score;
                }
            }

            current = best < 0 ? 0 : best;
        }

        var data = new byte[count];
        for (var index = 0; index < count; index++)
        {
            data[index] = (byte)Math.Clamp((rankByIndex[index] * 255) / Math.Max(count - 1, 1), 0, 255);
        }

        return data;
    }

    private static int TieBreak(int value)
    {
        var x = unchecked((uint)value);
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return unchecked((int)(x & 0xfff));
    }

    private static long Align(long value, long alignment)
    {
        return (value + alignment - 1) & ~(alignment - 1);
    }
}
