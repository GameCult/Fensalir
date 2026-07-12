#include "D3D12SdfCommon.hlsli"

struct PlanetaryPatchVertexOut { float4 position:SV_Position; };
PlanetaryPatchVertexOut D3D12ZyphosTerrainPatchVS(uint vertexId:SV_VertexID,uint instanceId:SV_InstanceID)
{
    PlanetaryPatchVertexOut output; output.position=float4(-2,-2,1,1); return output;
}
SceneOut D3D12ZyphosTerrainPatchPS(PlanetaryPatchVertexOut input)
{
    discard; return (SceneOut)0;
}
