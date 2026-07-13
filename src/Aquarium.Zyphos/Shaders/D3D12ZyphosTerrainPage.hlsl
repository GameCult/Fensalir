#include "D3D12ZyphosTerrain.hlsli"

struct PageInput { float4 direction_radius; float4 sampling; };
struct PageOutput { float4 height_gradient; float4 masks; };
StructuredBuffer<PageInput> page_inputs : register(t0);
RWStructuredBuffer<PageOutput> page_outputs : register(u0);
cbuffer PageConstants : register(b0) { uint page_sample_count; }

CultMathPlanetaryPageSample zyPageErosion(float3 direction, float radius, float spacing, float parent_spacing)
{
    float3 dir=normalize(direction);
    float field=zySphericalField(dir);
    CultMathPlanetarySurfaceSample child=zyAdvancedErosionSurface(dir,field,radius,spacing);
    CultMathPlanetarySurfaceSample parent=(CultMathPlanetarySurfaceSample)0;
    bool has_parent=parent_spacing>0.0;
    if(has_parent)parent=zyAdvancedErosionSurface(dir,field,radius,parent_spacing);
    return cultmath_planetary_residual_sample(child,parent,has_parent);
}


[numthreads(64,1,1)]
void D3D12ZyphosTerrainPageCS(uint3 id : SV_DispatchThreadID)
{
    if(id.x>=page_sample_count) return;
    PageInput input=page_inputs[id.x]; float3 dir=normalize(input.direction_radius.xyz); float radius=input.direction_radius.w; float spacing=input.sampling.x; float parent_spacing=input.sampling.y;
    CultMathPlanetaryPageSample sample=zyPageErosion(dir,radius,spacing,parent_spacing);
    CultMathPlanetarySurfaceSample child=zyAdvancedErosionSurface(dir,zySphericalField(dir),radius,spacing);
    page_outputs[id.x].height_gradient=sample.height_gradient;
    page_outputs[id.x].masks=float4(sample.masks,0,child.unresolved_height_bound);
}
