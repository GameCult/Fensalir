struct TubeFieldVertex
{
    float3 position;
    float3 segmentStart;
    float3 segmentEnd;
    float3 previousPoint;
    float3 nextPoint;
    float4 shapeData;
    float4 radiusData;
    float4 color;
    float4 material;
    float4 tubeData;
};

struct TubeFieldSegment
{
    float4 previousRadius;
    float4 startRadius;
    float4 endFeather;
    float4 nextAlpha;
    float4 color0;
    float4 color1;
    float4 material;
    float4 tubeData;
};

struct FieldReservoirCandidate
{
    float4 colorTravel;
    float4 metadata;
    float4 control;
    float4 reservoirGuide;
};

struct TubeFieldReservoir
{
    float4 colorTravel;
    float4 metadata;
    float4 control;
    float4 reservoirGuide;
    float4 statistics;
    float4 sampleData;
    uint4 sampleKey;
};

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

cbuffer TubeFieldConstants : register(b3)
{
    float4 tubeShape;
    float4 tubeColumns;
    float4 tubeAmplitude;
    float4 tubeMaterial;
    float4 tubeDispatch;
    float4 tubeDraw;
    float3 tubeOrigin;
    float tubePadding0;
    float3 tubeAxisStep;
    float tubePadding1;
    float3 tubeColumnStep;
    float tubePadding2;
};

ByteAddressBuffer TubeFieldSamples : register(t42);
Texture2D<float4> TubeFieldRamp : register(t43);
Texture2D<float> TubeFieldBlueNoise : register(t44);
SamplerState TubeFieldRampSampler : register(s0);
RWStructuredBuffer<TubeFieldVertex> TubeFieldVertices : register(u10);
RWStructuredBuffer<uint> TubeFieldIndices : register(u11);
RWStructuredBuffer<uint> TubeFieldStats : register(u12);
RWStructuredBuffer<uint> TubeFieldDrawArguments : register(u13);
RWStructuredBuffer<TubeFieldSegment> TubeFieldSegments : register(u16);

StructuredBuffer<TubeFieldSegment> RestirSegments : register(t46);
StructuredBuffer<uint> RestirTileSegmentsRead : register(t47);
StructuredBuffer<TubeFieldReservoir> RestirPreviousReservoirs : register(t48);
StructuredBuffer<TubeFieldReservoir> RestirReadReservoirs : register(t49);
StructuredBuffer<uint> RestirTileCountsRead : register(t50);
RWStructuredBuffer<uint> RestirTileCounts : register(u17);
RWStructuredBuffer<uint> RestirTileSegments : register(u18);
RWStructuredBuffer<TubeFieldReservoir> RestirInitialReservoirs : register(u19);
RWStructuredBuffer<TubeFieldReservoir> RestirWriteReservoirs : register(u20);
RWStructuredBuffer<FieldReservoirCandidate> RestirFieldReservoirCandidates : register(u21);

static const uint TubeFieldRestirTileSize = 16u;
static const uint TubeFieldRestirMaxTileSegments = 128u;
static const uint TubeFieldRestirInitialCandidateCount = TubeFieldRestirMaxTileSegments;
static const uint TubeFieldRestirSpatialCandidateCount = 4u;
static const uint FieldReservoirSlotsPerPixel = 4u;

RWStructuredBuffer<FieldReservoirCandidate> FieldReservoirCandidates : register(u14);
RWByteAddressBuffer FieldReservoirLocks : register(u15);

uint PositiveModulo(int value, uint modulo)
{
    int m = (int)max(modulo, 1u);
    int r = value % m;
    return (uint)(r < 0 ? r + m : r);
}

uint SampleAddress(uint logicalColumn, uint sampleIndex)
{
    uint width = max((uint)round(tubeShape.x), 1u);
    uint height = max((uint)round(tubeShape.y), 1u);
    uint firstColumn = (uint)max(round(tubeShape.w), 0.0);
    uint columnStride = max((uint)round(tubeColumns.y), 1u);
    uint rollingModulo = (uint)max(round(tubeColumns.z), 0.0);
    int rollingOffset = (int)round(tubeColumns.w);
    uint physicalColumn = firstColumn + logicalColumn * columnStride;
    if (rollingModulo > 0u)
    {
        physicalColumn = PositiveModulo((int)physicalColumn + rollingOffset, rollingModulo);
    }

    physicalColumn = min(physicalColumn, height - 1u);
    uint strideBytes = max((uint)round(tubeShape.z), 4u);
    return (physicalColumn * width + min(sampleIndex, width - 1u)) * strideBytes;
}

float RawSample(uint logicalColumn, int sampleIndex)
{
    uint width = max((uint)round(tubeShape.x), 1u);
    uint clamped = (uint)clamp(sampleIndex, 0, (int)width - 1);
    return asfloat(TubeFieldSamples.Load(SampleAddress(logicalColumn, clamped)));
}

float FilteredSample(uint logicalColumn, float x)
{
    int center = (int)floor(x + 0.5);
    float s0 = RawSample(logicalColumn, center - 2);
    float s1 = RawSample(logicalColumn, center - 1);
    float s2 = RawSample(logicalColumn, center);
    float s3 = RawSample(logicalColumn, center + 1);
    float s4 = RawSample(logicalColumn, center + 2);
    return (s0 + s4 + 4.0 * (s1 + s3) + 6.0 * s2) / 16.0;
}

float NormalizedSample(uint logicalColumn, float x)
{
    float value = FilteredSample(logicalColumn, x);
    return saturate((value - tubeAmplitude.z) / max(tubeAmplitude.w - tubeAmplitude.z, 0.0001));
}

float Catmull(float p0, float p1, float p2, float p3, float t)
{
    float t2 = t * t;
    float t3 = t2 * t;
    return 0.5 * ((2.0 * p1) +
        (-p0 + p2) * t +
        (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 +
        (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3);
}

float SampleCurve(uint logicalColumn, float x)
{
    int i1 = (int)floor(x);
    float t = frac(x);
    float p0 = NormalizedSample(logicalColumn, (float)(i1 - 1));
    float p1 = NormalizedSample(logicalColumn, (float)i1);
    float p2 = NormalizedSample(logicalColumn, (float)(i1 + 1));
    float p3 = NormalizedSample(logicalColumn, (float)(i1 + 2));
    return saturate(Catmull(p0, p1, p2, p3, t));
}

float SampleAmplitudeCurve(uint logicalColumn, float x)
{
    return pow(SampleCurve(logicalColumn, x), max(tubeAmplitude.x, 0.0001));
}

float3 TubePoint(uint logicalColumn, float x)
{
    float value = SampleAmplitudeCurve(logicalColumn, x);
    return tubeOrigin +
        tubeAxisStep * x +
        tubeColumnStep * (float)logicalColumn +
        float3(0.0, value * tubeAmplitude.y, 0.0);
}

TubeFieldVertex MakeTubeVertex(float3 position, float3 previous, float3 start, float3 end, float3 next, float side, float endpointT, float capSign, float value, float3 rampColor, uint logicalColumn, float x0, float x1)
{
    TubeFieldVertex vertex;
    float radius = max(tubeMaterial.x + value * tubeMaterial.y, 0.0001);
    vertex.position = position;
    vertex.segmentStart = start;
    vertex.segmentEnd = end;
    vertex.previousPoint = previous;
    vertex.nextPoint = next;
    vertex.shapeData = float4(side, endpointT, capSign, 0.0);
    vertex.radiusData = float4(radius, radius, tubeMaterial.w, 0.0);
    vertex.color = float4(rampColor, tubeMaterial.z);
    vertex.material = float4(max(tubeDispatch.x, 0.0), tubeMaterial.z, 4.0, 0.0001);
    vertex.tubeData = float4((float)logicalColumn, x0, x1, tubeDraw.z);
    return vertex;
}

TubeFieldSegment MakeTubeSegment(float3 previous, float3 start, float3 end, float3 next, float v0, float v1, float3 rampColor0, float3 rampColor1, uint logicalColumn, float x0, float x1)
{
    TubeFieldSegment segment;
    float radius0 = max(tubeMaterial.x + v0 * tubeMaterial.y, 0.0001);
    float radius1 = max(tubeMaterial.x + v1 * tubeMaterial.y, 0.0001);
    segment.previousRadius = float4(previous, radius0);
    segment.startRadius = float4(start, radius0);
    segment.endFeather = float4(end, tubeMaterial.w);
    segment.nextAlpha = float4(next, tubeMaterial.z);
    segment.color0 = float4(rampColor0, v0);
    segment.color1 = float4(rampColor1, v1);
    segment.material = float4(max(tubeDispatch.x, 0.0), tubeMaterial.z, 4.0, 0.0001);
    segment.tubeData = float4((float)logicalColumn, x0, x1, tubeDraw.z);
    return segment;
}

[numthreads(256, 1, 1)]
void D3D12TubeFieldExpandCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint localSegment = dispatchThreadId.x;
    uint dispatchSegments = (uint)round(tubeDispatch.w);
    if (localSegment == 0u)
    {
        uint argumentBase = (uint)round(tubeDraw.x);
        TubeFieldDrawArguments[argumentBase + 0u] = dispatchSegments * 6u;
        TubeFieldDrawArguments[argumentBase + 1u] = 1u;
        TubeFieldDrawArguments[argumentBase + 2u] = (uint)round(tubeDraw.y);
        TubeFieldDrawArguments[argumentBase + 3u] = 0u;
        TubeFieldDrawArguments[argumentBase + 4u] = 0u;
    }

    if (localSegment >= dispatchSegments)
    {
        return;
    }

    uint width = max((uint)round(tubeShape.x), 2u);
    uint subdivisions = max((uint)round(tubeDispatch.y), 1u);
    uint piecesPerColumn = (width - 1u) * subdivisions;
    uint logicalColumn = localSegment / max(piecesPerColumn, 1u);
    uint pieceInColumn = localSegment % max(piecesPerColumn, 1u);
    uint sourceSegment = pieceInColumn / subdivisions;
    uint subdivision = pieceInColumn % subdivisions;
    float t0 = (float)subdivision / (float)subdivisions;
    float t1 = (float)(subdivision + 1u) / (float)subdivisions;
    float x0 = (float)sourceSegment + t0;
    float x1 = (float)sourceSegment + t1;
    float xPrev = max(0.0, x0 - 1.0 / (float)subdivisions);
    float xNext = min((float)(width - 1u), x1 + 1.0 / (float)subdivisions);
    float v0 = SampleCurve(logicalColumn, x0);
    float v1 = SampleCurve(logicalColumn, x1);
    float3 rampColor0 = TubeFieldRamp.SampleLevel(TubeFieldRampSampler, float2(saturate(v0), 0.5), 0.0).rgb;
    float3 rampColor1 = TubeFieldRamp.SampleLevel(TubeFieldRampSampler, float2(saturate(v1), 0.5), 0.0).rgb;
    float3 previous = TubePoint(logicalColumn, xPrev);
    float3 start = TubePoint(logicalColumn, x0);
    float3 end = TubePoint(logicalColumn, x1);
    float3 next = TubePoint(logicalColumn, xNext);

    uint globalSegment = (uint)round(tubeDispatch.z) + localSegment;
    uint vertexBase = globalSegment * 4u;
    uint indexBase = globalSegment * 6u;
    TubeFieldSegments[globalSegment] = MakeTubeSegment(previous, start, end, next, v0, v1, rampColor0, rampColor1, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 0u] = MakeTubeVertex(start, previous, start, end, next, -1.0, 0.0, -1.0, v0, rampColor0, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 1u] = MakeTubeVertex(start, previous, start, end, next, 1.0, 0.0, -1.0, v0, rampColor0, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 2u] = MakeTubeVertex(end, previous, start, end, next, -1.0, 1.0, 1.0, v1, rampColor1, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 3u] = MakeTubeVertex(end, previous, start, end, next, 1.0, 1.0, 1.0, v1, rampColor1, logicalColumn, x0, x1);
    TubeFieldIndices[indexBase + 0u] = vertexBase + 0u;
    TubeFieldIndices[indexBase + 1u] = vertexBase + 1u;
    TubeFieldIndices[indexBase + 2u] = vertexBase + 2u;
    TubeFieldIndices[indexBase + 3u] = vertexBase + 2u;
    TubeFieldIndices[indexBase + 4u] = vertexBase + 1u;
    TubeFieldIndices[indexBase + 5u] = vertexBase + 3u;
    TubeFieldStats[0] = globalSegment + 1u;
}

struct TubeFieldVertexIn
{
    float3 position : POSITION;
    float3 segmentStart : TEXCOORD0;
    float3 segmentEnd : TEXCOORD1;
    float3 previousPoint : TEXCOORD2;
    float3 nextPoint : TEXCOORD3;
    float4 shapeData : TEXCOORD4;
    float4 radiusData : TEXCOORD5;
    float4 color : COLOR;
    float4 material : TEXCOORD6;
    float4 tubeData : TEXCOORD7;
};

struct TubeFieldVertexOut
{
    float4 position : SV_Position;
    nointerpolation float2 segmentStartPx : TEXCOORD0;
    nointerpolation float2 segmentEndPx : TEXCOORD1;
    nointerpolation float2 previousPx : TEXCOORD2;
    nointerpolation float2 nextPx : TEXCOORD3;
    nointerpolation float2 segmentRadiusPx : TEXCOORD4;
    nointerpolation float2 joinRadiusPx : TEXCOORD5;
    nointerpolation float4 segmentValidity : TEXCOORD6;
    float2 segmentUv : TEXCOORD7;
    float4 color : COLOR;
    float4 material : TEXCOORD8;
    nointerpolation float feather : TEXCOORD9;
    nointerpolation float2 segmentTravel : TEXCOORD10;
    nointerpolation float4 tubeData : TEXCOORD11;
};

struct SceneOut
{
    float4 colorTravel : SV_Target0;
    float4 metadata : SV_Target1;
    float4 control : SV_Target2;
    float4 reservoirGuide : SV_Target3;
    float depth : SV_Depth;
};

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
    float2 slope = float2(
        lerp(cameraFrustumXy.x, cameraFrustumXy.y, uv.x),
        lerp(cameraFrustumXy.w, cameraFrustumXy.z, uv.y));
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(camera, target, forward, right, up);
    return normalize(forward + right * slope.x + up * slope.y);
}

float4 projectCameraSpace(float3 view)
{
    float z = max(view.z, 0.0001);
    float2 slope = view.xy / z;
    float2 frustumMin = float2(cameraFrustumXy.x, cameraFrustumXy.z);
    float2 frustumMax = float2(cameraFrustumXy.y, cameraFrustumXy.w);
    float2 uv = (slope - frustumMin) / max(frustumMax - frustumMin, float2(0.0001, 0.0001));
    float depth = saturate((z - cameraFrustumZ.x) / max(cameraFrustumZ.y - cameraFrustumZ.x, 0.0001));
    return float4(uv * 2.0 - 1.0, depth, 1.0);
}

float2 ndcToPixel(float2 ndc)
{
    return float2((ndc.x * 0.5 + 0.5) * resolution.x, (1.0 - (ndc.y * 0.5 + 0.5)) * resolution.y);
}

float2 pixelToNdc(float2 pixel)
{
    return float2(pixel.x / max(resolution.x, 1.0) * 2.0 - 1.0, 1.0 - pixel.y / max(resolution.y, 1.0) * 2.0);
}

float splineRadiusToPixels(float radiusWorld, float viewDepth)
{
    float frustumHeight = max(cameraFrustumXy.w - cameraFrustumXy.z, 0.0001);
    return max(radiusWorld * resolution.y / max(frustumHeight * max(viewDepth, 0.0001), 0.0001), 0.5);
}

float3 worldToCameraSpace(float3 worldPoint, float3 right, float3 up, float3 forward)
{
    return float3(
        dot(worldPoint - cameraPosition, right),
        dot(worldPoint - cameraPosition, up),
        dot(worldPoint - cameraPosition, forward));
}

float2 safeDirection(float2 value, float2 fallback)
{
    float lengthValue = length(value);
    return lengthValue > 0.001 ? value / lengthValue : fallback;
}

void capsuleDistancePx(
    float2 samplePx,
    float2 startPx,
    float2 endPx,
    float startRadiusPx,
    float endRadiusPx,
    out float sdf,
    out float closestT,
    out float radiusPx,
    out float2 normalPx)
{
    float2 segment = endPx - startPx;
    float segmentLength2 = max(dot(segment, segment), 0.0001);
    closestT = saturate(dot(samplePx - startPx, segment) / segmentLength2);
    float2 closest = startPx + segment * closestT;
    radiusPx = lerp(startRadiusPx, endRadiusPx, closestT);
    float2 normalPxRaw = samplePx - closest;
    float normalPxLength = length(normalPxRaw);
    float2 tangent = safeDirection(segment, float2(1.0, 0.0));
    normalPx = normalPxLength > 0.0001 ? normalPxRaw / normalPxLength : float2(-tangent.y, tangent.x);
    sdf = normalPxLength - radiusPx;
}

float blueNoiseAt(float2 pixel, uint salt)
{
    uint width;
    uint height;
    TubeFieldBlueNoise.GetDimensions(width, height);
    uint2 dimensions = max(uint2(width, height), uint2(1, 1));
    uint frame = (uint)frameIndex;
    uint2 offset = uint2(frame * 17u + salt * 43u, frame * 29u + salt * 71u);
    uint2 coord = (uint2(max(pixel, float2(0.0, 0.0))) + offset) % dimensions;
    return TubeFieldBlueNoise.Load(int3(coord, 0));
}

bool candidateIsValid(FieldReservoirCandidate candidate)
{
    return candidate.metadata.x > 0.5 &&
        candidate.colorTravel.w > 0.0 &&
        candidate.colorTravel.w <= farDistance &&
        saturate(candidate.control.x) > 0.0 &&
        saturate(candidate.reservoirGuide.z) > 0.0;
}

float candidatePriority(FieldReservoirCandidate candidate)
{
    if (!candidateIsValid(candidate))
    {
        return 1.0e20;
    }

    return candidate.colorTravel.w - saturate(candidate.control.x) * 0.025;
}

void injectFieldReservoirCandidate(FieldReservoirCandidate candidate, float2 pixel)
{
    uint2 clampedPixel = min((uint2)max(pixel, float2(0.0, 0.0)), (uint2)max(resolution - 1.0, float2(0.0, 0.0)));
    uint pixelIndex = clampedPixel.y * (uint)max(resolution.x, 1.0) + clampedPixel.x;
    uint lockAddress = pixelIndex * 4u;
    uint acquired = 0u;

    [allow_uav_condition]
    for (uint attempt = 0u; attempt < 32u; attempt++)
    {
        uint previous;
        FieldReservoirLocks.InterlockedCompareExchange(lockAddress, 0u, 1u, previous);
        if (previous == 0u)
        {
            acquired = 1u;
            break;
        }
    }

    if (acquired == 0u)
    {
        return;
    }

    uint baseIndex = pixelIndex * FieldReservoirSlotsPerPixel;
    float priorityNew = candidatePriority(candidate);
    float worstPriority = -1.0;
    uint worstSlot = 0u;

    [unroll]
    for (uint slot = 0u; slot < FieldReservoirSlotsPerPixel; slot++)
    {
        float priority = candidatePriority(FieldReservoirCandidates[baseIndex + slot]);
        if (priority > worstPriority)
        {
            worstPriority = priority;
            worstSlot = slot;
        }
    }

    if (priorityNew < worstPriority)
    {
        FieldReservoirCandidates[baseIndex + worstSlot] = candidate;
    }

    FieldReservoirLocks.Store(lockAddress, 0u);
}

void nearestTubeDistancePx(
    TubeFieldVertexOut input,
    float2 samplePx,
    out float sdf,
    out float closestT,
    out float radiusPx,
    out float2 normalPx)
{
    capsuleDistancePx(samplePx, input.segmentStartPx, input.segmentEndPx, input.segmentRadiusPx.x, input.segmentRadiusPx.y, sdf, closestT, radiusPx, normalPx);

    float previousSdf;
    float previousT;
    float previousRadius;
    float2 previousNormal;
    capsuleDistancePx(samplePx, input.previousPx, input.segmentStartPx, input.joinRadiusPx.x, input.segmentRadiusPx.x, previousSdf, previousT, previousRadius, previousNormal);
    if (input.segmentValidity.x > 0.5 && previousSdf < sdf)
    {
        sdf = previousSdf;
        closestT = 0.0;
        radiusPx = previousRadius;
        normalPx = previousNormal;
    }

    float nextSdf;
    float nextT;
    float nextRadius;
    float2 nextNormal;
    capsuleDistancePx(samplePx, input.segmentEndPx, input.nextPx, input.segmentRadiusPx.y, input.joinRadiusPx.y, nextSdf, nextT, nextRadius, nextNormal);
    if (input.segmentValidity.y > 0.5 && nextSdf < sdf)
    {
        sdf = nextSdf;
        closestT = 1.0;
        radiusPx = nextRadius;
        normalPx = nextNormal;
    }
}

uint restirPixelCount()
{
    return (uint)max(resolution.x, 1.0) * (uint)max(resolution.y, 1.0);
}

uint2 restirTileDimensions()
{
    return ((uint2)max(resolution, float2(1.0, 1.0)) + TubeFieldRestirTileSize - 1u) / TubeFieldRestirTileSize;
}

uint wangHash(uint value)
{
    value = (value ^ 61u) ^ (value >> 16);
    value *= 9u;
    value = value ^ (value >> 4);
    value *= 0x27d4eb2du;
    value = value ^ (value >> 15);
    return value;
}

float restirRandom01(uint seed)
{
    return (float)(wangHash(seed) & 0x00ffffffu) / 16777216.0;
}

bool finite1(float value)
{
    return !isnan(value) && !isinf(value);
}

bool finite2(float2 value)
{
    return all(!isnan(value)) && all(!isinf(value));
}

bool finite3(float3 value)
{
    return all(!isnan(value)) && all(!isinf(value));
}

bool finite4(float4 value)
{
    return all(!isnan(value)) && all(!isinf(value));
}

TubeFieldReservoir emptyTubeFieldReservoir()
{
    TubeFieldReservoir reservoir;
    reservoir.colorTravel = float4(0.0, 0.0, 0.0, farDistance + 1.0);
    reservoir.metadata = 0.0;
    reservoir.control = 0.0;
    reservoir.reservoirGuide = float4(1.0, 0.0, 0.0, 0.0);
    reservoir.statistics = 0.0;
    reservoir.sampleData = 0.0;
    reservoir.sampleKey = 0xffffffffu;
    return reservoir;
}

bool restirReservoirValid(TubeFieldReservoir reservoir)
{
    uint segmentBase = (uint)round(tubeDispatch.z);
    uint dispatchSegments = (uint)round(tubeDispatch.w);
    return reservoir.sampleKey.x != 0xffffffffu &&
        reservoir.sampleKey.x >= segmentBase &&
        reservoir.sampleKey.x < segmentBase + dispatchSegments &&
        reservoir.statistics.x > 0.0 &&
        reservoir.statistics.y > 0.0 &&
        reservoir.metadata.x > 0.5 &&
        reservoir.colorTravel.w <= farDistance;
}

void restirStreamCandidate(
    inout TubeFieldReservoir reservoir,
    TubeFieldReservoir candidate,
    float targetPdf,
    float invSourcePdf,
    float randomValue)
{
    if (!restirReservoirValid(candidate) || targetPdf <= 0.0 || invSourcePdf <= 0.0)
    {
        return;
    }

    float risWeight = targetPdf * invSourcePdf;
    reservoir.statistics.x += risWeight;
    reservoir.statistics.z += 1.0;
    if (randomValue * max(reservoir.statistics.x, 0.000001) < risWeight)
    {
        reservoir.colorTravel = candidate.colorTravel;
        reservoir.metadata = candidate.metadata;
        reservoir.control = candidate.control;
        reservoir.reservoirGuide = candidate.reservoirGuide;
        reservoir.statistics.y = targetPdf;
        reservoir.sampleData = candidate.sampleData;
        reservoir.sampleKey = candidate.sampleKey;
    }
}

void restirCombineReservoir(
    inout TubeFieldReservoir reservoir,
    TubeFieldReservoir candidate,
    float targetPdf,
    float randomValue)
{
    if (!restirReservoirValid(candidate) || targetPdf <= 0.0)
    {
        return;
    }

    float sourceM = max(candidate.statistics.z, 1.0);
    float risWeight = targetPdf * max(candidate.statistics.x, 0.0) * sourceM;
    reservoir.statistics.x += risWeight;
    reservoir.statistics.z += sourceM;
    if (randomValue * max(reservoir.statistics.x, 0.000001) < risWeight)
    {
        reservoir.colorTravel = candidate.colorTravel;
        reservoir.metadata = candidate.metadata;
        reservoir.control = candidate.control;
        reservoir.reservoirGuide = candidate.reservoirGuide;
        reservoir.statistics.y = targetPdf;
        reservoir.sampleData = candidate.sampleData;
        reservoir.sampleKey = candidate.sampleKey;
    }
}

void restirFinalize(inout TubeFieldReservoir reservoir)
{
    float denominator = reservoir.statistics.y * max(reservoir.statistics.z, 1.0);
    reservoir.statistics.w = denominator > 0.0 ? reservoir.statistics.x / denominator : 0.0;
}

bool restirSameField(TubeFieldReservoir a, TubeFieldReservoir b)
{
    return abs(a.metadata.x - b.metadata.x) < 0.001;
}

bool restirCompatibleOpaqueShift(TubeFieldReservoir center, TubeFieldReservoir shifted)
{
    if (!restirReservoirValid(center) || !restirReservoirValid(shifted) || !restirSameField(center, shifted))
    {
        return false;
    }

    float centerCoverage = saturate(center.control.x);
    float shiftedCoverage = saturate(shifted.control.x);
    if (shiftedCoverage <= 0.004)
    {
        return false;
    }

    float depthTolerance = max(0.025, center.control.z * 0.25);
    bool sameSurface = abs(center.colorTravel.w - shifted.colorTravel.w) <= depthTolerance;
    bool closerSurface = shifted.colorTravel.w <= center.colorTravel.w + depthTolerance;
    return sameSurface || (closerSurface && shiftedCoverage >= centerCoverage * 0.25);
}

float4 projectWorldToPixelAndDepth(float3 worldPoint)
{
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);
    float3 view = worldToCameraSpace(worldPoint, right, up, forward);
    float4 projected = projectCameraSpace(view);
    return float4(ndcToPixel(projected.xy), projected.z, view.z);
}

bool evaluateTubeFieldSegmentCandidate(uint segmentIndex, float2 pixel, out TubeFieldReservoir candidate, out float targetPdf)
{
    candidate = emptyTubeFieldReservoir();
    targetPdf = 0.0;
    TubeFieldSegment segment = RestirSegments[segmentIndex];
    float4 startProjected = projectWorldToPixelAndDepth(segment.startRadius.xyz);
    float4 endProjected = projectWorldToPixelAndDepth(segment.endFeather.xyz);
    float4 previousProjected = projectWorldToPixelAndDepth(segment.previousRadius.xyz);
    float4 nextProjected = projectWorldToPixelAndDepth(segment.nextAlpha.xyz);
    if (!finite4(startProjected) || !finite4(endProjected) || !finite4(previousProjected) || !finite4(nextProjected))
    {
        return false;
    }

    float startRadiusPx = splineRadiusToPixels(segment.startRadius.w, startProjected.w);
    float endRadiusPx = splineRadiusToPixels(segment.previousRadius.w, endProjected.w);
    float previousRadiusPx = splineRadiusToPixels(segment.previousRadius.w, previousProjected.w);
    float nextRadiusPx = splineRadiusToPixels(segment.startRadius.w, nextProjected.w);
    if (!finite1(startRadiusPx) || !finite1(endRadiusPx) || !finite1(previousRadiusPx) || !finite1(nextRadiusPx))
    {
        return false;
    }

    float sdf;
    float closestT;
    float radiusPx;
    float2 normalPx;
    capsuleDistancePx(pixel, startProjected.xy, endProjected.xy, startRadiusPx, endRadiusPx, sdf, closestT, radiusPx, normalPx);
    float previousSdf;
    float previousT;
    float previousRadius;
    float2 previousNormal;
    capsuleDistancePx(pixel, previousProjected.xy, startProjected.xy, previousRadiusPx, startRadiusPx, previousSdf, previousT, previousRadius, previousNormal);
    if (previousSdf < sdf)
    {
        sdf = previousSdf;
        closestT = 0.0;
        radiusPx = previousRadius;
        normalPx = previousNormal;
    }

    float nextSdf;
    float nextT;
    float nextRadius;
    float2 nextNormal;
    capsuleDistancePx(pixel, endProjected.xy, nextProjected.xy, endRadiusPx, nextRadiusPx, nextSdf, nextT, nextRadius, nextNormal);
    if (nextSdf < sdf)
    {
        sdf = nextSdf;
        closestT = 1.0;
        radiusPx = nextRadius;
        normalPx = nextNormal;
    }

    float aa = max(0.75, radiusPx * max(segment.endFeather.w, 0.0));
    float coverage = 1.0 - smoothstep(0.0, aa, sdf);
    float alpha = saturate(segment.nextAlpha.w * coverage);
    if (!finite1(sdf) || !finite1(radiusPx) || !finite2(normalPx) || alpha <= 0.004)
    {
        return false;
    }

    float value = lerp(segment.color0.w, segment.color1.w, closestT);
    float3 emissionColor = lerp(segment.color0.rgb, segment.color1.rgb, closestT);
    float3 ray = rayDirectionForPixel(pixel, jitterPixels, cameraPosition, cameraTarget);
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);
    float rimBlend = saturate((sdf + radiusPx) / max(radiusPx, 0.0001));
    float frontBlend = sqrt(saturate(1.0 - rimBlend * rimBlend));
    float3 tubeNormal = normalize(((right * normalPx.x) - (up * normalPx.y)) * rimBlend - ray * frontBlend);
    float normalFacing = saturate(-dot(ray, tubeNormal));
    float glowFacing = pow(normalFacing, max(segment.material.z, 0.0001));
    float alphaFacing = pow(normalFacing, max(segment.material.w, 0.0001));
    float claimCoverage = saturate(alpha * alphaFacing);
    if (claimCoverage <= 0.004)
    {
        return false;
    }

    float emission = value * value * max(segment.material.x, 0.0);
    float3 color = emissionColor * emission * glowFacing * claimCoverage;
    float travel = lerp(startProjected.w, endProjected.w, closestT);
    float3 worldPosition = lerp(segment.startRadius.xyz, segment.endFeather.xyz, closestT);
    if (!finite3(color) || !finite3(worldPosition) || !finite1(travel) || !finite3(tubeNormal))
    {
        return false;
    }

    candidate.colorTravel = float4(color, min(travel, farDistance + 1.0));
    candidate.metadata = float4(segment.tubeData.w, tubeNormal);
    candidate.control = float4(claimCoverage, coverage, saturate(radiusPx / 32.0), value);
    candidate.reservoirGuide = float4(claimCoverage, 0.0, coverage, value);
    candidate.statistics = float4(1.0, 1.0, 1.0, 0.0);
    candidate.sampleData = float4(worldPosition, closestT);
    candidate.sampleKey = uint4(segmentIndex, 0u, 0u, 0u);
    targetPdf = max(0.0001, claimCoverage * (0.05 + dot(color, float3(0.2126, 0.7152, 0.0722))));
    return true;
}

bool shiftTubeFieldReservoirToPixel(
    TubeFieldReservoir source,
    float2 pixel,
    out TubeFieldReservoir shifted,
    out float shiftedTargetPdf)
{
    shifted = emptyTubeFieldReservoir();
    shiftedTargetPdf = 0.0;
    if (!restirReservoirValid(source))
    {
        return false;
    }

    if (!evaluateTubeFieldSegmentCandidate(source.sampleKey.x, pixel + 0.5, shifted, shiftedTargetPdf))
    {
        return false;
    }

    shifted.statistics = source.statistics;
    shifted.statistics.y = shiftedTargetPdf;
    shifted.reservoirGuide.y = max(source.reservoirGuide.y, shifted.reservoirGuide.y);
    return restirReservoirValid(shifted);
}

void writeFieldReservoirCandidateSlot(uint baseIndex, uint slot, TubeFieldReservoir reservoir)
{
    if (slot >= FieldReservoirSlotsPerPixel || !restirReservoirValid(reservoir))
    {
        return;
    }

    RestirFieldReservoirCandidates[baseIndex + slot].colorTravel = reservoir.colorTravel;
    RestirFieldReservoirCandidates[baseIndex + slot].metadata = reservoir.metadata;
    RestirFieldReservoirCandidates[baseIndex + slot].control = reservoir.control;
    RestirFieldReservoirCandidates[baseIndex + slot].reservoirGuide = reservoir.reservoirGuide;
}

bool reservoirDuplicateSample(TubeFieldReservoir a, TubeFieldReservoir b)
{
    return restirReservoirValid(a) &&
        restirReservoirValid(b) &&
        a.sampleKey.x == b.sampleKey.x &&
        abs(a.sampleData.w - b.sampleData.w) < 0.002;
}

float tubeFieldReservoirPriority(TubeFieldReservoir reservoir)
{
    if (!restirReservoirValid(reservoir))
    {
        return 1.0e20;
    }

    return reservoir.colorTravel.w - saturate(reservoir.control.x) * 0.025;
}

void insertTubeFieldExportCandidate(
    TubeFieldReservoir candidate,
    inout TubeFieldReservoir slot0,
    inout TubeFieldReservoir slot1,
    inout TubeFieldReservoir slot2,
    inout TubeFieldReservoir slot3,
    inout float priority0,
    inout float priority1,
    inout float priority2,
    inout float priority3)
{
    float priority = tubeFieldReservoirPriority(candidate);
    if (priority >= 1.0e19 ||
        reservoirDuplicateSample(candidate, slot0) ||
        reservoirDuplicateSample(candidate, slot1) ||
        reservoirDuplicateSample(candidate, slot2) ||
        reservoirDuplicateSample(candidate, slot3))
    {
        return;
    }

    if (priority < priority0)
    {
        slot3 = slot2;
        priority3 = priority2;
        slot2 = slot1;
        priority2 = priority1;
        slot1 = slot0;
        priority1 = priority0;
        slot0 = candidate;
        priority0 = priority;
    }
    else if (priority < priority1)
    {
        slot3 = slot2;
        priority3 = priority2;
        slot2 = slot1;
        priority2 = priority1;
        slot1 = candidate;
        priority1 = priority;
    }
    else if (priority < priority2)
    {
        slot3 = slot2;
        priority3 = priority2;
        slot2 = candidate;
        priority2 = priority;
    }
    else if (priority < priority3)
    {
        slot3 = candidate;
        priority3 = priority;
    }
}

[numthreads(256, 1, 1)]
void D3D12TubeFieldRestirClearTilesCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint2 tiles = restirTileDimensions();
    uint tileCount = tiles.x * tiles.y;
    if (dispatchThreadId.x < tileCount)
    {
        RestirTileCounts[dispatchThreadId.x] = 0u;
    }
}

[numthreads(256, 1, 1)]
void D3D12TubeFieldRestirClearReservoirsCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint pixelCount = restirPixelCount();
    if (dispatchThreadId.x < pixelCount)
    {
        RestirInitialReservoirs[dispatchThreadId.x] = emptyTubeFieldReservoir();
    }
}

[numthreads(256, 1, 1)]
void D3D12TubeFieldRestirBinSegmentsCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint localSegment = dispatchThreadId.x;
    uint dispatchSegments = (uint)round(tubeDispatch.w);
    if (localSegment >= dispatchSegments)
    {
        return;
    }

    uint segmentIndex = (uint)round(tubeDispatch.z) + localSegment;
    TubeFieldSegment segment = RestirSegments[segmentIndex];
    float4 startProjected = projectWorldToPixelAndDepth(segment.startRadius.xyz);
    float4 endProjected = projectWorldToPixelAndDepth(segment.endFeather.xyz);
    if (!finite4(startProjected) || !finite4(endProjected))
    {
        return;
    }

    if (startProjected.w <= 0.0 && endProjected.w <= 0.0)
    {
        return;
    }

    float radiusPx = max(
        splineRadiusToPixels(segment.startRadius.w, startProjected.w),
        splineRadiusToPixels(segment.previousRadius.w, endProjected.w));
    radiusPx = min(radiusPx, 256.0);
    if (!finite1(radiusPx))
    {
        return;
    }

    float2 minPx = floor((min(startProjected.xy, endProjected.xy) - radiusPx * 1.5) / TubeFieldRestirTileSize);
    float2 maxPx = floor((max(startProjected.xy, endProjected.xy) + radiusPx * 1.5) / TubeFieldRestirTileSize);
    if (!finite2(minPx) || !finite2(maxPx))
    {
        return;
    }

    uint2 tileDims = restirTileDimensions();
    float2 tileLimit = (float2)tileDims;
    if (maxPx.x < 0.0 || maxPx.y < 0.0 || minPx.x >= tileLimit.x || minPx.y >= tileLimit.y)
    {
        return;
    }

    uint2 tileMin = min((uint2)max(minPx, float2(0.0, 0.0)), tileDims - 1u);
    uint2 tileMax = min((uint2)max(maxPx, float2(0.0, 0.0)), tileDims - 1u);
    uint2 tileSpan = tileMax - tileMin + 1u;
    if (tileSpan.x * tileSpan.y > 256u)
    {
        return;
    }

    for (uint tileY = tileMin.y; tileY <= tileMax.y; tileY++)
    {
        for (uint tileX = tileMin.x; tileX <= tileMax.x; tileX++)
        {
            uint tileIndex = tileY * tileDims.x + tileX;
            uint slot;
            InterlockedAdd(RestirTileCounts[tileIndex], 1u, slot);
            if (slot < TubeFieldRestirMaxTileSegments)
            {
                RestirTileSegments[tileIndex * TubeFieldRestirMaxTileSegments + slot] = segmentIndex;
            }
        }
    }
}

[numthreads(8, 8, 1)]
void D3D12TubeFieldRestirInitialCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    uint2 dimensions = (uint2)max(resolution, float2(1.0, 1.0));
    if (any(pixel >= dimensions))
    {
        return;
    }

    uint pixelIndex = pixel.y * dimensions.x + pixel.x;
    uint2 tileDims = restirTileDimensions();
    uint2 tile = min(pixel / TubeFieldRestirTileSize, tileDims - 1u);
    uint tileIndex = tile.y * tileDims.x + tile.x;
    uint count = min(RestirTileCountsRead[tileIndex], TubeFieldRestirMaxTileSegments);
    TubeFieldReservoir reservoir = RestirInitialReservoirs[pixelIndex];
    float invSourcePdf = count > 0u ? (float)count : 0.0;
    uint proposalCount = min(count, TubeFieldRestirInitialCandidateCount);
    for (uint index = 0u; index < proposalCount; index++)
    {
        uint candidateSlot = count <= TubeFieldRestirInitialCandidateCount
            ? index
            : (uint)floor(restirRandom01(pixelIndex * 2246822519u + index * 3266489917u + (uint)frameIndex * 668265263u) * (float)count);
        uint segmentIndex = RestirTileSegmentsRead[tileIndex * TubeFieldRestirMaxTileSegments + min(candidateSlot, count - 1u)];
        TubeFieldReservoir candidate;
        float targetPdf;
        if (evaluateTubeFieldSegmentCandidate(segmentIndex, (float2)pixel + 0.5, candidate, targetPdf))
        {
            float randomValue = restirRandom01(pixelIndex * 1664525u + index * 1013904223u + (uint)frameIndex * 977u);
            restirStreamCandidate(reservoir, candidate, targetPdf, invSourcePdf, randomValue);
        }
    }

    restirFinalize(reservoir);
    RestirInitialReservoirs[pixelIndex] = reservoir;
}

[numthreads(8, 8, 1)]
void D3D12TubeFieldRestirTemporalCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    uint2 dimensions = (uint2)max(resolution, float2(1.0, 1.0));
    if (any(pixel >= dimensions))
    {
        return;
    }

    uint pixelIndex = pixel.y * dimensions.x + pixel.x;
    TubeFieldReservoir reservoir = RestirInitialReservoirs[pixelIndex];
    if (restirReservoirValid(reservoir))
    {
        TubeFieldReservoir previousReservoir = RestirPreviousReservoirs[pixelIndex];
        TubeFieldReservoir shiftedPreviousReservoir;
        float shiftedPreviousTargetPdf;
        if (shiftTubeFieldReservoirToPixel(
                previousReservoir,
                (float2)pixel,
                shiftedPreviousReservoir,
                shiftedPreviousTargetPdf) &&
            restirCompatibleOpaqueShift(reservoir, shiftedPreviousReservoir))
        {
            float randomValue = restirRandom01(pixelIndex * 747796405u + (uint)frameIndex * 2891336453u);
            restirCombineReservoir(reservoir, shiftedPreviousReservoir, max(shiftedPreviousTargetPdf, 0.0001), randomValue);
            restirFinalize(reservoir);
        }
    }

    RestirWriteReservoirs[pixelIndex] = reservoir;
}

[numthreads(8, 8, 1)]
void D3D12TubeFieldRestirSpatialResolveCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    uint2 dimensions = (uint2)max(resolution, float2(1.0, 1.0));
    if (any(pixel >= dimensions))
    {
        return;
    }

    uint pixelIndex = pixel.y * dimensions.x + pixel.x;
    TubeFieldReservoir reservoir = RestirReadReservoirs[pixelIndex];
    if (!restirReservoirValid(reservoir))
    {
        RestirWriteReservoirs[pixelIndex] = reservoir;
        return;
    }

    static const int2 spatialOffsets[16] =
    {
        int2(-1, 0),
        int2(1, 0),
        int2(0, -1),
        int2(0, 1),
        int2(-2, -1),
        int2(2, 1),
        int2(-1, 2),
        int2(1, -2),
        int2(-3, 1),
        int2(3, -1),
        int2(-1, -3),
        int2(1, 3),
        int2(-4, 2),
        int2(4, -2),
        int2(-2, 4),
        int2(2, -4)
    };

    [unroll]
    for (uint sampleIndex = 0u; sampleIndex < TubeFieldRestirSpatialCandidateCount; sampleIndex++)
    {
        uint offsetIndex = wangHash(pixelIndex * 9781u + sampleIndex * 6271u + (uint)frameIndex * 1709u) & 15u;
        int2 offset = spatialOffsets[offsetIndex];
        uint2 neighbor = min((uint2)max(int2(pixel) + offset, int2(0, 0)), dimensions - 1u);
        uint neighborIndex = neighbor.y * dimensions.x + neighbor.x;
        TubeFieldReservoir neighborReservoir = RestirReadReservoirs[neighborIndex];
        TubeFieldReservoir shiftedNeighborReservoir;
        float shiftedNeighborTargetPdf;
        if (shiftTubeFieldReservoirToPixel(
                neighborReservoir,
                (float2)pixel,
                shiftedNeighborReservoir,
                shiftedNeighborTargetPdf) &&
            restirCompatibleOpaqueShift(reservoir, shiftedNeighborReservoir))
        {
            float randomValue = restirRandom01(pixelIndex * 89173u + sampleIndex * 19349663u + (uint)frameIndex * 83492791u);
            restirCombineReservoir(reservoir, shiftedNeighborReservoir, max(shiftedNeighborTargetPdf, 0.0001), randomValue);
        }
    }

    restirFinalize(reservoir);
    RestirWriteReservoirs[pixelIndex] = reservoir;
    uint baseIndex = pixelIndex * FieldReservoirSlotsPerPixel;

    TubeFieldReservoir export0 = emptyTubeFieldReservoir();
    TubeFieldReservoir export1 = emptyTubeFieldReservoir();
    TubeFieldReservoir export2 = emptyTubeFieldReservoir();
    TubeFieldReservoir export3 = emptyTubeFieldReservoir();
    float priority0 = 1.0e20;
    float priority1 = 1.0e20;
    float priority2 = 1.0e20;
    float priority3 = 1.0e20;

    uint2 tileDims = restirTileDimensions();
    uint2 tile = min(pixel / TubeFieldRestirTileSize, tileDims - 1u);
    uint tileIndex = tile.y * tileDims.x + tile.x;
    uint count = min(RestirTileCountsRead[tileIndex], TubeFieldRestirMaxTileSegments);
    for (uint index = 0u; index < count; index++)
    {
        uint segmentIndex = RestirTileSegmentsRead[tileIndex * TubeFieldRestirMaxTileSegments + index];
        TubeFieldReservoir candidate;
        float targetPdf;
        if (evaluateTubeFieldSegmentCandidate(segmentIndex, (float2)pixel + 0.5, candidate, targetPdf))
        {
            insertTubeFieldExportCandidate(
                candidate,
                export0,
                export1,
                export2,
                export3,
                priority0,
                priority1,
                priority2,
                priority3);
        }
    }

    if (tubeFieldReservoirPriority(reservoir) < priority3)
    {
        insertTubeFieldExportCandidate(
            reservoir,
            export0,
            export1,
            export2,
            export3,
            priority0,
            priority1,
            priority2,
            priority3);
    }

    writeFieldReservoirCandidateSlot(baseIndex, 0u, export0);
    writeFieldReservoirCandidateSlot(baseIndex, 1u, export1);
    writeFieldReservoirCandidateSlot(baseIndex, 2u, export2);
    writeFieldReservoirCandidateSlot(baseIndex, 3u, export3);
}

TubeFieldVertexOut D3D12TubeFieldVS(TubeFieldVertexIn input)
{
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);

    bool isEndVertex = input.shapeData.y > 0.5;
    float endpointT = isEndVertex ? 1.0 : 0.0;
    float3 startView = worldToCameraSpace(input.segmentStart, right, up, forward);
    float3 endView = worldToCameraSpace(input.segmentEnd, right, up, forward);
    float3 previousView = worldToCameraSpace(input.previousPoint, right, up, forward);
    float3 nextView = worldToCameraSpace(input.nextPoint, right, up, forward);
    float3 endpointView = worldToCameraSpace(input.position, right, up, forward);
    float4 startClip = projectCameraSpace(startView);
    float4 endClip = projectCameraSpace(endView);
    float4 previousClip = projectCameraSpace(previousView);
    float4 nextClip = projectCameraSpace(nextView);
    float4 endpointClip = projectCameraSpace(endpointView);

    float2 startPx = ndcToPixel(startClip.xy);
    float2 endPx = ndcToPixel(endClip.xy);
    float2 previousPx = ndcToPixel(previousClip.xy);
    float2 nextPx = ndcToPixel(nextClip.xy);
    float2 endpointPx = ndcToPixel(endpointClip.xy);
    float2 tangent = safeDirection(endPx - startPx, float2(1.0, 0.0));
    float startRadiusPx = splineRadiusToPixels(input.radiusData.x, startView.z);
    float endRadiusPx = splineRadiusToPixels(input.radiusData.y, endView.z);
    float previousRadiusPx = splineRadiusToPixels(input.radiusData.x, previousView.z);
    float nextRadiusPx = splineRadiusToPixels(input.radiusData.y, nextView.z);
    float endpointRadiusPx = lerp(startRadiusPx, endRadiusPx, endpointT);
    float envelopeRadiusPx = endpointRadiusPx + max(2.0, endpointRadiusPx * max(input.radiusData.z, 0.0) + 1.5);

    float2 previousSegmentPx = startPx - previousPx;
    float2 nextSegmentPx = nextPx - endPx;
    bool hasPrevious = dot(previousSegmentPx, previousSegmentPx) > 0.0001;
    bool hasNext = dot(nextSegmentPx, nextSegmentPx) > 0.0001;
    float2 joinTangent = tangent;
    if (!isEndVertex && hasPrevious)
    {
        joinTangent = safeDirection(tangent + safeDirection(previousSegmentPx, tangent), tangent);
    }
    else if (isEndVertex && hasNext)
    {
        joinTangent = safeDirection(tangent + safeDirection(nextSegmentPx, tangent), tangent);
    }

    float2 joinNormal = float2(-joinTangent.y, joinTangent.x);
    float2 expandedPx = endpointPx + joinNormal * input.shapeData.x * envelopeRadiusPx + tangent * input.shapeData.z * envelopeRadiusPx;

    TubeFieldVertexOut output;
    output.position = float4(pixelToNdc(expandedPx), endpointClip.z, 1.0);
    output.segmentStartPx = startPx;
    output.segmentEndPx = endPx;
    output.previousPx = previousPx;
    output.nextPx = nextPx;
    output.segmentRadiusPx = float2(startRadiusPx, endRadiusPx);
    output.joinRadiusPx = float2(previousRadiusPx, nextRadiusPx);
    output.segmentValidity = float4(hasPrevious ? 1.0 : 0.0, hasNext ? 1.0 : 0.0, endpointT, input.shapeData.x);
    output.segmentUv = float2(endpointT, input.shapeData.x);
    output.color = input.color;
    output.material = input.material;
    output.feather = input.radiusData.z;
    output.segmentTravel = float2(startView.z, endView.z);
    output.tubeData = input.tubeData;
    return output;
}

SceneOut D3D12TubeFieldPS(TubeFieldVertexOut input)
{
    float2 baseSamplePx = input.position.xy;
    float sdf;
    float closestT;
    float radiusPx;
    float2 normalPx;
    nearestTubeDistancePx(input, baseSamplePx, sdf, closestT, radiusPx, normalPx);

    uint jitterSalt = (uint)round(input.tubeData.w * 131.0 + input.tubeData.x * 17.0 + frameIndex * 97.0);
    float2 jitter01 = float2(
        blueNoiseAt(baseSamplePx, jitterSalt),
        blueNoiseAt(baseSamplePx + float2(37.0, 73.0), jitterSalt + 19u));
    float2 jitterPx = (jitter01 * 2.0 - 1.0) * min(1.25, max(0.25, radiusPx * 0.08));
    float2 samplePx = baseSamplePx + jitterPx;
    nearestTubeDistancePx(input, samplePx, sdf, closestT, radiusPx, normalPx);

    float aa = max(fwidth(sdf), max(0.75, radiusPx * max(input.feather, 0.0)));
    float coverage = 1.0 - smoothstep(0.0, aa, sdf);
    float sampleX = lerp(input.tubeData.y, input.tubeData.z, closestT);
    float value = SampleCurve((uint)round(input.tubeData.x), sampleX);
    float materialValue = saturate(value);
    float3 rampColor = TubeFieldRamp.SampleLevel(TubeFieldRampSampler, float2(materialValue, 0.5), 0.0).rgb;
    float3 ray = rayDirectionForPixel(baseSamplePx, jitterPixels, cameraPosition, cameraTarget);
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);
    float rimBlend = saturate((sdf + radiusPx) / max(radiusPx, 0.0001));
    float frontBlend = sqrt(saturate(1.0 - rimBlend * rimBlend));
    float3 tubeNormal = normalize(((right * normalPx.x) - (up * normalPx.y)) * rimBlend - ray * frontBlend);
    float normalFacing = saturate(-dot(ray, tubeNormal));
    float glowFacing = pow(normalFacing, max(input.material.z, 0.0001));
    float alphaFacing = pow(normalFacing, max(input.material.w, 0.0001));
    float claimCoverage = saturate(input.color.a * coverage * alphaFacing);
    if (claimCoverage <= 0.004)
    {
        discard;
    }

    bool exactTubeMaterial = input.tubeData.w < 0.0;
    float3 emissionColor = exactTubeMaterial ? float3(1.0, 0.035560537, 0.0) : rampColor;
    float emission = exactTubeMaterial ? max(input.material.x, 0.0) : materialValue * materialValue * max(input.material.x, 0.0);
    float3 color = emissionColor * emission * glowFacing * claimCoverage;
    float travel = lerp(input.segmentTravel.x, input.segmentTravel.y, closestT);

    SceneOut output;
    output.colorTravel = float4(color, min(travel, farDistance + 1.0));
    output.metadata = float4(input.tubeData.w, tubeNormal);
    output.control = float4(claimCoverage, coverage, saturate(radiusPx / 32.0), value);
    output.reservoirGuide = float4(claimCoverage, 0.0, coverage, value);
    output.depth = saturate(travel / max(farDistance, 0.0001));
    FieldReservoirCandidate candidate;
    candidate.colorTravel = output.colorTravel;
    candidate.metadata = output.metadata;
    candidate.control = output.control;
    candidate.reservoirGuide = output.reservoirGuide;
    injectFieldReservoirCandidate(candidate, baseSamplePx);
    return output;
}
