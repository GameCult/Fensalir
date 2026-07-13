#include "CultMath/CultMath.hlsl"

struct PageOutput { float4 height_gradient; float4 masks; };
struct PageSummary { float4 bounds; float4 metadata; };
StructuredBuffer<PageOutput> summary_inputs : register(t0);
RWStructuredBuffer<PageSummary> summary_outputs : register(u0);
cbuffer PageConstants : register(b0) { uint page_sample_count; uint page_sample_offset; uint page_summary_index; }

[numthreads(1,1,1)]
void D3D12ZyphosTerrainPageSummaryCS(uint3 id : SV_DispatchThreadID)
{
    if (id.x != 0 || page_sample_count == 0) return;
    float4 summary = cultmath_planetary_summary_empty();
    [loop] for (uint index = 0; index < page_sample_count; index++)
    {
        PageOutput sample = summary_inputs[page_sample_offset + index];
        summary = cultmath_planetary_summary_accumulate(summary, sample.height_gradient, sample.masks.w);
    }
    summary_outputs[page_summary_index].bounds = summary;
    summary_outputs[page_summary_index].metadata = float4(page_sample_count, page_sample_offset, page_summary_index, 1.0);
}
