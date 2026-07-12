#include "D3D12ZyphosTerrain.hlsli"

struct PageInput { float4 direction_radius; float4 sampling; };
struct PageOutput { float4 height_gradient; float4 masks; };
StructuredBuffer<PageInput> page_inputs : register(t0);
RWStructuredBuffer<PageOutput> page_outputs : register(u0);
cbuffer PageConstants : register(b0) { uint page_sample_count; }

float zyPageErosion(float3 direction, float radius, float spacing, float parent_spacing)
{
    float3 dir=normalize(direction);
    float child=zyAdvancedErosion(dir,zySphericalField(dir),radius,spacing).x;
    if(parent_spacing<=0.0)return child;
    float parent=zyAdvancedErosion(dir,zySphericalField(dir),radius,parent_spacing).x;
    return child-parent;
}


[numthreads(64,1,1)]
void D3D12ZyphosTerrainPageCS(uint3 id : SV_DispatchThreadID)
{
    if(id.x>=page_sample_count) return;
    PageInput input=page_inputs[id.x]; float3 dir=normalize(input.direction_radius.xyz); float radius=input.direction_radius.w; float spacing=input.sampling.x; float parent_spacing=input.sampling.y;
    float3 reference=abs(dir.z)<0.8?float3(0,0,1):float3(0,1,0); float3 tangent_x=normalize(cross(reference,dir)); float3 tangent_y=normalize(cross(dir,tangent_x));
    float angular=max(spacing/radius,1.0e-6); float center=zyPageErosion(dir,radius,spacing,parent_spacing);
    float dx=(zyPageErosion(normalize(dir+tangent_x*angular),radius,spacing,parent_spacing)-zyPageErosion(normalize(dir-tangent_x*angular),radius,spacing,parent_spacing))/(2.0*spacing);
    float dy=(zyPageErosion(normalize(dir+tangent_y*angular),radius,spacing,parent_spacing)-zyPageErosion(normalize(dir-tangent_y*angular),radius,spacing,parent_spacing))/(2.0*spacing);
    float4 erosion=zyAdvancedErosion(dir,zySphericalField(dir),radius,spacing);
    page_outputs[id.x].height_gradient=float4(center,dx,dy,erosion.w);
    page_outputs[id.x].masks=float4(erosion.y,erosion.z,0,1);
}
