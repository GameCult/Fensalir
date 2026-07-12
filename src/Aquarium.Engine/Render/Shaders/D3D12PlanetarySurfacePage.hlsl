struct PageInput { float4 direction_radius; float4 sampling; };
struct PageOutput { float4 height_gradient; float4 masks; };
StructuredBuffer<PageInput> page_inputs : register(t0);
RWStructuredBuffer<PageOutput> page_outputs : register(u0);
cbuffer PageConstants : register(b0) { uint page_sample_count; }
[numthreads(64,1,1)]
void D3D12ZyphosTerrainPageCS(uint3 id : SV_DispatchThreadID)
{
    if(id.x>=page_sample_count)return;
    page_outputs[id.x].height_gradient=0;
    page_outputs[id.x].masks=float4(0,0,0,1);
}
