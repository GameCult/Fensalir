using Vortice.Direct3D12;

namespace Aquarium.Engine.Render;

internal readonly record struct D3D12FieldResourceStats(
    int Declared,
    int Resolved,
    int StructuredBuffers,
    int Unsupported,
    int StaleRemoved)
{
    public static D3D12FieldResourceStats Empty { get; } = new(0, 0, 0, 0, 0);
}

internal sealed class D3D12FieldResourceRegistry : IDisposable
{
    private const string RegistryPrefix = "field-resource:";

    private readonly Dictionary<string, StructuredBufferSlot> structuredBuffers = new(StringComparer.Ordinal);
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
                ResolveStructuredBuffer(device, resourceRegistry, declaration);
                resolved++;
                structuredBufferCount++;
                continue;
            }

            unsupported++;
        }

        var staleRemoved = RemoveStaleStructuredBuffers(resourceRegistry, liveKeys);
        return new D3D12FieldResourceStats(
            Declared: declarations.Count,
            Resolved: resolved,
            StructuredBuffers: structuredBufferCount,
            Unsupported: unsupported,
            StaleRemoved: staleRemoved);
    }

    public void Dispose()
    {
        foreach (var slot in structuredBuffers.Values)
        {
            slot.Buffer.Dispose();
        }

        structuredBuffers.Clear();
        liveKeys.Clear();
    }

    private void ResolveStructuredBuffer(
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
            return;
        }

        var registryKey = RegistryKey(declaration.ResourceKey);
        if (hasExisting && existing is not null)
        {
            resourceRegistry.RemoveStructuredBuffer(registryKey);
            existing.Buffer.Dispose();
        }

        var buffer = new D3D12StructuredBuffer(
            device,
            elementCount,
            strideBytes,
            $"Aquarium D3D12 Field Resource {declaration.ResourceKey}",
            allowUnorderedAccess);

        resourceRegistry.Add(registryKey, buffer);
        structuredBuffers[declaration.ResourceKey] = new StructuredBufferSlot(
            buffer,
            elementCount,
            strideBytes,
            allowUnorderedAccess,
            declaration.Version);
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

    private static string RegistryKey(string resourceKey) => RegistryPrefix + resourceKey;

    private sealed record StructuredBufferSlot(
        D3D12StructuredBuffer Buffer,
        int ElementCount,
        int StrideBytes,
        bool AllowUnorderedAccess,
        ulong Version);
}
