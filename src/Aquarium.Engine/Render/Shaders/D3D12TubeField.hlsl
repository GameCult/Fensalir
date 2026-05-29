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

struct TubeFieldColumn
{
    float4 shape;
    float4 columns;
    float4 amplitude;
    float4 material;
    float4 dispatchDraw;
    float4 originField;
    float4 axisStepEmission;
    float4 columnStep;
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
RWStructuredBuffer<TubeFieldColumn> TubeFieldColumns : register(u16);

StructuredBuffer<TubeFieldColumn> RestirColumns : register(t46);
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
static const uint TubeFieldRestirSpatialTileLanes = 16u;
static const uint TubeFieldRestirDepthLanesPerSpatialLane = 1u;
static const uint TubeFieldRestirResidentTileCandidates = TubeFieldRestirSpatialTileLanes * TubeFieldRestirDepthLanesPerSpatialLane;
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

uint ColumnSampleAddress(TubeFieldColumn column, uint sampleIndex)
{
    uint width = max((uint)round(column.shape.x), 1u);
    uint height = max((uint)round(column.shape.y), 1u);
    uint firstColumn = (uint)max(round(column.shape.w), 0.0);
    uint columnStride = max((uint)round(column.columns.y), 1u);
    uint rollingModulo = (uint)max(round(column.columns.z), 0.0);
    int rollingOffset = (int)round(column.columns.w);
    uint logicalColumn = (uint)max(round(column.dispatchDraw.x), 0.0);
    uint physicalColumn = firstColumn + logicalColumn * columnStride;
    if (rollingModulo > 0u)
    {
        physicalColumn = PositiveModulo((int)physicalColumn + rollingOffset, rollingModulo);
    }

    physicalColumn = min(physicalColumn, height - 1u);
    uint strideBytes = max((uint)round(column.shape.z), 4u);
    return (physicalColumn * width + min(sampleIndex, width - 1u)) * strideBytes;
}

float RawSample(uint logicalColumn, int sampleIndex)
{
    uint width = max((uint)round(tubeShape.x), 1u);
    uint clamped = (uint)clamp(sampleIndex, 0, (int)width - 1);
    return asfloat(TubeFieldSamples.Load(SampleAddress(logicalColumn, clamped)));
}

float RawColumnSample(TubeFieldColumn column, int sampleIndex)
{
    uint width = max((uint)round(column.shape.x), 1u);
    uint clamped = (uint)clamp(sampleIndex, 0, (int)width - 1);
    return asfloat(TubeFieldSamples.Load(ColumnSampleAddress(column, clamped)));
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

float FilteredColumnSample(TubeFieldColumn column, float x)
{
    int center = (int)floor(x + 0.5);
    float s0 = RawColumnSample(column, center - 2);
    float s1 = RawColumnSample(column, center - 1);
    float s2 = RawColumnSample(column, center);
    float s3 = RawColumnSample(column, center + 1);
    float s4 = RawColumnSample(column, center + 2);
    return (s0 + s4 + 4.0 * (s1 + s3) + 6.0 * s2) / 16.0;
}

float NormalizedSample(uint logicalColumn, float x)
{
    float value = FilteredSample(logicalColumn, x);
    return saturate((value - tubeAmplitude.z) / max(tubeAmplitude.w - tubeAmplitude.z, 0.0001));
}

float NormalizedColumnSample(TubeFieldColumn column, float x)
{
    float value = FilteredColumnSample(column, x);
    return saturate((value - column.amplitude.z) / max(column.amplitude.w - column.amplitude.z, 0.0001));
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

float SampleColumnCurve(TubeFieldColumn column, float x)
{
    int i1 = (int)floor(x);
    float t = frac(x);
    float p0 = NormalizedColumnSample(column, (float)(i1 - 1));
    float p1 = NormalizedColumnSample(column, (float)i1);
    float p2 = NormalizedColumnSample(column, (float)(i1 + 1));
    float p3 = NormalizedColumnSample(column, (float)(i1 + 2));
    return saturate(Catmull(p0, p1, p2, p3, t));
}

float SampleAmplitudeCurve(uint logicalColumn, float x)
{
    return pow(SampleCurve(logicalColumn, x), max(tubeAmplitude.x, 0.0001));
}

float SampleColumnAmplitudeCurve(TubeFieldColumn column, float x)
{
    return pow(SampleColumnCurve(column, x), max(column.amplitude.x, 0.0001));
}

float3 TubePoint(uint logicalColumn, float x)
{
    float value = SampleAmplitudeCurve(logicalColumn, x);
    return tubeOrigin +
        tubeAxisStep * x +
        tubeColumnStep * (float)logicalColumn +
        float3(0.0, value * tubeAmplitude.y, 0.0);
}

float3 TubeColumnPoint(TubeFieldColumn column, float x)
{
    float value = SampleColumnAmplitudeCurve(column, x);
    return column.originField.xyz +
        column.axisStepEmission.xyz * x +
        float3(0.0, value * column.amplitude.y, 0.0);
}

TubeFieldVertex MakeTubeVertex(float3 position, float3 previous, float3 start, float3 end, float3 next, float side, float endpointT, float capSign, float v0, float v1, float3 rampColor, uint logicalColumn, float x0, float x1)
{
    TubeFieldVertex vertex;
    float radius0 = max(tubeMaterial.x + v0 * tubeMaterial.y, 0.0001);
    float radius1 = max(tubeMaterial.x + v1 * tubeMaterial.y, 0.0001);
    vertex.position = position;
    vertex.segmentStart = start;
    vertex.segmentEnd = end;
    vertex.previousPoint = previous;
    vertex.nextPoint = next;
    vertex.shapeData = float4(side, endpointT, capSign, 0.0);
    vertex.radiusData = float4(radius0, radius1, tubeMaterial.w, 0.0);
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
    segment.endFeather = float4(end, radius1);
    segment.nextAlpha = float4(next, radius1);
    segment.color0 = float4(rampColor0, v0);
    segment.color1 = float4(rampColor1, v1);
    segment.material = float4(max(tubeDispatch.x, 0.0), tubeMaterial.z, 4.0, tubeMaterial.w);
    segment.tubeData = float4((float)logicalColumn, x0, x1, tubeDraw.z);
    return segment;
}

[numthreads(256, 1, 1)]
void D3D12TubeFieldExpandCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint localColumn = dispatchThreadId.x;
    uint dispatchColumns = (uint)round(tubeDispatch.w);
    if (localColumn == 0u)
    {
        uint argumentBase = (uint)round(tubeDraw.x);
        TubeFieldDrawArguments[argumentBase + 0u] = 0u;
        TubeFieldDrawArguments[argumentBase + 1u] = 1u;
        TubeFieldDrawArguments[argumentBase + 2u] = (uint)round(tubeDraw.y);
        TubeFieldDrawArguments[argumentBase + 3u] = 0u;
        TubeFieldDrawArguments[argumentBase + 4u] = 0u;
    }

    if (localColumn >= dispatchColumns)
    {
        return;
    }

    uint globalColumn = (uint)round(tubeDispatch.z) + localColumn;
    TubeFieldColumn column;
    column.shape = tubeShape;
    column.columns = tubeColumns;
    column.amplitude = tubeAmplitude;
    column.material = tubeMaterial;
    column.dispatchDraw = float4((float)localColumn, max(tubeDispatch.x, 0.0), tubeDraw.z, tubeDispatch.y);
    column.originField = float4(tubeOrigin + tubeColumnStep * (float)localColumn, tubeDraw.z);
    column.axisStepEmission = float4(tubeAxisStep, max(tubeDispatch.x, 0.0));
    column.columnStep = float4(tubeColumnStep, 0.0);
    TubeFieldColumns[globalColumn] = column;
    TubeFieldStats[0] = globalColumn + 1u;
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

uint restirTileCandidateSlot(uint pixelIndex, uint tileIndex, uint count, uint sampleIndex, uint salt)
{
    if (count <= 1u)
    {
        return 0u;
    }

    uint seed = pixelIndex * 2246822519u ^
        tileIndex * 3266489917u ^
        sampleIndex * 668265263u ^
        (uint)frameIndex * 374761393u ^
        salt;
    return wangHash(seed) % count;
}

uint packRestirTileSegment(uint segmentIndex, float travel)
{
    uint depthKey = (uint)round(saturate(travel / max(farDistance, 0.0001)) * 65534.0);
    return (depthKey << 16) | (segmentIndex & 0xffffu);
}

bool unpackRestirTileSegment(uint packed, out uint segmentIndex)
{
    segmentIndex = packed & 0xffffu;
    return packed != 0xffffffffu;
}

void insertRestirTileSegment(uint tileIndex, uint spatialLane, uint packed)
{
    uint candidate = packed;
    [unroll]
    for (uint depthLane = 0u; depthLane < TubeFieldRestirDepthLanesPerSpatialLane; depthLane++)
    {
        uint slotIndex = tileIndex * TubeFieldRestirMaxTileSegments +
            spatialLane * TubeFieldRestirDepthLanesPerSpatialLane +
            depthLane;
        uint previous;
        InterlockedMin(RestirTileSegments[slotIndex], candidate, previous);
        if (candidate == previous)
        {
            return;
        }

        if (candidate > previous)
        {
            continue;
        }

        if (previous == 0xffffffffu)
        {
            return;
        }

        candidate = previous;
    }
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

bool evaluateTubeFieldColumnCandidate(uint columnIndex, float2 pixel, out TubeFieldReservoir candidate, out float targetPdf)
{
    candidate = emptyTubeFieldReservoir();
    targetPdf = 0.0;
    TubeFieldColumn column = RestirColumns[columnIndex];
    uint width = max((uint)round(column.shape.x), 2u);
    float xMax = (float)(width - 1u);
    float4 startProjected = projectWorldToPixelAndDepth(TubeColumnPoint(column, 0.0));
    float4 endProjected = projectWorldToPixelAndDepth(TubeColumnPoint(column, xMax));
    if (!finite4(startProjected) || !finite4(endProjected))
    {
        return false;
    }

    float sdf;
    float closestT;
    float radiusPx;
    float2 normalPx;
    float2 axisPx = endProjected.xy - startProjected.xy;
    float axisLength2 = max(dot(axisPx, axisPx), 0.0001);
    float xCenter = saturate(dot(pixel - startProjected.xy, axisPx) / axisLength2) * xMax;
    float sampleStep = max(0.5, xMax / 32.0);
    float x0 = clamp(xCenter - sampleStep, 0.0, xMax);
    float x1 = clamp(xCenter + sampleStep, 0.0, xMax);
    float v0 = SampleColumnCurve(column, x0);
    float v1 = SampleColumnCurve(column, x1);
    float3 p0 = TubeColumnPoint(column, x0);
    float3 p1 = TubeColumnPoint(column, x1);
    float4 projected0 = projectWorldToPixelAndDepth(p0);
    float4 projected1 = projectWorldToPixelAndDepth(p1);
    if (!finite4(projected0) || !finite4(projected1))
    {
        return false;
    }

    float r0 = splineRadiusToPixels(max(column.material.x + v0 * column.material.y, 0.0001), projected0.w);
    float r1 = splineRadiusToPixels(max(column.material.x + v1 * column.material.y, 0.0001), projected1.w);
    float localT;
    capsuleDistancePx(pixel, projected0.xy, projected1.xy, r0, r1, sdf, localT, radiusPx, normalPx);
    closestT = saturate(lerp(x0, x1, localT) / max(xMax, 0.0001));

    float aa = max(0.75, radiusPx * max(column.material.w, 0.0));
    float coverage = 1.0 - smoothstep(0.0, aa, sdf);
    float alpha = saturate(column.material.z * coverage);
    if (!finite1(sdf) || !finite1(radiusPx) || !finite2(normalPx) || alpha <= 0.004)
    {
        return false;
    }

    float x = closestT * xMax;
    float value = SampleColumnCurve(column, x);
    float3 emissionColor = TubeFieldRamp.SampleLevel(TubeFieldRampSampler, float2(saturate(value), 0.5), 0.0).rgb;
    float3 ray = rayDirectionForPixel(pixel, float2(0.0, 0.0), cameraPosition, cameraTarget);
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);
    float rimBlend = saturate((sdf + radiusPx) / max(radiusPx, 0.0001));
    float frontBlend = sqrt(saturate(1.0 - rimBlend * rimBlend));
    float3 tubeNormal = normalize(((right * normalPx.x) - (up * normalPx.y)) * rimBlend - ray * frontBlend);
    float normalFacing = saturate(-dot(ray, tubeNormal));
    float glowFacing = pow(normalFacing, 4.0);
    float alphaFacing = 1.0;
    float claimCoverage = saturate(alpha * alphaFacing);
    if (claimCoverage <= 0.004)
    {
        return false;
    }

    float emission = value * value * max(column.dispatchDraw.y, 0.0);
    float3 color = emissionColor * emission * glowFacing * claimCoverage;
    float3 worldPosition = TubeColumnPoint(column, x);
    float travel = distance(cameraPosition, worldPosition);
    if (!finite3(color) || !finite3(worldPosition) || !finite1(travel) || !finite3(tubeNormal))
    {
        return false;
    }

    candidate.colorTravel = float4(color, min(travel, farDistance + 1.0));
    candidate.metadata = float4(column.originField.w, tubeNormal);
    candidate.control = float4(claimCoverage, coverage, saturate(radiusPx / 32.0), value);
    candidate.reservoirGuide = float4(claimCoverage, 0.0, coverage, value);
    candidate.statistics = float4(1.0, 1.0, 1.0, 0.0);
    candidate.sampleData = float4(worldPosition, closestT);
    candidate.sampleKey = uint4(columnIndex, 0u, 0u, 0u);
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

    if (!evaluateTubeFieldColumnCandidate(source.sampleKey.x, pixel + 0.5 + jitterPixels, shifted, shiftedTargetPdf))
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

    uint tileSegmentCount = tileCount * TubeFieldRestirMaxTileSegments;
    if (dispatchThreadId.x < tileSegmentCount)
    {
        RestirTileSegments[dispatchThreadId.x] = 0xffffffffu;
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
    uint localColumn = dispatchThreadId.x;
    uint dispatchColumns = (uint)round(tubeDispatch.w);
    if (localColumn >= dispatchColumns)
    {
        return;
    }

    uint columnIndex = (uint)round(tubeDispatch.z) + localColumn;
    TubeFieldColumn column = RestirColumns[columnIndex];
    uint width = max((uint)round(column.shape.x), 2u);
    float xMax = (float)(width - 1u);
    float4 startProjected = projectWorldToPixelAndDepth(TubeColumnPoint(column, 0.0));
    float4 endProjected = projectWorldToPixelAndDepth(TubeColumnPoint(column, xMax));
    if (!finite4(startProjected) || !finite4(endProjected))
    {
        return;
    }

    if (startProjected.w <= 0.0 && endProjected.w <= 0.0)
    {
        return;
    }

    float startValue = SampleColumnCurve(column, 0.0);
    float endValue = SampleColumnCurve(column, xMax);
    float radiusPx = max(
        splineRadiusToPixels(max(column.material.x + startValue * column.material.y, 0.0001), startProjected.w),
        splineRadiusToPixels(max(column.material.x + endValue * column.material.y, 0.0001), endProjected.w));
    float2 minProjected = min(startProjected.xy, endProjected.xy);
    float2 maxProjected = max(startProjected.xy, endProjected.xy);
    radiusPx = min(radiusPx, 256.0);
    float segmentTravel = min(startProjected.w, endProjected.w);
    if (!finite1(radiusPx) || !finite1(segmentTravel))
    {
        return;
    }

    float2 minPx = floor((minProjected - radiusPx * 1.5) / TubeFieldRestirTileSize);
    float2 maxPx = floor((maxProjected + radiusPx * 1.5) / TubeFieldRestirTileSize);
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
            uint packed = packRestirTileSegment(columnIndex, segmentTravel);
            [unroll(4)]
            for (uint laneY = 0u; laneY < 4u; laneY++)
            {
                [unroll(4)]
                for (uint laneX = 0u; laneX < 4u; laneX++)
                {
                    insertRestirTileSegment(tileIndex, laneY * 4u + laneX, packed);
                }
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
    TubeFieldReservoir reservoir = RestirInitialReservoirs[pixelIndex];
    uint touchedSegmentCount = RestirTileCountsRead[tileIndex];
    if (touchedSegmentCount == 0u)
    {
        RestirInitialReservoirs[pixelIndex] = reservoir;
        return;
    }

    uint count = TubeFieldRestirResidentTileCandidates;
    uint2 localPixelInTile = pixel - tile * TubeFieldRestirTileSize;
    uint laneX = min(localPixelInTile.x / 4u, 3u);
    uint laneY = min(localPixelInTile.y / 4u, 3u);
    uint candidateSlot = min(laneY * 4u + laneX, count - 1u);
    uint columnIndex;
    if (unpackRestirTileSegment(RestirTileSegmentsRead[tileIndex * TubeFieldRestirMaxTileSegments + candidateSlot], columnIndex))
    {
        TubeFieldReservoir candidate;
        float targetPdf;
        if (evaluateTubeFieldColumnCandidate(columnIndex, (float2)pixel + 0.5 + jitterPixels, candidate, targetPdf))
        {
            restirStreamCandidate(reservoir, candidate, targetPdf, (float)count, restirRandom01(pixelIndex * 1664525u + (uint)frameIndex * 977u));
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
    TubeFieldReservoir previousReservoir = RestirPreviousReservoirs[pixelIndex];
    TubeFieldReservoir shiftedPreviousReservoir;
    float shiftedPreviousTargetPdf;
    if (shiftTubeFieldReservoirToPixel(
            previousReservoir,
            (float2)pixel,
            shiftedPreviousReservoir,
            shiftedPreviousTargetPdf))
    {
        if (!restirReservoirValid(reservoir))
        {
            reservoir = shiftedPreviousReservoir;
            reservoir.statistics.y = max(shiftedPreviousTargetPdf, 0.0001);
            restirFinalize(reservoir);
        }
        else if (restirCompatibleOpaqueShift(reservoir, shiftedPreviousReservoir))
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

    [unroll(4)]
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
                shiftedNeighborTargetPdf))
        {
            if (!restirReservoirValid(reservoir))
            {
                reservoir = shiftedNeighborReservoir;
                reservoir.statistics.y = max(shiftedNeighborTargetPdf, 0.0001);
            }
            else if (restirCompatibleOpaqueShift(reservoir, shiftedNeighborReservoir))
            {
                float randomValue = restirRandom01(pixelIndex * 89173u + sampleIndex * 19349663u + (uint)frameIndex * 83492791u);
                restirCombineReservoir(reservoir, shiftedNeighborReservoir, max(shiftedNeighborTargetPdf, 0.0001), randomValue);
            }
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
    uint touchedSegmentCount = RestirTileCountsRead[tileIndex];
    if (touchedSegmentCount > 0u)
    {
        uint count = TubeFieldRestirResidentTileCandidates;
        uint2 localPixelInTile = pixel - tile * TubeFieldRestirTileSize;
        uint laneX = min(localPixelInTile.x / 4u, 3u);
        uint laneY = min(localPixelInTile.y / 4u, 3u);
        uint candidateSlot = min(laneY * 4u + laneX, count - 1u);
        uint columnIndex;
        if (unpackRestirTileSegment(RestirTileSegmentsRead[tileIndex * TubeFieldRestirMaxTileSegments + candidateSlot], columnIndex))
        {
            TubeFieldReservoir candidate;
            float targetPdf;
            if (evaluateTubeFieldColumnCandidate(columnIndex, (float2)pixel + 0.5 + jitterPixels, candidate, targetPdf))
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
