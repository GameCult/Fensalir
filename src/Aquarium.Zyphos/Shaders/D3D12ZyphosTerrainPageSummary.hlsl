struct PageOutput { float4 height_gradient; float4 masks; };
struct PageSummary { float4 bounds; float4 metadata; };
StructuredBuffer<PageOutput> summary_inputs : register(t0);
RWStructuredBuffer<PageSummary> summary_outputs : register(u0);
cbuffer PageConstants : register(b0) { uint page_sample_count; }

[numthreads(1,1,1)]
void D3D12ZyphosTerrainPageSummaryCS(uint3 id : SV_DispatchThreadID)
{
    if(id.x!=0 || page_sample_count==0) return;
    float minimum_height=3.402823466e+38, maximum_height=-3.402823466e+38, maximum_slope=0.0, maximum_unresolved=0.0;
    [loop] for(uint index=0;index<page_sample_count;index++)
    {
        float4 sample=summary_inputs[index].height_gradient;
        minimum_height=min(minimum_height,sample.x); maximum_height=max(maximum_height,sample.x);
        maximum_slope=max(maximum_slope,length(sample.yz)); maximum_unresolved=max(maximum_unresolved,sample.w);
    }
    summary_outputs[0].bounds=float4(minimum_height,maximum_height,maximum_slope,maximum_unresolved);
    summary_outputs[0].metadata=float4(page_sample_count,0,0,1);
}
