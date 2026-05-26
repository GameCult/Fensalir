using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.Direct3D;

namespace Aquarium.Engine.Render;

internal sealed class D3D12FieldMesh : IDisposable
{
    public D3D12FieldMesh(
        D3D12StructuredBuffer vertices,
        D3D12StructuredBuffer indices,
        AquariumFieldMeshTopology topology,
        AquariumFieldMeshIndexFormat indexFormat,
        int submeshCount,
        ulong version)
    {
        Vertices = vertices;
        Indices = indices;
        Topology = topology;
        IndexFormat = indexFormat;
        SubmeshCount = submeshCount;
        Version = version;
    }

    public D3D12StructuredBuffer Vertices { get; }

    public D3D12StructuredBuffer Indices { get; }

    public AquariumFieldMeshTopology Topology { get; }

    public AquariumFieldMeshIndexFormat IndexFormat { get; }

    public int SubmeshCount { get; }

    public ulong Version { get; }

    public PrimitiveTopology D3DTopology => Topology switch
    {
        AquariumFieldMeshTopology.TriangleStrip => PrimitiveTopology.TriangleStrip,
        AquariumFieldMeshTopology.LineList => PrimitiveTopology.LineList,
        AquariumFieldMeshTopology.LineStrip => PrimitiveTopology.LineStrip,
        AquariumFieldMeshTopology.PointList => PrimitiveTopology.PointList,
        _ => PrimitiveTopology.TriangleList,
    };

    public Format D3DIndexFormat => IndexFormat == AquariumFieldMeshIndexFormat.UInt16
        ? Format.R16_UInt
        : Format.R32_UInt;

    public VertexBufferView VertexBufferView => new(
        Vertices.Resource.GPUVirtualAddress,
        (uint)Vertices.SizeBytes,
        (uint)Vertices.StrideBytes);

    public IndexBufferView IndexBufferView => new(
        Indices.Resource.GPUVirtualAddress,
        (uint)Indices.SizeBytes,
        D3DIndexFormat);

    public void Dispose()
    {
        Indices.Dispose();
        Vertices.Dispose();
    }
}
