#include "GameCult.Geometry/GameCult.Geometry.hlsl"

struct ParityInput { float4 position_base; float4 slope_fade; float4 scalar0; float4 rounding; float4 onset; float4 scalar1; float4 scalar2; float4 scalar3; };
struct ParityOutput { float4 delta_magnitude; float4 ridge_fade; };
StructuredBuffer<ParityInput> inputs : register(t0);
RWStructuredBuffer<ParityOutput> outputs : register(u0);
cbuffer ParityConstants : register(b0) { uint input_count; }

[numthreads(64, 1, 1)]
void D3D12AdvancedErosionParityCS(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= input_count) return;
    ParityInput input = inputs[id.x];
    GameCultGeometryAdvancedErosionParameters p;
    p.scale=input.scalar0.x; p.strength=input.scalar0.y; p.gully_weight=input.scalar0.z; p.detail=input.scalar0.w;
    p.rounding=input.rounding; p.onset=input.onset; p.assumed_slope=input.scalar1.xy;
    p.cell_scale=input.scalar1.z; p.normalization=input.scalar1.w; p.octaves=(int)input.scalar2.x;
    p.lacunarity=input.scalar2.y; p.gain=input.scalar2.z;
    GameCultGeometryErosionBandSelection band; band.active_octaves=(int)input.scalar3.x; band.final_octave_weight=input.scalar3.y; band.finest_included_wavelength=input.scalar3.z; band.unresolved_height_bound=input.scalar3.w;
    GameCultGeometryAdvancedErosionResult r = gamecult_geometry_advanced_erosion_filter_banded(input.position_base.xy, float3(input.position_base.z, input.slope_fade.xy), input.slope_fade.z, p, band);
    outputs[id.x].delta_magnitude=float4(r.delta, r.magnitude);
    outputs[id.x].ridge_fade=float4(r.ridge_map, r.fade_target, 0, 0);
}
