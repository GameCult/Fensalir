cbuffer AquariumFrame : register(b0)
{
    float2 resolution;
    float timeSeconds;
    float viewRadius;
    float3 cameraPosition;
    float farDistance;
    float3 cameraTarget;
    float sceneFlags;
    float2 viewCenter;
    float frameIndex;
    float previousTimeSeconds;
    float3 previousCameraPosition;
    float previousViewRadius;
    float3 previousCameraTarget;
    float previousSceneFlags;
    float2 previousViewCenter;
    float2 jitterPixels;
    float2 previousJitterPixels;
    float renderDebugMode;
    float exposure;
    float bloomIntensity;
    float bloomVeilIntensity;
    float4 cursorWorlds;
    float4 temporalGaussianInfo;
    float4 cameraFrustumXy;
    float4 cameraFrustumZ;
    float4 gpuFusionInfo;
    float4 fractalReservoirInfo;
    float4 fractalReservoirFrame;
    float4 reservoirBudgetInfo;
};

Texture2D<float4> sourceTexture : register(t0);
Texture2D<float4> currentSceneMetadataTexture : register(t5);
Texture2D<float4> currentSceneControlTexture : register(t7);
Texture2D<float4> currentReservoirGuideTexture : register(t26);

#include "D3D12FieldReservoir.hlsli"

StructuredBuffer<FieldReservoirSample> fieldReservoirCandidates : register(t45);
RWStructuredBuffer<FieldReservoirSample> reservoirHistoryWrite : register(u22);
RWTexture2D<float4> reservoirResolvedTexture : register(u23);

float4 loadTexturePixel(Texture2D<float4> source, uint2 pixel)
{
    return source.Load(int3(pixel, 0));
}

FieldReservoirSample sceneFieldReservoirSample(uint2 pixel, float2 uv)
{
    float4 colorTravel = loadTexturePixel(sourceTexture, pixel);
    float4 metadata = loadTexturePixel(currentSceneMetadataTexture, pixel);
    float4 control = loadTexturePixel(currentSceneControlTexture, pixel);
    float4 guide = loadTexturePixel(currentReservoirGuideTexture, pixel);
    float target = fieldReservoirDefaultTarget(colorTravel, control, guide);
    return makeFieldReservoirSample(
        colorTravel,
        metadata,
        control,
        guide,
        float4(uv, colorTravel.w, 0.0),
        float4(uv, uv),
        float4(1.0, 1.0, FieldDomainKindScreen, FieldShiftKindNone),
        target,
        1.0,
        1.0,
        FieldProposalKindDeterministicStructural);
}

FieldReservoirSample currentFrameReservoirSample(uint2 pixel, float2 uv)
{
    uint width = (uint)max(resolution.x, 1.0);
    uint baseIndex = (pixel.y * width + pixel.x) * FieldReservoirSlotsPerPixel;
    FieldReservoirSample current = sceneFieldReservoirSample(pixel, uv);
    FieldReservoirSample candidate = fieldReservoirCandidates[baseIndex + FieldReservoirRowCurrent];
    current = mergeFieldReservoirVisibilityProposals(
        current,
        candidate,
        fieldReservoirRandom01(pixel, (uint)frameIndex, 31u),
        farDistance);
    current.guide.y = 0.0;
    current.guide.w = 0.0;
    return current;
}

float4 reservoirHistoryColor(FieldReservoirSample sample)
{
    if (!fieldReservoirSampleValid(sample, farDistance))
    {
        return float4(0.0, 0.0, 0.0, farDistance + 1.0);
    }

    return float4(fieldReservoirResolvedColor(sample, farDistance), sample.colorTravel.w);
}

[numthreads(8, 8, 1)]
void D3D12ReservoirHistoryUpdateCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint2 dimensions = (uint2)max(resolution, float2(1.0, 1.0));
    if (dispatchThreadId.x >= dimensions.x || dispatchThreadId.y >= dimensions.y)
    {
        return;
    }

    uint2 pixel = dispatchThreadId.xy;
    float2 uv = (float2(pixel) + 0.5) / float2(dimensions);
    uint baseIndex = (pixel.y * dimensions.x + pixel.x) * FieldReservoirSlotsPerPixel;
    FieldReservoirSample current = currentFrameReservoirSample(pixel, uv);

    reservoirHistoryWrite[baseIndex + FieldReservoirRowCurrent] = current;
    reservoirHistoryWrite[baseIndex + FieldReservoirRowTemporal] = current;
    reservoirHistoryWrite[baseIndex + FieldReservoirRowSpatial] = current;
    reservoirHistoryWrite[baseIndex + FieldReservoirRowFinal] = current;
    reservoirResolvedTexture[pixel] = reservoirHistoryColor(current);
}
