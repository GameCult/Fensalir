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
#include "D3D12FieldReservoir.hlsli"

struct SdfObject
{
    float4 centerRadius;
    float4 previousCenterPad;
    float4 state;
};

StructuredBuffer<SdfObject> sdfObjects : register(t24);

struct TubeFieldReplayManifestEntry
{
    float4 shape;
    float4 columns;
    float4 amplitude;
    float4 material;
    float4 dispatch;
    float4 draw;
    float4 origin;
    float4 axisStep;
    float4 columnStep;
    float4 source;
};

StructuredBuffer<TubeFieldReplayManifestEntry> tubeFieldReplayManifest : register(t59);
ByteAddressBuffer tubeFieldReplaySource0 : register(t60);
ByteAddressBuffer tubeFieldReplaySource1 : register(t61);
ByteAddressBuffer tubeFieldReplaySource2 : register(t62);
ByteAddressBuffer tubeFieldReplaySource3 : register(t63);
ByteAddressBuffer tubeFieldReplaySource4 : register(t64);
ByteAddressBuffer tubeFieldReplaySource5 : register(t65);
ByteAddressBuffer tubeFieldReplaySource6 : register(t66);
ByteAddressBuffer tubeFieldReplaySource7 : register(t67);
ByteAddressBuffer tubeFieldReplaySource8 : register(t68);
ByteAddressBuffer tubeFieldReplaySource9 : register(t69);
ByteAddressBuffer tubeFieldReplaySource10 : register(t70);
ByteAddressBuffer tubeFieldReplaySource11 : register(t71);
ByteAddressBuffer tubeFieldReplaySource12 : register(t72);
ByteAddressBuffer tubeFieldReplaySource13 : register(t73);
ByteAddressBuffer tubeFieldReplaySource14 : register(t74);
ByteAddressBuffer tubeFieldReplaySource15 : register(t75);

StructuredBuffer<FieldReservoirSample> fieldReservoirCandidates : register(t45);
StructuredBuffer<FieldReservoirSample> reservoirHistoryRead : register(t51);
RWStructuredBuffer<FieldReservoirSample> reservoirHistoryWrite : register(u22);
RWTexture2D<float4> reservoirResolvedTexture : register(u23);

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
static const float FIELD_ID_TUBE_FIELD_BASE = 5100.0;
static const float FIELD_ID_TUBE_FIELD_MAX = 10000.0;
static const int AQUARIUM_SDF_OBJECT_CAPACITY = 64;
static const float MAX_HISTORY_AGE = 32.0;

bool nativeDomainReservoirEnabled()
{
    return cameraFrustumZ.z < 0.5;
}

float fieldReservoirSpatialReuseBudget()
{
    return clamp(reservoirBudgetInfo.x, 0.25, 1.0);
}

bool fieldReservoirShouldSpendSpatialReuse(uint2 pixel)
{
    float budget = fieldReservoirSpatialReuseBudget();
    uint activePhases = clamp((uint)floor(budget * 4.0 + 0.5), 1u, 4u);
    if (activePhases >= 4u)
    {
        return true;
    }

    uint tilePhase = (pixel.x & 1u) | ((pixel.y & 1u) << 1);
    uint phaseStart = (uint)frameIndex & 3u;
    uint phaseDistance = (tilePhase + 4u - phaseStart) & 3u;
    return phaseDistance < activePhases;
}

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

uint tubeReplayPositiveModulo(int value, uint modulo)
{
    int m = (int)max(modulo, 1u);
    int r = value % m;
    return (uint)(r < 0 ? r + m : r);
}

uint tubeReplaySampleAddressWithOffset(TubeFieldReplayManifestEntry replay, uint logicalColumn, uint sampleIndex, int rollingOffset)
{
    uint width = max((uint)round(replay.shape.x), 1u);
    uint height = max((uint)round(replay.shape.y), 1u);
    uint firstColumn = (uint)max(round(replay.shape.w), 0.0);
    uint columnStride = max((uint)round(replay.columns.y), 1u);
    uint rollingModulo = (uint)max(round(replay.columns.z), 0.0);
    uint physicalColumn = firstColumn + logicalColumn * columnStride;
    if (rollingModulo > 0u)
    {
        physicalColumn = tubeReplayPositiveModulo((int)physicalColumn + rollingOffset, rollingModulo);
    }

    physicalColumn = min(physicalColumn, height - 1u);
    uint strideBytes = max((uint)round(replay.shape.z), 4u);
    return (physicalColumn * width + min(sampleIndex, width - 1u)) * strideBytes;
}

float tubeReplayLoad(uint sourceIndex, uint address)
{
    if (sourceIndex == 0u) return asfloat(tubeFieldReplaySource0.Load(address));
    if (sourceIndex == 1u) return asfloat(tubeFieldReplaySource1.Load(address));
    if (sourceIndex == 2u) return asfloat(tubeFieldReplaySource2.Load(address));
    if (sourceIndex == 3u) return asfloat(tubeFieldReplaySource3.Load(address));
    if (sourceIndex == 4u) return asfloat(tubeFieldReplaySource4.Load(address));
    if (sourceIndex == 5u) return asfloat(tubeFieldReplaySource5.Load(address));
    if (sourceIndex == 6u) return asfloat(tubeFieldReplaySource6.Load(address));
    if (sourceIndex == 7u) return asfloat(tubeFieldReplaySource7.Load(address));
    if (sourceIndex == 8u) return asfloat(tubeFieldReplaySource8.Load(address));
    if (sourceIndex == 9u) return asfloat(tubeFieldReplaySource9.Load(address));
    if (sourceIndex == 10u) return asfloat(tubeFieldReplaySource10.Load(address));
    if (sourceIndex == 11u) return asfloat(tubeFieldReplaySource11.Load(address));
    if (sourceIndex == 12u) return asfloat(tubeFieldReplaySource12.Load(address));
    if (sourceIndex == 13u) return asfloat(tubeFieldReplaySource13.Load(address));
    if (sourceIndex == 14u) return asfloat(tubeFieldReplaySource14.Load(address));
    return asfloat(tubeFieldReplaySource15.Load(address));
}

float tubeReplayRawSampleWithOffset(TubeFieldReplayManifestEntry replay, uint logicalColumn, int sampleIndex, int rollingOffset)
{
    uint width = max((uint)round(replay.shape.x), 1u);
    uint clamped = (uint)clamp(sampleIndex, 0, (int)width - 1);
    uint sourceIndex = min((uint)max(round(replay.source.x), 0.0), 15u);
    return tubeReplayLoad(sourceIndex, tubeReplaySampleAddressWithOffset(replay, logicalColumn, clamped, rollingOffset));
}

float tubeReplayFilteredSampleWithOffset(TubeFieldReplayManifestEntry replay, uint logicalColumn, float x, int rollingOffset)
{
    int center = (int)floor(x + 0.5);
    float s0 = tubeReplayRawSampleWithOffset(replay, logicalColumn, center - 2, rollingOffset);
    float s1 = tubeReplayRawSampleWithOffset(replay, logicalColumn, center - 1, rollingOffset);
    float s2 = tubeReplayRawSampleWithOffset(replay, logicalColumn, center, rollingOffset);
    float s3 = tubeReplayRawSampleWithOffset(replay, logicalColumn, center + 1, rollingOffset);
    float s4 = tubeReplayRawSampleWithOffset(replay, logicalColumn, center + 2, rollingOffset);
    return (s0 + s4 + 4.0 * (s1 + s3) + 6.0 * s2) / 16.0;
}

float tubeReplayCatmull(float p0, float p1, float p2, float p3, float t)
{
    float t2 = t * t;
    float t3 = t2 * t;
    return 0.5 * ((2.0 * p1) +
        (-p0 + p2) * t +
        (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 +
        (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3);
}

float tubeReplayNormalizedSampleWithOffset(TubeFieldReplayManifestEntry replay, uint logicalColumn, float x, int rollingOffset)
{
    float value = tubeReplayFilteredSampleWithOffset(replay, logicalColumn, x, rollingOffset);
    return saturate((value - replay.amplitude.z) / max(replay.amplitude.w - replay.amplitude.z, 0.0001));
}

float tubeReplaySampleCurve(TubeFieldReplayManifestEntry replay, uint logicalColumn, float x)
{
    int i1 = (int)floor(x);
    float t = frac(x);
    int rollingOffset = (int)round(replay.columns.w);
    float p0 = tubeReplayNormalizedSampleWithOffset(replay, logicalColumn, (float)(i1 - 1), rollingOffset);
    float p1 = tubeReplayNormalizedSampleWithOffset(replay, logicalColumn, (float)i1, rollingOffset);
    float p2 = tubeReplayNormalizedSampleWithOffset(replay, logicalColumn, (float)(i1 + 1), rollingOffset);
    float p3 = tubeReplayNormalizedSampleWithOffset(replay, logicalColumn, (float)(i1 + 2), rollingOffset);
    return saturate(tubeReplayCatmull(p0, p1, p2, p3, t));
}

bool tubeFieldReplaySampleMatches(TubeFieldReplayManifestEntry replay, FieldReservoirSample sample)
{
    return replay.source.y > 0.5 &&
        replay.shape.x > 0.5 &&
        sample.domainSupport.z > FieldDomainKindTube - 0.25 &&
        sample.domainSupport.z < FieldDomainKindTube + 0.25 &&
        sample.metadata.x >= replay.draw.z &&
        sample.metadata.x < replay.draw.z + 50.0;
}

bool tubeFieldReplayEntryFor(FieldReservoirSample currentSample, FieldReservoirSample previousSample, out TubeFieldReplayManifestEntry replay)
{
    [loop]
    for (int index = 0; index < 16; index++)
    {
        TubeFieldReplayManifestEntry candidate = tubeFieldReplayManifest[index];
        if (tubeFieldReplaySampleMatches(candidate, currentSample) &&
            tubeFieldReplaySampleMatches(candidate, previousSample))
        {
            replay = candidate;
            return true;
        }
    }

    replay = tubeFieldReplayManifest[0];
    return false;
}

float tubeFieldReplayValidationWeight(FieldReservoirSample currentSample, FieldReservoirSample previousSample, uint2 pixel)
{
    TubeFieldReplayManifestEntry replay;
    if (!tubeFieldReplayEntryFor(currentSample, previousSample, replay))
    {
        return 0.0;
    }

    uint columnCount = max((uint)round(replay.columns.x), 1u);
    uint logicalColumn = min((uint)max(round(previousSample.domainSample.z), 0.0), columnCount - 1u);
    float sampleX = clamp(previousSample.domainSample.w, 0.0, max(replay.shape.x - 1.0, 0.0));
    float value = tubeReplaySampleCurve(replay, logicalColumn, sampleX);
    float amplitude = pow(value, max(replay.amplitude.x, 0.0001));
    float3 replayWorld = replay.origin.xyz + replay.axisStep.xyz * sampleX + replay.columnStep.xyz * (float)logicalColumn + float3(0.0, amplitude * replay.amplitude.y, 0.0);
    float3 ray = rayDirectionForPixel((float2)pixel, jitterPixels, cameraPosition, cameraTarget);
    float3 delta = replayWorld - cameraPosition;
    float replayTravel = dot(delta, ray);
    if (replayTravel <= 0.0 || replayTravel > farDistance)
    {
        return 0.0;
    }

    float rayDistance = length(delta - ray * replayTravel);
    float radius = max(replay.material.x + value * replay.material.y, 0.0001);
    float support = 1.0 - smoothstep(radius, radius + max(radius * 0.35, 0.01), rayDistance);
    float travelTolerance = max(0.045, currentSample.colorTravel.w * 0.018);
    float travelWeight = 1.0 - smoothstep(travelTolerance, travelTolerance * 4.0, abs(replayTravel - currentSample.colorTravel.w));
    return support * travelWeight;
}

bool fieldReservoirSampleIsSdfObject(FieldReservoirSample sample)
{
    return sample.metadata.x >= FIELD_ID_SDF_OBJECT_BASE &&
        sample.domainSupport.z > FieldDomainKindSdf - 0.25 &&
        sample.domainSupport.z < FieldDomainKindSdf + 0.25;
}

float3 sdfObjectWorldPositionFromSample(FieldReservoirSample sample, float3 sampleCamera, float3 sampleTarget, float2 sampleJitter)
{
    float2 pixel = clamp(sample.domainSample.xy, float2(0.0, 0.0), float2(1.0, 1.0)) * max(resolution, float2(1.0, 1.0));
    float3 ray = rayDirectionForPixel(pixel, sampleJitter, sampleCamera, sampleTarget);
    return sampleCamera + ray * sample.colorTravel.w;
}

float sdfObjectReplayValidationWeight(FieldReservoirSample currentSample, FieldReservoirSample previousSample, uint2 pixel)
{
    if (!fieldReservoirSampleIsSdfObject(currentSample) || !fieldReservoirSampleIsSdfObject(previousSample))
    {
        return 0.0;
    }

    if (abs(previousSample.metadata.x - currentSample.metadata.x) >= 0.001)
    {
        return 0.0;
    }

    int sdfIndex = clamp((int)round(currentSample.metadata.x - FIELD_ID_SDF_OBJECT_BASE), 0, AQUARIUM_SDF_OBJECT_CAPACITY - 1);
    SdfObject sdfObject = sdfObjects[sdfIndex];
    float3 previousWorld = sdfObjectWorldPositionFromSample(
        previousSample,
        previousCameraPosition,
        previousCameraTarget,
        previousJitterPixels);
    float3 localOffset = previousWorld - sdfObject.previousCenterPad.xyz;
    float3 replayWorld = sdfObject.centerRadius.xyz + localOffset;
    float3 ray = rayDirectionForPixel((float2)pixel, jitterPixels, cameraPosition, cameraTarget);
    float3 delta = replayWorld - cameraPosition;
    float replayTravel = dot(delta, ray);
    if (replayTravel <= 0.0 || replayTravel > farDistance)
    {
        return 0.0;
    }

    float rayDistance = length(delta - ray * replayTravel);
    float objectRadius = max(sdfObject.centerRadius.w, 0.0001);
    float supportRadius = max(objectRadius * 0.012, max(currentSample.colorTravel.w * 0.0015, 0.003));
    float support = 1.0 - smoothstep(supportRadius, supportRadius * 4.0, rayDistance);
    float travelTolerance = max(0.045, currentSample.colorTravel.w * 0.018);
    float travelWeight = 1.0 - smoothstep(travelTolerance, travelTolerance * 4.0, abs(replayTravel - currentSample.colorTravel.w));
    return support * travelWeight;
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

float4 sceneReservoirDomainSample(float2 uv, float4 colorTravel, float4 metadata)
{
    if (metadata.x >= FIELD_ID_SDF_OBJECT_BASE)
    {
        float2 pixel = (floor(uv * resolution) + 0.5);
        float3 ray = rayDirectionForPixel(pixel, jitterPixels, cameraPosition, cameraTarget);
        float3 worldPosition = cameraPosition + ray * colorTravel.w;
        int sdfIndex = clamp((int)round(metadata.x - FIELD_ID_SDF_OBJECT_BASE), 0, AQUARIUM_SDF_OBJECT_CAPACITY - 1);
        float3 localPosition = worldPosition - sdfObjects[sdfIndex].centerRadius.xyz;
        return float4(uv, localPosition.xz);
    }

    return float4(uv, uv);
}

float4 sceneReservoirDomainSupport(float4 metadata, float4 control)
{
    if (metadata.x >= FIELD_ID_SDF_OBJECT_BASE)
    {
        float radiusPx = max(1.0, 1.0 + saturate(control.y) * 2.0);
        return float4(radiusPx, radiusPx, FieldDomainKindSdf, FieldShiftKindObjectReplay);
    }

    return float4(1.0, 1.0, FieldDomainKindScreen, FieldShiftKindNone);
}

FieldReservoirSample sceneFieldReservoirSample(float2 uv)
{
    float4 colorTravel = sourceTexture.SampleLevel(sourceSampler, uv, 0.0);
    float4 metadata = loadCurrentMetadata(uv);
    float4 control = loadCurrentControl(uv);
    float4 guide = loadCurrentReservoirGuide(uv);
    float target = fieldReservoirDefaultTarget(colorTravel, control, guide);
    return makeFieldReservoirSample(
        colorTravel,
        metadata,
        control,
        guide,
        0.0,
        sceneReservoirDomainSample(uv, colorTravel, metadata),
        sceneReservoirDomainSupport(metadata, control),
        target,
        1.0,
        1.0,
        FieldProposalKindDeterministicStructural);
}

float reservoirSampleConfidence(FieldReservoirSample sample)
{
    return sample.guide.x > 0.0
        ? saturate(sample.guide.x)
        : (sample.control.w > 0.0 ? saturate(sample.control.w) : 1.0);
}

float reservoirSampleDomainValidity(FieldReservoirSample sample)
{
    return sample.guide.z > 0.0 ? saturate(sample.guide.z) : 1.0;
}

bool fieldReservoirSampleRequiresExplicitMotion(FieldReservoirSample sample)
{
    return sample.metadata.x >= FIELD_ID_TUBE_FIELD_BASE &&
        sample.metadata.x < FIELD_ID_TUBE_FIELD_MAX;
}

float reservoirTemporalValidationWeight(
    FieldReservoirSample currentSample,
    FieldReservoirSample previousSample,
    float expectedPreviousTravel,
    float3 neighborhoodMin,
    float3 neighborhoodMax,
    float2 dimensions)
{
    if (!fieldReservoirSampleValid(currentSample, farDistance) ||
        !fieldReservoirSampleValid(previousSample, farDistance))
    {
        return 0.0;
    }

    bool isTubeFieldSample = fieldReservoirSampleRequiresExplicitMotion(currentSample);
    float travelDelta = abs(previousSample.colorTravel.w - expectedPreviousTravel);
    float travelTolerance = max(0.045, expectedPreviousTravel * 0.018);
    float travelWeight = 1.0 - smoothstep(travelTolerance, travelTolerance * 4.0, travelDelta);
    float fieldWeight = abs(previousSample.metadata.x - currentSample.metadata.x) < 0.001 ? 1.0 : 0.0;
    float normalWeight = 0.0;
    if (dot(previousSample.metadata.yzw, previousSample.metadata.yzw) > 0.01 &&
        dot(currentSample.metadata.yzw, currentSample.metadata.yzw) > 0.01)
    {
        normalWeight = smoothstep(0.68, 0.96, dot(normalize(previousSample.metadata.yzw), normalize(currentSample.metadata.yzw)));
    }

    float3 historyColor = isTubeFieldSample ? previousSample.colorTravel.rgb : clamp(previousSample.colorTravel.rgb, neighborhoodMin, neighborhoodMax);
    float colorDelta = length(historyColor - currentSample.colorTravel.rgb);
    float colorWeight = isTubeFieldSample ? 1.0 : 1.0 - smoothstep(0.18, 1.2, colorDelta);
    float coverageWeight = isTubeFieldSample
        ? smoothstep(0.001, 0.20, saturate(currentSample.control.x))
        : smoothstep(0.02, 0.55, saturate(currentSample.control.x));
    float coverageContinuityWeight = isTubeFieldSample
        ? 1.0
        : 1.0 - smoothstep(0.10, 0.50, abs(saturate(previousSample.control.x) - saturate(currentSample.control.x)));
    float detailWeight = 1.0 - smoothstep(0.08, 0.45, abs(saturate(previousSample.control.z) - saturate(currentSample.control.z)));
    float confidenceWeight = lerp(0.45, 1.0, min(reservoirSampleConfidence(currentSample), reservoirSampleConfidence(previousSample)));
    bool useNativeDomain = nativeDomainReservoirEnabled();
    float domainWeight = useNativeDomain
        ? reservoirSampleDomainValidity(currentSample) * reservoirSampleDomainValidity(previousSample)
        : 1.0;
    float supportOverlap = useNativeDomain
        ? fieldReservoirDomainSupportOverlap(currentSample, previousSample, dimensions)
        : 1.0;
    uint2 currentPixel = (uint2)pixelFromUv(currentSample.domainSample.xy);
    float replayWeight = !useNativeDomain
        ? 1.0
        : (fieldReservoirSampleRequiresExplicitMotion(currentSample)
        ? tubeFieldReplayValidationWeight(currentSample, previousSample, currentPixel)
        : (fieldReservoirSampleIsSdfObject(currentSample)
            ? sdfObjectReplayValidationWeight(currentSample, previousSample, currentPixel)
            : 1.0));
    return travelWeight * fieldWeight * normalWeight * colorWeight * coverageWeight * coverageContinuityWeight * detailWeight * confidenceWeight * domainWeight * supportOverlap * replayWeight;
}

float reservoirTemporalRejectionCode(
    FieldReservoirSample currentSample,
    FieldReservoirSample previousSample,
    float expectedPreviousTravel,
    float2 dimensions)
{
    if (!fieldReservoirSampleValid(previousSample, farDistance))
    {
        return 1.0;
    }

    float travelTolerance = max(0.045, expectedPreviousTravel * 0.018);
    if (previousSample.colorTravel.w < expectedPreviousTravel - travelTolerance * 2.0)
    {
        return 5.0;
    }

    if (previousSample.colorTravel.w > expectedPreviousTravel + travelTolerance * 2.0)
    {
        return 2.0;
    }

    if (abs(previousSample.metadata.x - currentSample.metadata.x) >= 0.001)
    {
        return 6.0;
    }

    if (!nativeDomainReservoirEnabled())
    {
        return 1.0;
    }

    if (!fieldReservoirDomainStateValid(previousSample) || !fieldReservoirDomainStateValid(currentSample))
    {
        return 7.0;
    }

    if (fieldReservoirDomainSupportOverlap(currentSample, previousSample, dimensions) <= 0.0)
    {
        return 8.0;
    }

    if (fieldReservoirSampleRequiresExplicitMotion(currentSample) &&
        tubeFieldReplayValidationWeight(currentSample, previousSample, (uint2)pixelFromUv(currentSample.domainSample.xy)) <= 0.0)
    {
        return 9.0;
    }

    if (fieldReservoirSampleIsSdfObject(currentSample) &&
        sdfObjectReplayValidationWeight(currentSample, previousSample, (uint2)pixelFromUv(currentSample.domainSample.xy)) <= 0.0)
    {
        return 10.0;
    }

    return 1.0;
}

float reservoirSpatialValidationWeight(FieldReservoirSample currentSample, FieldReservoirSample neighborSample, float pixelDistance, float2 dimensions)
{
    if (!fieldReservoirSampleValid(currentSample, farDistance) ||
        !fieldReservoirSampleValid(neighborSample, farDistance))
    {
        return 0.0;
    }

    float fieldWeight = abs(neighborSample.metadata.x - currentSample.metadata.x) < 0.001 ? 1.0 : 0.0;
    float travelDelta = abs(neighborSample.colorTravel.w - currentSample.colorTravel.w);
    float travelTolerance = max(0.05, currentSample.colorTravel.w * 0.02);
    float travelWeight = 1.0 - smoothstep(travelTolerance, travelTolerance * 4.0, travelDelta);
    float normalWeight = 0.0;
    if (dot(neighborSample.metadata.yzw, neighborSample.metadata.yzw) > 0.01 &&
        dot(currentSample.metadata.yzw, currentSample.metadata.yzw) > 0.01)
    {
        normalWeight = smoothstep(0.72, 0.97, dot(normalize(neighborSample.metadata.yzw), normalize(currentSample.metadata.yzw)));
    }

    float supportWeight = smoothstep(0.001, 0.45, min(saturate(neighborSample.control.x), saturate(currentSample.control.x)));
    float supportOverlap = fieldReservoirDomainSupportOverlap(currentSample, neighborSample, dimensions);
    float distanceWeight = 1.0 - smoothstep(0.0, 1.75, pixelDistance);
    return fieldWeight * travelWeight * normalWeight * supportWeight * supportOverlap * distanceWeight *
        reservoirSampleDomainValidity(currentSample) * reservoirSampleDomainValidity(neighborSample);
}

FieldReservoirSample currentFrameReservoirSample(uint2 pixel, float2 uv)
{
    uint width = (uint)max(resolution.x, 1.0);
    uint baseIndex = (pixel.y * width + pixel.x) * FieldReservoirSlotsPerPixel;
    FieldReservoirSample current = sceneFieldReservoirSample(uv);
    FieldReservoirSample tube = fieldReservoirCandidates[baseIndex + FieldReservoirRowCurrent];
    current = mergeFieldReservoirVisibilityProposals(
        current,
        tube,
        fieldReservoirRandom01(pixel, (uint)frameIndex, 31u),
        farDistance);
    current.guide.y = 0.0;
    current.guide.w = 0.0;
    return current;
}

FieldReservoirSample temporallyReuseReservoirSample(FieldReservoirSample current, uint2 pixel, float2 uv)
{
    FieldReservoirSample temporal = current;
    if (!fieldReservoirSampleValid(current, farDistance))
    {
        return temporal;
    }

    bool requiresMotion = fieldReservoirSampleRequiresExplicitMotion(current);
    if (requiresMotion && current.motion.w <= 0.5)
    {
        temporal.guide.w = 4.0;
        return temporal;
    }

    if (frameIndex <= 0.5)
    {
        return temporal;
    }

    float expectedPreviousTravel = current.motion.z;
    float2 previousUv = current.motion.xy;
    if (current.motion.w <= 0.5)
    {
        float2 pixelFloat = float2(pixel);
        float3 currentRay = rayDirectionForPixel(pixelFloat, jitterPixels, cameraPosition, cameraTarget);
        float3 worldPosition = cameraPosition + currentRay * current.colorTravel.w;
        float3 previousWorldPosition = temporalPreviousWorldPosition(worldPosition, current.metadata.x);
        previousUv = projectWorldToPreviousHistoryUv(previousWorldPosition);
        expectedPreviousTravel = distance(previousCameraPosition, previousWorldPosition);
    }

    if (!all(previousUv >= 0.0) || !all(previousUv <= 1.0))
    {
        temporal.guide.w = 2.0;
        return temporal;
    }

    uint2 dimensions = (uint2)max(resolution, float2(1.0, 1.0));
    uint2 previousPixel = min((uint2)pixelFromUv(previousUv), dimensions - 1u);
    uint previousBaseIndex = (previousPixel.y * dimensions.x + previousPixel.x) * FieldReservoirSlotsPerPixel;
    FieldReservoirSample previous = reservoirHistoryRead[previousBaseIndex + FieldReservoirRowFinal];
    float3 neighborhoodMin;
    float3 neighborhoodMax;
    currentNeighborhood(uv, neighborhoodMin, neighborhoodMax);
    float validationWeight = reservoirTemporalValidationWeight(
        current,
        previous,
        expectedPreviousTravel,
        neighborhoodMin,
        neighborhoodMax,
        (float2)dimensions);
    if (validationWeight <= 0.0)
    {
        temporal.guide.w = reservoirTemporalRejectionCode(current, previous, expectedPreviousTravel, (float2)dimensions);
        return temporal;
    }

    FieldReservoirSample validatedPrevious = scaleFieldReservoirSampleWeight(previous, validationWeight);
    validatedPrevious.control.w = min(max(previous.control.w, 0.0) + 1.0, MAX_HISTORY_AGE);
    validatedPrevious.guide.y = min(max(previous.guide.y, 0.0) + 1.0, MAX_HISTORY_AGE);
    temporal = mergeFieldReservoirSamples(
        current,
        validatedPrevious,
        fieldReservoirRandom01(pixel, (uint)frameIndex, 73u),
        farDistance);
    temporal.control.w = min(max(current.control.w, validatedPrevious.control.w), MAX_HISTORY_AGE);
    temporal.guide.y = min(max(current.guide.y, validatedPrevious.guide.y), MAX_HISTORY_AGE);
    temporal.guide.w = 0.0;
    return temporal;
}

FieldReservoirSample spatiallyReuseReservoirSample(FieldReservoirSample temporal, uint2 pixel, float2 uv, uint2 dimensions)
{
    FieldReservoirSample spatial = temporal;
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

            int2 neighborSigned = clamp((int2)pixel + int2(x, y), int2(0, 0), (int2)dimensions - int2(1, 1));
            uint2 neighborPixel = (uint2)neighborSigned;
            float2 neighborUv = (float2(neighborPixel) + 0.5) / float2(dimensions);
            FieldReservoirSample neighbor = currentFrameReservoirSample(neighborPixel, neighborUv);
            float validationWeight = reservoirSpatialValidationWeight(spatial, neighbor, length(float2(x, y)), (float2)dimensions);
            if (validationWeight <= 0.0)
            {
                continue;
            }

            FieldReservoirSample validatedNeighbor = scaleFieldReservoirSampleWeight(neighbor, validationWeight * 0.55);
            spatial = mergeFieldReservoirSamples(
                spatial,
                validatedNeighbor,
                fieldReservoirRandom01(pixel + neighborPixel, (uint)frameIndex, 109u),
                farDistance);
            spatial.guide.w = 3.0;
        }
    }

    return spatial;
}

float4 reservoirDebugOrColor(FieldReservoirSample candidate, uint2 pixel)
{
    if (renderDebugMode >= 19.5 && renderDebugMode < 20.5)
    {
        bool spendsSpatialReuse = fieldReservoirShouldSpendSpatialReuse(pixel);
        float budget = fieldReservoirSpatialReuseBudget();
        if (spendsSpatialReuse)
        {
            return float4(0.05, 0.95, 0.35, candidate.colorTravel.w);
        }

        return float4(0.02, 0.08, max(0.18, budget), candidate.colorTravel.w);
    }

    if (renderDebugMode >= 12.5 && renderDebugMode < 13.5)
    {
        if (!fieldReservoirSampleValid(candidate, farDistance))
        {
            return float4(0.025, 0.025, 0.035, candidate.colorTravel.w);
        }

        float invalidation = candidate.guide.w;
        float3 color = float3(0.0, 0.0, 0.0);
        if (invalidation < 0.5)
        {
            color = float3(0.0, 0.85, 0.25);
        }
        else if (invalidation < 1.5)
        {
            color = float3(1.0, 0.85, 0.0);
        }
        else if (invalidation < 2.5)
        {
            color = float3(1.0, 0.0, 0.0);
        }
        else if (invalidation < 3.5)
        {
            color = float3(0.2, 0.55, 1.0);
        }
        else if (invalidation < 4.5)
        {
            color = float3(1.0, 0.0, 0.85);
        }
        else if (invalidation < 5.5)
        {
            color = float3(0.0, 1.0, 1.0);
        }
        else if (invalidation < 6.5)
        {
            color = float3(0.9, 0.0, 1.0);
        }
        else if (invalidation < 7.5)
        {
            color = float3(1.0, 0.35, 0.0);
        }
        else if (invalidation < 8.5)
        {
            color = float3(0.5, 0.15, 1.0);
        }
        else if (invalidation < 9.5)
        {
            color = float3(1.0, 0.0, 0.25);
        }
        else
        {
            color = float3(1.0, 0.55, 0.05);
        }

        return float4(color, candidate.colorTravel.w);
    }

    if (renderDebugMode >= 13.5 && renderDebugMode < 14.5)
    {
        if (!fieldReservoirSampleValid(candidate, farDistance))
        {
            return float4(0.0, 0.0, 0.0, candidate.colorTravel.w);
        }

        return float4(
            saturate(candidate.stats.x),
            saturate(candidate.stats.y * 0.1),
            saturate(candidate.stats.z / 16.0),
            candidate.colorTravel.w);
    }

    if (renderDebugMode >= 14.5 && renderDebugMode < 15.5)
    {
        if (!fieldReservoirSampleValid(candidate, farDistance))
        {
            return float4(0.0, 0.0, 0.0, candidate.colorTravel.w);
        }

        return float4(
            saturate(candidate.proposal.x),
            saturate(candidate.proposal.y),
            saturate(fieldReservoirContributionWeight(candidate)),
            candidate.colorTravel.w);
    }

    if (renderDebugMode >= 15.5 && renderDebugMode < 16.5)
    {
        if (!fieldReservoirSampleValid(candidate, farDistance))
        {
            return float4(0.0, 0.0, 0.0, candidate.colorTravel.w);
        }

        return float4(frac(candidate.domainSample.x), frac(candidate.domainSample.y), saturate(candidate.domainSupport.z / 4.0), candidate.colorTravel.w);
    }

    if (renderDebugMode >= 16.5 && renderDebugMode < 17.5)
    {
        if (!fieldReservoirSampleValid(candidate, farDistance))
        {
            return float4(0.0, 0.0, 0.0, candidate.colorTravel.w);
        }

        float2 footprint = saturate(candidate.domainSupport.xy / 8.0);
        return float4(footprint.x, footprint.y, saturate(candidate.guide.z), candidate.colorTravel.w);
    }

    if (renderDebugMode >= 17.5 && renderDebugMode < 18.5)
    {
        if (!fieldReservoirSampleValid(candidate, farDistance))
        {
            return float4(0.0, 0.0, 0.0, candidate.colorTravel.w);
        }

        return float4(saturate(candidate.domainSupport.w / 3.0), saturate(candidate.guide.w / 7.0), saturate(candidate.domainSupport.z / 4.0), candidate.colorTravel.w);
    }

    if (renderDebugMode >= 18.5 && renderDebugMode < 19.5)
    {
        if (!fieldReservoirSampleValid(candidate, farDistance))
        {
            return float4(0.0, 0.0, 0.0, candidate.colorTravel.w);
        }

        float code = candidate.guide.w;
        float previousUvOut = step(1.5, code) * (1.0 - step(2.5, code));
        float previousCloser = step(4.5, code) * (1.0 - step(5.5, code));
        float supportLost = step(7.5, code) * (1.0 - step(8.5, code));
        return float4(previousUvOut, previousCloser, supportLost, candidate.colorTravel.w);
    }

    return float4(fieldReservoirResolvedColor(candidate, farDistance), candidate.colorTravel.w);
}

FieldReservoirResolveOut D3D12FieldReservoirResolvePS(VertexOut input)
{
    uint2 pixel = (uint2)pixelFromUv(input.uv);

    FieldReservoirResolveOut output;
    output.colorTravel = sourceTexture.Load(int3(pixel, 0));
    output.metadata = loadCurrentMetadata(input.uv);
    output.control = loadCurrentControl(input.uv);
    output.reservoirGuide = loadCurrentReservoirGuide(input.uv);
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
    FieldReservoirSample current = currentFrameReservoirSample(currentPixel, uv);
    FieldReservoirSample temporal = temporallyReuseReservoirSample(current, currentPixel, uv);
    FieldReservoirSample spatial = temporal;
    if (fieldReservoirShouldSpendSpatialReuse(currentPixel))
    {
        spatial = spatiallyReuseReservoirSample(temporal, currentPixel, uv, dimensions);
    }
    FieldReservoirSample finalSample = spatial;
    finalSample.guide.w = spatial.guide.w;

    reservoirHistoryWrite[baseIndex + FieldReservoirRowCurrent] = current;
    reservoirHistoryWrite[baseIndex + FieldReservoirRowTemporal] = temporal;
    reservoirHistoryWrite[baseIndex + FieldReservoirRowSpatial] = spatial;
    reservoirHistoryWrite[baseIndex + FieldReservoirRowFinal] = finalSample;
    reservoirResolvedTexture[currentPixel] = reservoirDebugOrColor(finalSample, currentPixel);
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
    else if (renderDebugMode >= 12.5 && renderDebugMode < 18.5)
    {
        finalColor = saturate(resolved);
    }

    ResolveOut output;
    output.finalColor = float4(ditherDisplay(finalColor, input.uv), 1.0);
    return output;
}
