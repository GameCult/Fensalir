using SharpGen.Runtime;
using Vortice.Direct3D12;

namespace Aquarium.Engine.Render;

internal readonly record struct D3D12FieldResourceStats(
    int Declared,
    int Resolved,
    int StructuredBuffers,
    int Texture2D,
    int SurfacePages,
    int VolumeTextures,
    int Meshes,
    int Unsupported,
    int StaleRemoved)
{
    public static D3D12FieldResourceStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}

internal sealed class D3D12FieldResourceRegistry : IDisposable
{
    private const string RegistryPrefix = "field-resource:";

    private readonly Dictionary<string, StructuredBufferSlot> structuredBuffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2DSlot> textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VolumeTextureSlot> volumeTextures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, D3D12FieldMesh> meshes = new(StringComparer.Ordinal);
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
        var surfacePageCount = 0;
        var volumeTextureCount = 0;
        var meshCount = 0;
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

            if (declaration.Kind == AquariumFieldResourceKind.SurfacePage)
            {
                surfacePageCount++;
                if (!declaration.HasSourceAsset && ResolveSurfacePage(device, declaration))
                {
                    resolved++;
                }
                else if (declaration.HasSourceAsset)
                {
                    // Local asset surface pages need a command list and are loaded lazily by TryResolveSurfacePage.
                }
                else
                {
                    unsupported++;
                }

                continue;
            }

            if (declaration.Kind == AquariumFieldResourceKind.VolumeTexture)
            {
                volumeTextureCount++;
                if (ResolveVolumeTexture(device, declaration))
                {
                    resolved++;
                }
                else
                {
                    unsupported++;
                }

                continue;
            }

            if (declaration.Kind == AquariumFieldResourceKind.Mesh)
            {
                meshCount++;
                if (ResolveMesh(device, declaration))
                {
                    resolved++;
                }
                else
                {
                    unsupported++;
                }

                continue;
            }

            unsupported++;
        }

        var staleRemoved = RemoveStaleStructuredBuffers(resourceRegistry, liveKeys);
        staleRemoved += RemoveStaleTextures(liveKeys);
        staleRemoved += RemoveStaleVolumeTextures(liveKeys);
        staleRemoved += RemoveStaleMeshes(liveKeys);
        return new D3D12FieldResourceStats(
            Declared: declarations.Count,
            Resolved: resolved,
            StructuredBuffers: structuredBufferCount,
            Texture2D: textureCount,
            SurfacePages: surfacePageCount,
            VolumeTextures: volumeTextureCount,
            Meshes: meshCount,
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

        foreach (var slot in volumeTextures.Values)
        {
            slot.Texture.Dispose();
        }

        foreach (var mesh in meshes.Values)
        {
            mesh.Dispose();
        }

        structuredBuffers.Clear();
        textures.Clear();
        volumeTextures.Clear();
        meshes.Clear();
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

    public void MarkStructuredBufferUpload(string resourceKey, int elementOffset, int elementCount)
    {
        if (!structuredBuffers.TryGetValue(resourceKey, out var slot) ||
            elementOffset < 0 ||
            elementCount <= 0)
        {
            return;
        }

        var firstFullColumn = (elementOffset + slot.Width - 1) / slot.Width;
        var endFullColumn = (elementOffset + elementCount) / slot.Width;
        if (endFullColumn <= firstFullColumn)
        {
            return;
        }

        var validColumns = slot.ValidColumns;
        for (var column = Math.Max(0, firstFullColumn); column < Math.Min(validColumns.Length, endFullColumn); column++)
        {
            validColumns[column] = true;
        }
    }

    public int CountContiguousValidColumns(AquariumFieldTubeSplineLowering lowering)
    {
        if (!structuredBuffers.TryGetValue(lowering.ResourceKey, out var slot))
        {
            return 0;
        }

        var normalized = lowering.Normalized();
        var validCount = 0;
        for (var logicalColumn = 0; logicalColumn < normalized.ColumnCount; logicalColumn++)
        {
            var physicalColumn = normalized.FirstColumn + logicalColumn * normalized.ColumnStride;
            if (normalized.RollingModulo > 0)
            {
                physicalColumn = PositiveModulo(physicalColumn + normalized.RollingOffset, normalized.RollingModulo);
            }

            physicalColumn = Math.Min(physicalColumn, slot.ValidColumns.Length - 1);
            if (physicalColumn < 0 ||
                physicalColumn >= slot.ValidColumns.Length ||
                !slot.ValidColumns[physicalColumn])
            {
                break;
            }

            validCount++;
        }

        return validCount;
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

    public bool TryGetSurfacePage(string resourceKey, out D3D12FieldTexture2D surfacePage)
    {
        if (textures.TryGetValue(resourceKey, out var slot) &&
            slot.Kind == AquariumFieldResourceKind.SurfacePage)
        {
            surfacePage = slot.Texture;
            return true;
        }

        surfacePage = null!;
        return false;
    }

    public bool TryGetVolumeTexture(string resourceKey, out D3D12FieldTexture3D volumeTexture)
    {
        if (volumeTextures.TryGetValue(resourceKey, out var slot))
        {
            volumeTexture = slot.Texture;
            return true;
        }

        volumeTexture = null!;
        return false;
    }

    public bool TryGetMesh(string resourceKey, out D3D12FieldMesh mesh)
    {
        if (meshes.TryGetValue(resourceKey, out mesh!))
        {
            return true;
        }

        mesh = null!;
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
            textures[declaration.ResourceKey] = new Texture2DSlot(texture, declaration.Kind, declaration.SourceUri, declaration.Version);
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

    public bool TryResolveSurfacePage(
        ID3D12Device device,
        ID3D12GraphicsCommandList commandList,
        AquariumFieldResourceDeclaration declaration,
        out D3D12FieldTexture2D surfacePage)
    {
        surfacePage = null!;
        if (declaration.Kind != AquariumFieldResourceKind.SurfacePage ||
            declaration.Residency != AquariumFieldResourceResidency.GpuResident ||
            declaration.Access != AquariumFieldShaderAccess.ShaderResource)
        {
            return false;
        }

        if (textures.TryGetValue(declaration.ResourceKey, out var existing) &&
            existing.Kind == AquariumFieldResourceKind.SurfacePage &&
            existing.Version == declaration.Version &&
            string.Equals(existing.SourceUri, declaration.SourceUri, StringComparison.Ordinal))
        {
            surfacePage = existing.Texture;
            return true;
        }

        if (existing is not null)
        {
            existing.Texture.Dispose();
            textures.Remove(declaration.ResourceKey);
        }

        try
        {
            surfacePage = declaration.HasSourceAsset
                ? D3D12FieldTexture2D.LoadLocalAsset(device, commandList, declaration)
                : D3D12FieldTexture2D.TryCreateEmpty(device, declaration, out var emptySurfacePage)
                    ? emptySurfacePage
                    : null!;
            if (surfacePage is null)
            {
                return false;
            }

            textures[declaration.ResourceKey] = new Texture2DSlot(
                surfacePage,
                AquariumFieldResourceKind.SurfacePage,
                declaration.SourceUri,
                declaration.Version);
            return true;
        }
        catch (IOException)
        {
            surfacePage = null!;
            return false;
        }
        catch (InvalidOperationException)
        {
            surfacePage = null!;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            surfacePage = null!;
            return false;
        }
        catch (ArgumentException)
        {
            surfacePage = null!;
            return false;
        }
        catch (SharpGenException)
        {
            surfacePage = null!;
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
        var width = Math.Max(1, declaration.Width);
        var columnCount = Math.Max(1, (elementCount + width - 1) / width);
        if (hasExisting && existing is not null &&
            existing.ElementCount == elementCount &&
            existing.StrideBytes == strideBytes &&
            existing.Width == width &&
            existing.AllowUnorderedAccess == allowUnorderedAccess)
        {
            structuredBuffers[declaration.ResourceKey] = existing with { Version = declaration.Version };
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
            width,
            new bool[columnCount],
            strideBytes,
            allowUnorderedAccess,
            declaration.Version);
        return true;
    }

    private bool ResolveSurfacePage(
        ID3D12Device device,
        AquariumFieldResourceDeclaration declaration)
    {
        if (declaration.Residency != AquariumFieldResourceResidency.GpuResident ||
            declaration.Access != AquariumFieldShaderAccess.ShaderResource)
        {
            return false;
        }

        if (textures.TryGetValue(declaration.ResourceKey, out var existing) &&
            existing.Kind == AquariumFieldResourceKind.SurfacePage &&
            existing.Version == declaration.Version &&
            string.Equals(existing.SourceUri, declaration.SourceUri, StringComparison.Ordinal))
        {
            return true;
        }

        if (existing is not null)
        {
            existing.Texture.Dispose();
            textures.Remove(declaration.ResourceKey);
        }

        if (!D3D12FieldTexture2D.TryCreateEmpty(device, declaration, out var surfacePage))
        {
            return false;
        }

        textures[declaration.ResourceKey] = new Texture2DSlot(
            surfacePage,
            AquariumFieldResourceKind.SurfacePage,
            declaration.SourceUri,
            declaration.Version);
        return true;
    }

    private bool ResolveVolumeTexture(
        ID3D12Device device,
        AquariumFieldResourceDeclaration declaration)
    {
        if (declaration.Residency != AquariumFieldResourceResidency.GpuResident ||
            declaration.Access != AquariumFieldShaderAccess.ShaderResource)
        {
            return false;
        }

        if (volumeTextures.TryGetValue(declaration.ResourceKey, out var existing) &&
            existing.Version == declaration.Version &&
            existing.Width == declaration.Width &&
            existing.Height == declaration.Height &&
            existing.Depth == declaration.DepthOrCount)
        {
            return true;
        }

        if (existing is not null)
        {
            existing.Texture.Dispose();
            volumeTextures.Remove(declaration.ResourceKey);
        }

        if (!D3D12FieldTexture3D.TryCreateEmpty(device, declaration, out var volumeTexture))
        {
            return false;
        }

        volumeTextures[declaration.ResourceKey] = new VolumeTextureSlot(
            volumeTexture,
            declaration.Width,
            declaration.Height,
            declaration.DepthOrCount,
            declaration.Version);
        return true;
    }

    private bool ResolveMesh(
        ID3D12Device device,
        AquariumFieldResourceDeclaration declaration)
    {
        var mesh = declaration.Mesh;
        if (!mesh.IsValid || declaration.Residency != AquariumFieldResourceResidency.GpuResident)
        {
            return false;
        }

        if (meshes.TryGetValue(declaration.ResourceKey, out var existing) &&
            existing.Version == declaration.Version &&
            existing.Vertices.ElementCount == mesh.Vertices.Count &&
            existing.Vertices.StrideBytes == mesh.Vertices.StrideBytes &&
            existing.Indices.ElementCount == mesh.Indices.Count &&
            existing.Indices.StrideBytes == mesh.Indices.StrideBytes &&
            existing.Topology == mesh.Topology &&
            existing.IndexFormat == mesh.IndexFormat &&
            existing.Layout == mesh.Layout)
        {
            return true;
        }

        if (existing is not null)
        {
            existing.Dispose();
            meshes.Remove(declaration.ResourceKey);
        }

        var vertices = new D3D12StructuredBuffer(
            device,
            mesh.Vertices.Count,
            mesh.Vertices.StrideBytes,
            $"Aquarium D3D12 Field Mesh Vertices {declaration.ResourceKey}");
        var indices = new D3D12StructuredBuffer(
            device,
            mesh.Indices.Count,
            mesh.Indices.StrideBytes,
            $"Aquarium D3D12 Field Mesh Indices {declaration.ResourceKey}");
        meshes[declaration.ResourceKey] = new D3D12FieldMesh(
            vertices,
            indices,
            mesh.Topology,
            mesh.IndexFormat,
            mesh.Layout,
            mesh.SubmeshCount,
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

    private int RemoveStaleVolumeTextures(HashSet<string> currentKeys)
    {
        var removed = 0;
        foreach (var key in volumeTextures.Keys.ToArray())
        {
            if (currentKeys.Contains(key))
            {
                continue;
            }

            volumeTextures[key].Texture.Dispose();
            volumeTextures.Remove(key);
            removed++;
        }

        return removed;
    }

    private int RemoveStaleMeshes(HashSet<string> currentKeys)
    {
        var removed = 0;
        foreach (var key in meshes.Keys.ToArray())
        {
            if (currentKeys.Contains(key))
            {
                continue;
            }

            meshes[key].Dispose();
            meshes.Remove(key);
            removed++;
        }

        return removed;
    }

    private static string RegistryKey(string resourceKey) => RegistryPrefix + resourceKey;

    private static int PositiveModulo(int value, int modulo)
    {
        var remainder = value % modulo;
        return remainder < 0 ? remainder + modulo : remainder;
    }

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
        int Width,
        bool[] ValidColumns,
        int StrideBytes,
        bool AllowUnorderedAccess,
        ulong Version);

    private sealed record Texture2DSlot(
        D3D12FieldTexture2D Texture,
        AquariumFieldResourceKind Kind,
        string SourceUri,
        ulong Version);

    private sealed record VolumeTextureSlot(
        D3D12FieldTexture3D Texture,
        int Width,
        int Height,
        int Depth,
        ulong Version);
}
