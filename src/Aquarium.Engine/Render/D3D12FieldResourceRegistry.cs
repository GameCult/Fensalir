using SharpGen.Runtime;
using Vortice.Direct3D12;

namespace Aquarium.Engine.Render;

internal readonly record struct D3D12FieldResourceStats(
    int Declared,
    int Resolved,
    int StructuredBuffers,
    int Texture2D,
    int Unsupported,
    int StaleRemoved)
{
    public static D3D12FieldResourceStats Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

internal sealed class D3D12FieldResourceRegistry : IDisposable
{
    private const string RegistryPrefix = "field-resource:";

    private readonly Dictionary<string, StructuredBufferSlot> structuredBuffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2DSlot> textures = new(StringComparer.Ordinal);
    private readonly HashSet<string> liveKeys = new(StringComparer.Ordinal);

    public D3D12FieldResourceStats Resolve(
        ID3D12Device device,
        D3D12ResourceRegistry resourceRegistry,
        IReadOnlyList<AquariumFieldResourceDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(resourceRegistry);
        ArgumentNullException.ThrowIfNull(declarations);

        liveKeys.Clear();

        var resolved = 0;
        var structuredBufferCount = 0;
        var textureCount = 0;
        var unsupported = 0;
        foreach (var declaration in declarations)
        {
            if (!declaration.HasIdentity || !declaration.HasShape || !declaration.IsGpuVisible)
            {
                unsupported++;
                continue;
            }

            if (!liveKeys.Add(declaration.ResourceKey))
            {
                unsupported++;
                continue;
            }

            if (declaration.Kind is AquariumFieldResourceKind.StructuredBuffer or AquariumFieldResourceKind.CurvePointBuffer)
            {
                if (ResolveStructuredBuffer(device, resourceRegistry, declaration))
                {
                    resolved++;
                    structuredBufferCount++;
                }
                else
                {
                    unsupported++;
                }

                continue;
            }

            if (declaration.Kind == AquariumFieldResourceKind.Texture2D)
            {
                textureCount++;
                continue;
            }

            unsupported++;
        }

        var staleRemoved = RemoveStaleStructuredBuffers(resourceRegistry, liveKeys);
        staleRemoved += RemoveStaleTextures(liveKeys);
        return new D3D12FieldResourceStats(
            Declared: declarations.Count,
            Resolved: resolved,
            StructuredBuffers: structuredBufferCount,
            Texture2D: textureCount,
            Unsupported: unsupported,
            StaleRemoved: staleRemoved);
    }

    public void Dispose()
    {
        foreach (var slot in structuredBuffers.Values)
        {
            slot.Buffer.Dispose();
        }

        foreach (var slot in textures.Values)
        {
            slot.Texture.Dispose();
        }

        structuredBuffers.Clear();
        textures.Clear();
        liveKeys.Clear();
    }

    public bool TryGetStructuredBuffer(string resourceKey, out D3D12StructuredBuffer buffer)
    {
        if (structuredBuffers.TryGetValue(resourceKey, out var slot))
        {
            buffer = slot.Buffer;
            return true;
        }

        buffer = null!;
        return false;
    }

    public bool TryGetTexture2D(string resourceKey, out D3D12FieldTexture2D texture)
    {
        if (textures.TryGetValue(resourceKey, out var slot))
        {
            texture = slot.Texture;
            return true;
        }

        texture = null!;
        return false;
    }

    public bool TryResolveTexture2D(
        ID3D12Device device,
        ID3D12GraphicsCommandList commandList,
        AquariumFieldResourceDeclaration declaration,
        out D3D12FieldTexture2D texture)
    {
        texture = null!;
        if (declaration.Kind != AquariumFieldResourceKind.Texture2D ||
            declaration.Residency != AquariumFieldResourceResidency.GpuResident ||
            declaration.Access != AquariumFieldShaderAccess.ShaderResource ||
            !declaration.HasSourceAsset)
        {
            return false;
        }

        if (textures.TryGetValue(declaration.ResourceKey, out var existing) &&
            existing.Version == declaration.Version &&
            string.Equals(existing.SourceUri, declaration.SourceUri, StringComparison.Ordinal))
        {
            texture = existing.Texture;
            return true;
        }

        if (existing is not null)
        {
            existing.Texture.Dispose();
            textures.Remove(declaration.ResourceKey);
        }

        try
        {
            texture = D3D12FieldTexture2D.LoadLocalAsset(device, commandList, declaration);
            textures[declaration.ResourceKey] = new Texture2DSlot(texture, declaration.SourceUri, declaration.Version);
            return true;
        }
        catch (IOException)
        {
            texture = null!;
            return false;
        }
        catch (InvalidOperationException)
        {
            texture = null!;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            texture = null!;
            return false;
        }
        catch (ArgumentException)
        {
            texture = null!;
            return false;
        }
        catch (SharpGenException)
        {
            texture = null!;
            return false;
        }
    }

    private bool ResolveStructuredBuffer(
        ID3D12Device device,
        D3D12ResourceRegistry resourceRegistry,
        AquariumFieldResourceDeclaration declaration)
    {
        var elementCount = Math.Max(1, declaration.DepthOrCount > 0 ? declaration.DepthOrCount : declaration.Width);
        var strideBytes = Math.Max(1, declaration.StrideBytes);
        var allowUnorderedAccess = declaration.Access == AquariumFieldShaderAccess.UnorderedAccess;

        var hasExisting = structuredBuffers.TryGetValue(declaration.ResourceKey, out var existing);
        if (hasExisting && existing is not null &&
            existing.ElementCount == elementCount &&
            existing.StrideBytes == strideBytes &&
            existing.AllowUnorderedAccess == allowUnorderedAccess &&
            existing.Version == declaration.Version)
        {
            return true;
        }

        var registryKey = RegistryKey(declaration.ResourceKey);
        if (hasExisting && existing is not null)
        {
            resourceRegistry.RemoveStructuredBuffer(registryKey);
            existing.Buffer.Dispose();
        }

        var buffer = declaration.Residency == AquariumFieldResourceResidency.SharedGpu
            ? OpenSharedStructuredBuffer(device, declaration, elementCount, strideBytes)
            : new D3D12StructuredBuffer(
                device,
                elementCount,
                strideBytes,
                $"Aquarium D3D12 Field Resource {declaration.ResourceKey}",
                allowUnorderedAccess);

        if (buffer is null)
        {
            return false;
        }

        resourceRegistry.Add(registryKey, buffer);
        structuredBuffers[declaration.ResourceKey] = new StructuredBufferSlot(
            buffer,
            elementCount,
            strideBytes,
            allowUnorderedAccess,
            declaration.Version);
        return true;
    }

    private int RemoveStaleStructuredBuffers(D3D12ResourceRegistry resourceRegistry, HashSet<string> currentKeys)
    {
        var removed = 0;
        foreach (var key in structuredBuffers.Keys.ToArray())
        {
            if (currentKeys.Contains(key))
            {
                continue;
            }

            resourceRegistry.RemoveStructuredBuffer(RegistryKey(key));
            structuredBuffers[key].Buffer.Dispose();
            structuredBuffers.Remove(key);
            removed++;
        }

        return removed;
    }

    private int RemoveStaleTextures(HashSet<string> currentKeys)
    {
        var removed = 0;
        foreach (var key in textures.Keys.ToArray())
        {
            if (currentKeys.Contains(key))
            {
                continue;
            }

            textures[key].Texture.Dispose();
            textures.Remove(key);
            removed++;
        }

        return removed;
    }

    private static string RegistryKey(string resourceKey) => RegistryPrefix + resourceKey;

    private static D3D12StructuredBuffer? OpenSharedStructuredBuffer(
        ID3D12Device device,
        AquariumFieldResourceDeclaration declaration,
        int elementCount,
        int strideBytes)
    {
        if (declaration.NativeHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var resource = device.OpenSharedHandle<ID3D12Resource>(declaration.NativeHandle);
            resource.Name = $"Aquarium D3D12 Shared Field Resource {declaration.ResourceKey}";
            return new D3D12StructuredBuffer(
                resource,
                elementCount,
                strideBytes,
                ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
        }
        catch (SharpGenException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private sealed record StructuredBufferSlot(
        D3D12StructuredBuffer Buffer,
        int ElementCount,
        int StrideBytes,
        bool AllowUnorderedAccess,
        ulong Version);

    private sealed record Texture2DSlot(
        D3D12FieldTexture2D Texture,
        string SourceUri,
        ulong Version);
}
