struct PageOutput { float4 height_gradient; float4 masks; };
struct PageSummary { float4 bounds; float4 metadata; };
StructuredBuffer<PageOutput> summary_inputs : register(t0);
RWStructuredBuffer<PageSummary> summary_outputs : register(u0);
cbuffer PageConstants : register(b0) { uint page_sample_count; uint page_sample_offset; uint page_summary_index; }

[numthreads(1,1,1)]
void D3D12ZyphosTerrainPageSummaryCS(uint3 id : SV_DispatchThreadID)
{
    if (id.x != 0 || page_sample_count == 0) return;
    float minimum_height = 3.402823466e+38;
    float maximum_height = -3.402823466e+38;
    float maximum_slope = 0.0;
    float maximum_unresolved = 0.0;
    [loop] for (uint index = 0; index < page_sample_count; index++)
    {
        float4 sample = summary_inputs[page_sample_offset + index].height_gradient;
        minimum_height = min(minimum_height, sample.x);
        maximum_height = max(maximum_height, sample.x);
        maximum_slope = max(maximum_slope, length(sample.yzw));
        maximum_unresolved = max(maximum_unresolved, summary_inputs[page_sample_offset + index].masks.w);
    }
    summary_outputs[page_summary_index].bounds = float4(minimum_height, maximum_height, maximum_slope, maximum_unresolved);
    summary_outputs[page_summary_index].metadata = float4(page_sample_count, page_sample_offset, page_summary_index, 1.0);
}
