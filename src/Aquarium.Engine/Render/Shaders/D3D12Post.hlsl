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
};

Texture2D<float4> sourceTexture : register(t0);
Texture2D<float4> currentSceneMetadataTexture : register(t5);
Texture2D<float4> currentSceneControlTexture : register(t7);
Texture2D<float4> bloomTexture0 : register(t29);
Texture2D<float4> bloomTexture1 : register(t30);
Texture2D<float4> bloomTexture2 : register(t31);
Texture2D<float4> bloomTexture3 : register(t32);
Texture2D<float4> bloomTexture4 : register(t33);
Texture2D<float4> bloomTexture5 : register(t34);
Texture2D<float4> bloomTexture6 : register(t35);
Texture2D<float4> bloomTexture7 : register(t36);
Texture2D<float4> currentReservoirGuideTexture : register(t26);
Texture2D<float> blueNoiseTexture : register(t28);
SamplerState sourceSampler : register(s0);

#include "D3D12Aces2.hlsl"

struct SdfObject
{
    float4 centerRadius;
    float4 previousCenterPad;
    float4 state;
};

StructuredBuffer<SdfObject> sdfObjects : register(t24);

struct FieldReservoirCandidate
{
    float4 colorTravel;
    float4 metadata;
    float4 control;
    float4 reservoirGuide;
};

StructuredBuffer<FieldReservoirCandidate> fieldReservoirCandidates : register(t45);
StructuredBuffer<FieldReservoirCandidate> reservoirHistoryRead : register(t51);
RWStructuredBuffer<FieldReservoirCandidate> reservoirHistoryWrite : register(u22);
RWTexture2D<float4> reservoirResolvedTexture : register(u23);

static const uint FieldReservoirSlotsPerPixel = 4u;

struct VertexOut
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

struct ResolveOut
{
    float4 finalColor : SV_Target0;
};

struct FieldReservoirResolveOut
{
    float4 colorTravel : SV_Target0;
    float4 metadata : SV_Target1;
    float4 control : SV_Target2;
    float4 reservoirGuide : SV_Target3;
};

static const float FIELD_ID_HEIGHT_FIELD = 4.0;
static const float FIELD_ID_SDF_OBJECT_BASE = 10.0;
static const int AQUARIUM_SDF_OBJECT_CAPACITY = 64;
static const float MAX_HISTORY_AGE = 32.0;
VertexOut FullscreenTriangleVS(uint vertexId : SV_VertexID)
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    VertexOut output;
    output.position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.uv = uv;
    return output;
}

float luminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float3 aces(float3 color)
{
    return saturate(OcioAces2(float4(max(color, 0.0), 1.0)).rgb);
}

float3 debugFieldIdColor(float fieldId)
{
    if (fieldId < 0.5)
    {
        return float3(0.0, 0.0, 0.0);
    }

    if (abs(fieldId - 1.0) < 0.25)
    {
        return float3(0.0, 0.9, 1.0);
    }

    if (abs(fieldId - 2.0) < 0.25)
    {
        return float3(1.0, 0.92, 0.25);
    }

    if (fieldId >= FIELD_ID_SDF_OBJECT_BASE)
    {
        float phase = frac((fieldId - FIELD_ID_SDF_OBJECT_BASE) * 0.37);
        return 0.35 + 0.65 * float3(
            0.5 + 0.5 * sin(phase * 6.28318 + 0.0),
            0.5 + 0.5 * sin(phase * 6.28318 + 2.1),
            0.5 + 0.5 * sin(phase * 6.28318 + 4.2));
    }

    return float3(1.0, 0.0, 1.0);
}

void cameraBasis(float3 camera, float3 target, out float3 forward, out float3 right, out float3 up)
{
    forward = normalize(target - camera);
    float3 worldUp = abs(forward.y) > 0.96 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    right = normalize(cross(worldUp, forward));
    up = normalize(cross(forward, right));
}

float3 rayDirectionForPixel(float2 pixel, float2 jitter, float3 camera, float3 target)
{
    float2 uv = (pixel + jitter) / max(resolution, float2(1.0, 1.0));
    float x = lerp(cameraFrustumXy.x, cameraFrustumXy.y, uv.x);
    float y = lerp(cameraFrustumXy.z, cameraFrustumXy.w, uv.y);
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(camera, target, forward, right, up);
    return normalize(forward + right * x + up * y);
}

float3 temporalPreviousWorldPosition(float3 worldPosition, float fieldId)
{
    if (fieldId >= FIELD_ID_SDF_OBJECT_BASE)
    {
        int sdfIndex = clamp((int)round(fieldId - FIELD_ID_SDF_OBJECT_BASE), 0, AQUARIUM_SDF_OBJECT_CAPACITY - 1);
        float3 currentCenter = sdfObjects[sdfIndex].centerRadius.xyz;
        float3 previousCenter = sdfObjects[sdfIndex].previousCenterPad.xyz;
        return previousCenter + (worldPosition - currentCenter);
    }

    return worldPosition;
}

float2 projectWorldToPreviousHistoryUv(float3 worldPosition)
{
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(previousCameraPosition, previousCameraTarget, forward, right, up);
    float3 delta = worldPosition - previousCameraPosition;
    float z = max(dot(delta, forward), 0.0001);
    float2 frustumMin = float2(cameraFrustumXy.x, cameraFrustumXy.z);
    float2 frustumMax = float2(cameraFrustumXy.y, cameraFrustumXy.w);
    float2 slope = float2(dot(delta, right), dot(delta, up)) / z;
    float2 ndc = ((slope - frustumMin) / max(frustumMax - frustumMin, float2(0.0001, 0.0001))) * 2.0 - 1.0;
    float2 pixel = (ndc * resolution + resolution) * 0.5 - previousJitterPixels;
    return float2(pixel.x / resolution.x, 1.0 - pixel.y / resolution.y);
}

int2 pixelFromUv(float2 uv)
{
    return clamp((int2)floor(uv * resolution), int2(0, 0), (int2)resolution - int2(1, 1));
}

float4 loadCurrentMetadata(float2 uv)
{
    return currentSceneMetadataTexture.Load(int3(pixelFromUv(uv), 0));
}

float4 loadCurrentControl(float2 uv)
{
    return currentSceneControlTexture.Load(int3(pixelFromUv(uv), 0));
}

FieldReservoirCandidate emptyFieldReservoirCandidate()
{
    FieldReservoirCandidate candidate;
    candidate.colorTravel = float4(0.0, 0.0, 0.0, farDistance + 1.0);
    candidate.metadata = 0.0;
    candidate.control = 0.0;
    candidate.reservoirGuide = float4(1.0, 0.0, 1.0, 0.0);
    return candidate;
}

float4 loadCurrentReservoirGuide(float2 uv)
{
    return currentReservoirGuideTexture.Load(int3(pixelFromUv(uv), 0));
}

void currentNeighborhood(float2 uv, out float3 neighborhoodMin, out float3 neighborhoodMax)
{
    float2 texel = 1.0 / resolution;
    neighborhoodMin = 100000.0;
    neighborhoodMax = -100000.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float3 sampleColor = sourceTexture.SampleLevel(sourceSampler, uv + float2(x, y) * texel, 0.0).rgb;
            neighborhoodMin = min(neighborhoodMin, sampleColor);
            neighborhoodMax = max(neighborhoodMax, sampleColor);
        }
    }
}

float3 bloomColorAt(float2 uv)
{
    // Sonic Ether-style octave bloom: normalize weighted mip octaves so scatter shape changes
    // the radius of bloom without silently becoming an exposure control.
    const float scatterPower = 1.25;
    float weights[8] =
    {
        pow(1.0, scatterPower),
        pow(2.0, scatterPower),
        pow(3.0, scatterPower),
        pow(4.0, scatterPower),
        pow(5.0, scatterPower),
        pow(6.0, scatterPower),
        pow(7.0, scatterPower),
        pow(8.0, scatterPower)
    };
    float inverseWeightSum = rcp(
        weights[0] + weights[1] + weights[2] + weights[3] +
        weights[4] + weights[5] + weights[6] + weights[7]);
    float3 bloom =
        bloomTexture0.SampleLevel(sourceSampler, uv, 0.0).rgb * weights[0] +
        bloomTexture1.SampleLevel(sourceSampler, uv, 0.0).rgb * weights[1] +
        bloomTexture2.SampleLevel(sourceSampler, uv, 0.0).rgb * weights[2] +
        bloomTexture3.SampleLevel(sourceSampler, uv, 0.0).rgb * weights[3] +
        bloomTexture4.SampleLevel(sourceSampler, uv, 0.0).rgb * weights[4] +
        bloomTexture5.SampleLevel(sourceSampler, uv, 0.0).rgb * weights[5] +
        bloomTexture6.SampleLevel(sourceSampler, uv, 0.0).rgb * weights[6] +
        bloomTexture7.SampleLevel(sourceSampler, uv, 0.0).rgb * weights[7];
    return bloom * inverseWeightSum;
}

float3 presentColor(float3 scene, float2 uv, float bloomScale)
{
    float3 bloom = bloomColorAt(uv) * bloomScale;
    float3 exposedScene = scene * max(exposure, 0.001);
    float corePreservation = 1.0 - smoothstep(0.7, 2.4, luminance(exposedScene));
    float3 bloomContribution = bloom * (bloomIntensity + bloomVeilIntensity) * corePreservation;
    return aces(exposedScene + bloomContribution);
}

float blueNoiseAt(float2 uv, uint salt)
{
    uint width;
    uint height;
    blueNoiseTexture.GetDimensions(width, height);
    uint2 dimensions = max(uint2(width, height), uint2(1, 1));
    uint2 pixel = (uint2)floor(saturate(uv) * resolution);
    uint2 offset = uint2((uint)frameIndex * 19u + salt * 53u, (uint)frameIndex * 31u + salt * 97u);
    return blueNoiseTexture.Load(int3((pixel + offset) % dimensions, 0));
}

float3 ditherDisplay(float3 color, float2 uv)
{
    float noise = blueNoiseAt(uv, 11u) - 0.5;
    return saturate(color + noise / 255.0);
}

float3 bloomBrightPass(float3 exposedColor)
{
    float luma = luminance(exposedColor);
    const float threshold = 1.0;
    const float knee = 0.35;
    float soft = saturate((luma - threshold + knee) / max(2.0 * knee, 0.0001));
    float contribution = max(luma - threshold, 0.0) + soft * soft * knee;
    return exposedColor * saturate(contribution / max(luma, 0.0001));
}

float3 clampBloomFirefly(float2 uv, float3 centerColor)
{
    float2 texel = 1.0 / resolution;
    float centerLuma = luminance(centerColor);
    float neighborMaxLuma = 0.0;
    float neighborSumLuma = 0.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            if (x == 0 && y == 0)
            {
                continue;
            }

            float3 sampleColor = sourceTexture.SampleLevel(sourceSampler, uv + float2(x, y) * texel, 0.0).rgb;
            float sampleLuma = luminance(sampleColor);
            neighborMaxLuma = max(neighborMaxLuma, sampleLuma);
            neighborSumLuma += sampleLuma;
        }
    }

    float neighborAvgLuma = neighborSumLuma * 0.125;
    float supportedLuma = max(neighborMaxLuma * 1.55, neighborAvgLuma * 2.75) + 1.25;
    float spike = smoothstep(supportedLuma, supportedLuma * 2.5 + 2.0, centerLuma);
    float clampedLuma = lerp(centerLuma, min(centerLuma, supportedLuma), spike);
    return centerColor * (clampedLuma / max(centerLuma, 0.0001));
}

float4 D3D12BloomPrefilterPS(VertexOut input) : SV_Target0
{
    float3 color = sourceTexture.SampleLevel(sourceSampler, input.uv, 0.0).rgb * max(exposure, 0.001);
    return float4(bloomBrightPass(clampBloomFirefly(input.uv, color)), 1.0);
}

float4 D3D12BloomDownsamplePS(VertexOut input) : SV_Target0
{
    float3 sumColor = 0.0;
    float sumWeight = 0.0;
    uint sourceWidth;
    uint sourceHeight;
    sourceTexture.GetDimensions(sourceWidth, sourceHeight);
    float2 texel = 1.0 / float2(max(sourceWidth, 1), max(sourceHeight, 1));

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float3 sampleColor = sourceTexture.SampleLevel(sourceSampler, input.uv + float2(x, y) * texel, 0.0).rgb;
            float weight = rcp(1.0 + luminance(sampleColor) * 0.12);
            sumColor += sampleColor * weight;
            sumWeight += weight;
        }
    }

    return float4(sumColor / max(sumWeight, 0.0001), 1.0);
}

float4 D3D12BloomBlurHorizontalPS(VertexOut input) : SV_Target0
{
    uint sourceWidth;
    uint sourceHeight;
    sourceTexture.GetDimensions(sourceWidth, sourceHeight);
    float2 texel = float2(1.0 / max(sourceWidth, 1), 0.0);
    float3 color =
        sourceTexture.SampleLevel(sourceSampler, input.uv - texel * 2.0, 0.0).rgb * 0.06136 +
        sourceTexture.SampleLevel(sourceSampler, input.uv - texel, 0.0).rgb * 0.24477 +
        sourceTexture.SampleLevel(sourceSampler, input.uv, 0.0).rgb * 0.38774 +
        sourceTexture.SampleLevel(sourceSampler, input.uv + texel, 0.0).rgb * 0.24477 +
        sourceTexture.SampleLevel(sourceSampler, input.uv + texel * 2.0, 0.0).rgb * 0.06136;
    return float4(color, 1.0);
}

float4 D3D12BloomBlurVerticalPS(VertexOut input) : SV_Target0
{
    uint sourceWidth;
    uint sourceHeight;
    sourceTexture.GetDimensions(sourceWidth, sourceHeight);
    float2 texel = float2(0.0, 1.0 / max(sourceHeight, 1));
    float3 color =
        sourceTexture.SampleLevel(sourceSampler, input.uv - texel * 2.0, 0.0).rgb * 0.06136 +
        sourceTexture.SampleLevel(sourceSampler, input.uv - texel, 0.0).rgb * 0.24477 +
        sourceTexture.SampleLevel(sourceSampler, input.uv, 0.0).rgb * 0.38774 +
        sourceTexture.SampleLevel(sourceSampler, input.uv + texel, 0.0).rgb * 0.24477 +
        sourceTexture.SampleLevel(sourceSampler, input.uv + texel * 2.0, 0.0).rgb * 0.06136;
    return float4(color, 1.0);
}

bool fieldReservoirCandidateValid(FieldReservoirCandidate candidate)
{
    return candidate.metadata.x > 0.5 &&
        candidate.colorTravel.w > 0.0 &&
        candidate.colorTravel.w <= farDistance &&
        saturate(candidate.control.x) > 0.0 &&
        saturate(candidate.reservoirGuide.z) > 0.0;
}

FieldReservoirCandidate sceneFieldReservoirCandidate(float2 uv)
{
    FieldReservoirCandidate candidate;
    candidate.colorTravel = sourceTexture.SampleLevel(sourceSampler, uv, 0.0);
    candidate.metadata = loadCurrentMetadata(uv);
    candidate.control = loadCurrentControl(uv);
    candidate.reservoirGuide = loadCurrentReservoirGuide(uv);
    return candidate;
}

float reservoirCandidateConfidence(FieldReservoirCandidate candidate)
{
    return candidate.reservoirGuide.x > 0.0
        ? saturate(candidate.reservoirGuide.x)
        : (candidate.control.w > 0.0 ? saturate(candidate.control.w) : 1.0);
}

float reservoirCandidateDomainValidity(FieldReservoirCandidate candidate)
{
    return candidate.reservoirGuide.z > 0.0 ? saturate(candidate.reservoirGuide.z) : 1.0;
}

float reservoirHistoryValidationWeight(
    FieldReservoirCandidate currentCandidate,
    FieldReservoirCandidate previousCandidate,
    float expectedPreviousTravel,
    float3 neighborhoodMin,
    float3 neighborhoodMax,
    out float3 clampedHistory)
{
    clampedHistory = clamp(previousCandidate.colorTravel.rgb, neighborhoodMin, neighborhoodMax);
    if (!fieldReservoirCandidateValid(currentCandidate) ||
        !fieldReservoirCandidateValid(previousCandidate))
    {
        return 0.0;
    }

    float travelDelta = abs(previousCandidate.colorTravel.w - expectedPreviousTravel);
    float travelTolerance = max(0.045, expectedPreviousTravel * 0.018);
    float travelWeight = 1.0 - smoothstep(travelTolerance, travelTolerance * 4.0, travelDelta);
    float fieldWeight = abs(previousCandidate.metadata.x - currentCandidate.metadata.x) < 0.001 ? 1.0 : 0.0;
    float normalWeight = 0.0;
    if (dot(previousCandidate.metadata.yzw, previousCandidate.metadata.yzw) > 0.01 &&
        dot(currentCandidate.metadata.yzw, currentCandidate.metadata.yzw) > 0.01)
    {
        normalWeight = smoothstep(0.68, 0.96, dot(normalize(previousCandidate.metadata.yzw), normalize(currentCandidate.metadata.yzw)));
    }

    float colorDelta = length(clampedHistory - currentCandidate.colorTravel.rgb);
    float colorWeight = 1.0 - smoothstep(0.18, 1.2, colorDelta);
    float currentLuma = luminance(currentCandidate.colorTravel.rgb);
    float historyLuma = luminance(clampedHistory);
    float hotSdfCurrent = currentCandidate.metadata.x >= FIELD_ID_SDF_OBJECT_BASE ? smoothstep(1.0, 5.0, currentLuma - historyLuma) : 0.0;
    colorWeight = max(colorWeight, hotSdfCurrent * 0.82);
    float coverageWeight = smoothstep(0.02, 0.55, saturate(currentCandidate.control.x));
    float coverageContinuityWeight = 1.0 - smoothstep(0.10, 0.50, abs(saturate(previousCandidate.control.x) - saturate(currentCandidate.control.x)));
    float temporalDetailWeight = 1.0 - smoothstep(0.08, 0.45, abs(saturate(previousCandidate.control.z) - saturate(currentCandidate.control.z)));
    temporalDetailWeight = max(temporalDetailWeight, 1.0 - smoothstep(0.02, 0.12, max(saturate(previousCandidate.control.z), saturate(currentCandidate.control.z))));
    float reservoirConfidenceWeight = lerp(0.45, 1.0, min(reservoirCandidateConfidence(currentCandidate), reservoirCandidateConfidence(previousCandidate)));
    float reservoirDomainWeight = reservoirCandidateDomainValidity(currentCandidate) * reservoirCandidateDomainValidity(previousCandidate);
    return travelWeight * colorWeight * fieldWeight * normalWeight * coverageWeight * coverageContinuityWeight * temporalDetailWeight * reservoirConfidenceWeight * reservoirDomainWeight;
}

FieldReservoirCandidate resolveReservoirHistoryCandidate(
    FieldReservoirCandidate currentCandidate,
    float2 pixel,
    float2 uv,
    out float historyWeight,
    out float historyAge)
{
    historyWeight = 0.0;
    historyAge = 0.0;
    if (!fieldReservoirCandidateValid(currentCandidate))
    {
        return emptyFieldReservoirCandidate();
    }

    float reservoirSampleAge = max(currentCandidate.reservoirGuide.y, 0.0);
    float reservoirInvalidationCode = 0.0;
    float3 historyColor = currentCandidate.colorTravel.rgb;
    if (frameIndex > 0.5 && currentCandidate.colorTravel.w <= farDistance && currentCandidate.metadata.x > 0.5)
    {
        float3 currentRay = rayDirectionForPixel(pixel, jitterPixels, cameraPosition, cameraTarget);
        float3 worldPosition = cameraPosition + currentRay * currentCandidate.colorTravel.w;
        float3 previousWorldPosition = temporalPreviousWorldPosition(worldPosition, currentCandidate.metadata.x);
        float2 previousUv = projectWorldToPreviousHistoryUv(previousWorldPosition);

        if (all(previousUv >= 0.0) && all(previousUv <= 1.0))
        {
            uint2 dimensions = (uint2)max(resolution, float2(1.0, 1.0));
            uint2 previousPixel = min((uint2)pixelFromUv(previousUv), dimensions - 1u);
            uint width = dimensions.x;
            uint previousBaseIndex = (previousPixel.y * width + previousPixel.x) * FieldReservoirSlotsPerPixel;
            float expectedPreviousTravel = distance(previousCameraPosition, previousWorldPosition);
            float3 neighborhoodMin;
            float3 neighborhoodMax;
            currentNeighborhood(uv, neighborhoodMin, neighborhoodMax);
            float bestValidationWeight = 0.0;
            float bestPreviousHistoryAge = 0.0;
            float bestPreviousReservoirSampleAge = 0.0;
            float3 bestHistoryColor = currentCandidate.colorTravel.rgb;

            [unroll]
            for (uint slot = 0u; slot < FieldReservoirSlotsPerPixel; slot++)
            {
                FieldReservoirCandidate previousCandidate = reservoirHistoryRead[previousBaseIndex + slot];
                float3 clampedHistory;
                float validationWeight = reservoirHistoryValidationWeight(
                    currentCandidate,
                    previousCandidate,
                    expectedPreviousTravel,
                    neighborhoodMin,
                    neighborhoodMax,
                    clampedHistory);
                if (validationWeight > bestValidationWeight)
                {
                    bestValidationWeight = validationWeight;
                    bestPreviousHistoryAge = max(previousCandidate.control.w, 0.0);
                    bestPreviousReservoirSampleAge = max(previousCandidate.reservoirGuide.y, 0.0);
                    bestHistoryColor = clampedHistory;
                }
            }

            if (bestValidationWeight > 0.0)
            {
                float historyConfidence = smoothstep(0.0, 6.0, bestPreviousHistoryAge);
                historyColor = bestHistoryColor;
                historyWeight = 0.82 * lerp(0.35, 1.0, historyConfidence) * bestValidationWeight;
                historyAge = bestValidationWeight > 0.01 ? min(bestPreviousHistoryAge + 1.0, MAX_HISTORY_AGE) : 0.0;
                reservoirSampleAge = bestValidationWeight > 0.01 ? min(max(reservoirSampleAge, bestPreviousReservoirSampleAge + 1.0), MAX_HISTORY_AGE) : reservoirSampleAge;
                reservoirInvalidationCode = bestValidationWeight > 0.01 ? 0.0 : 1.0;
            }
            else
            {
                reservoirInvalidationCode = 1.0;
            }
        }
    }

    FieldReservoirCandidate resolved = currentCandidate;
    resolved.colorTravel = float4(lerp(currentCandidate.colorTravel.rgb, historyColor, historyWeight), currentCandidate.colorTravel.w);
    resolved.control = float4(currentCandidate.control.xyz, historyAge);
    resolved.reservoirGuide = float4(
        reservoirCandidateConfidence(currentCandidate),
        reservoirSampleAge,
        reservoirCandidateDomainValidity(currentCandidate),
        reservoirInvalidationCode);
    return resolved;
}

float fieldReservoirCandidatePriority(float4 colorTravel, float4 metadata, float4 control, float4 reservoirGuide)
{
    if (metadata.x <= 0.5 ||
        colorTravel.w <= 0.0 ||
        colorTravel.w > farDistance ||
        saturate(control.x) <= 0.0 ||
        saturate(reservoirGuide.z) <= 0.0)
    {
        return 1.0e20;
    }

    return colorTravel.w - saturate(control.x) * 0.025;
}

void acceptFieldReservoirCandidate(
    FieldReservoirCandidate candidate,
    inout float4 bestColorTravel,
    inout float4 bestMetadata,
    inout float4 bestControl,
    inout float4 bestReservoirGuide,
    inout float bestPriority)
{
    if (!fieldReservoirCandidateValid(candidate))
    {
        return;
    }

    float priority = fieldReservoirCandidatePriority(
        candidate.colorTravel,
        candidate.metadata,
        candidate.control,
        candidate.reservoirGuide);
    if (priority < bestPriority)
    {
        bestColorTravel = candidate.colorTravel;
        bestMetadata = candidate.metadata;
        bestControl = candidate.control;
        bestReservoirGuide = candidate.reservoirGuide;
        bestPriority = priority;
    }
}

FieldReservoirCandidate carryFieldReservoirHistoryCandidate(FieldReservoirCandidate previousCandidate)
{
    if (!fieldReservoirCandidateValid(previousCandidate) || previousCandidate.control.w >= MAX_HISTORY_AGE)
    {
        return emptyFieldReservoirCandidate();
    }

    FieldReservoirCandidate carried = previousCandidate;
    carried.control.w = min(previousCandidate.control.w + 1.0, MAX_HISTORY_AGE);
    carried.reservoirGuide.y = min(max(previousCandidate.reservoirGuide.y, 0.0) + 1.0, MAX_HISTORY_AGE);
    carried.reservoirGuide.w = 0.0;
    return carried;
}

FieldReservoirResolveOut D3D12FieldReservoirResolvePS(VertexOut input)
{
    uint2 pixel = (uint2)pixelFromUv(input.uv);
    uint width = (uint)max(resolution.x, 1.0);
    uint pixelIndex = pixel.y * width + pixel.x;
    float4 bestColorTravel = sourceTexture.Load(int3(pixel, 0));
    float4 bestMetadata = loadCurrentMetadata(input.uv);
    float4 bestControl = loadCurrentControl(input.uv);
    float4 bestReservoirGuide = loadCurrentReservoirGuide(input.uv);
    float bestPriority = fieldReservoirCandidatePriority(
        bestColorTravel,
        bestMetadata,
        bestControl,
        bestReservoirGuide);

    uint baseIndex = pixelIndex * FieldReservoirSlotsPerPixel;
    [unroll]
    for (uint slot = 0u; slot < FieldReservoirSlotsPerPixel; slot++)
    {
        acceptFieldReservoirCandidate(
            fieldReservoirCandidates[baseIndex + slot],
            bestColorTravel,
            bestMetadata,
            bestControl,
            bestReservoirGuide,
            bestPriority);
    }

    if (bestPriority >= 1.0e19)
    {
        bestColorTravel = float4(0.0, 0.0, 0.0, farDistance + 1.0);
        bestMetadata = 0.0;
        bestControl = 0.0;
        bestReservoirGuide = float4(1.0, 0.0, 1.0, 0.0);
    }

    FieldReservoirResolveOut output;
    output.colorTravel = bestColorTravel;
    output.metadata = bestMetadata;
    output.control = bestControl;
    output.reservoirGuide = bestReservoirGuide;
    return output;
}

[numthreads(8, 8, 1)]
void D3D12ReservoirHistoryUpdateCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint2 dimensions = (uint2)max(resolution, float2(1.0, 1.0));
    if (dispatchThreadId.x >= dimensions.x || dispatchThreadId.y >= dimensions.y)
    {
        return;
    }

    uint2 currentPixel = dispatchThreadId.xy;
    float2 uv = (float2(currentPixel) + 0.5) / float2(dimensions);
    float2 pixel = float2(currentPixel);
    uint pixelIndex = currentPixel.y * dimensions.x + currentPixel.x;
    uint baseIndex = pixelIndex * FieldReservoirSlotsPerPixel;
    FieldReservoirCandidate sceneCandidate = sceneFieldReservoirCandidate(uv);
    FieldReservoirCandidate bestResolved = emptyFieldReservoirCandidate();
    float bestPriority = 1.0e20;
    bool hasSharedCandidate = false;

    [unroll]
    for (uint slot = 0u; slot < FieldReservoirSlotsPerPixel; slot++)
    {
        hasSharedCandidate = hasSharedCandidate || fieldReservoirCandidateValid(fieldReservoirCandidates[baseIndex + slot]);
    }

    if (!hasSharedCandidate && !fieldReservoirCandidateValid(sceneCandidate))
    {
        [unroll]
        for (uint slot = 0u; slot < FieldReservoirSlotsPerPixel; slot++)
        {
            reservoirHistoryWrite[baseIndex + slot] = emptyFieldReservoirCandidate();
        }

        reservoirResolvedTexture[currentPixel] = sceneCandidate.colorTravel;
        return;
    }

    [unroll]
    for (uint slot = 0u; slot < FieldReservoirSlotsPerPixel; slot++)
    {
        FieldReservoirCandidate currentCandidate = fieldReservoirCandidates[baseIndex + slot];
        if (!hasSharedCandidate && slot == 0u)
        {
            currentCandidate = sceneCandidate;
        }

        float slotHistoryWeight;
        float slotHistoryAge;
        FieldReservoirCandidate resolvedCandidate = resolveReservoirHistoryCandidate(
            currentCandidate,
            pixel,
            uv,
            slotHistoryWeight,
            slotHistoryAge);
        reservoirHistoryWrite[baseIndex + slot] = resolvedCandidate;
        float priority = fieldReservoirCandidatePriority(
            resolvedCandidate.colorTravel,
            resolvedCandidate.metadata,
            resolvedCandidate.control,
            resolvedCandidate.reservoirGuide);
        if (priority < bestPriority)
        {
            bestResolved = resolvedCandidate;
            bestPriority = priority;
        }
    }

    if (bestPriority >= 1.0e19)
    {
        bestResolved = sceneCandidate;
    }

    reservoirResolvedTexture[currentPixel] = bestResolved.colorTravel;
}

ResolveOut D3D12ReservoirPresentationResolvePS(VertexOut input)
{
    float4 resolvedSample = sourceTexture.SampleLevel(sourceSampler, input.uv, 0.0);
    float4 currentMetadata = loadCurrentMetadata(input.uv);
    float4 currentControl = loadCurrentControl(input.uv);
    float4 currentReservoirGuide = loadCurrentReservoirGuide(input.uv);
    float currentCoverage = saturate(currentControl.x);
    float currentStepRatio = saturate(currentControl.y);
    float currentTemporalDetail = saturate(currentControl.z);
    float currentFieldId = currentMetadata.x;
    float currentReservoirConfidence = currentReservoirGuide.x > 0.0 ? saturate(currentReservoirGuide.x) : 1.0;
    float reservoirSampleAge = max(currentReservoirGuide.y, 0.0);
    float currentReservoirDomainValidity = currentReservoirGuide.z > 0.0 ? saturate(currentReservoirGuide.z) : 1.0;
    float3 resolved = resolvedSample.rgb;
    float3 finalColor = presentColor(resolved, input.uv, 1.0);
    if (renderDebugMode > 0.5 && renderDebugMode < 1.5)
    {
        finalColor = aces(resolved * max(exposure, 0.001));
    }
    else if (renderDebugMode >= 1.5 && renderDebugMode < 2.5)
    {
        finalColor = aces(resolved * max(exposure, 0.001));
    }
    else if (renderDebugMode >= 2.5 && renderDebugMode < 3.5)
    {
        finalColor = saturate(reservoirSampleAge / MAX_HISTORY_AGE).xxx;
    }
    else if (renderDebugMode >= 3.5 && renderDebugMode < 4.5)
    {
        finalColor = currentReservoirConfidence.xxx;
    }
    else if (renderDebugMode >= 4.5 && renderDebugMode < 5.5)
    {
        finalColor = saturate(float3(currentCoverage, currentStepRatio, currentTemporalDetail));
    }
    else if (renderDebugMode >= 5.5 && renderDebugMode < 6.5)
    {
        finalColor = debugFieldIdColor(currentFieldId);
    }
    else if (renderDebugMode >= 6.5 && renderDebugMode < 7.5)
    {
        float3 bloom = bloomColorAt(input.uv);
        finalColor = aces(bloom * bloomIntensity + bloom * bloomVeilIntensity);
    }
    else if (renderDebugMode >= 7.5 && renderDebugMode < 8.5)
    {
        float luma = luminance(resolved * max(exposure, 0.001));
        finalColor = luma.xxx;
    }
    else if (renderDebugMode >= 8.5 && renderDebugMode < 9.5)
    {
        finalColor = currentFieldId >= FIELD_ID_SDF_OBJECT_BASE ? debugFieldIdColor(currentFieldId) : 0.0;
    }
    else if (renderDebugMode >= 9.5 && renderDebugMode < 10.5)
    {
        finalColor = currentStepRatio.xxx;
    }
    else if (renderDebugMode >= 11.5 && renderDebugMode < 12.5)
    {
        finalColor = float3(currentReservoirConfidence, saturate(reservoirSampleAge / MAX_HISTORY_AGE), currentReservoirDomainValidity);
    }
    ResolveOut output;
    output.finalColor = float4(ditherDisplay(finalColor, input.uv), 1.0);
    return output;
}
