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

#include "D3D12FieldReservoir.hlsli"

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

RWStructuredBuffer<FieldReservoirSample> FieldReservoirCandidates : register(u14);
RWByteAddressBuffer FieldReservoirLocks : register(u15);

uint PositiveModulo(int value, uint modulo)
{
    int m = (int)max(modulo, 1u);
    int r = value % m;
    return (uint)(r < 0 ? r + m : r);
}

uint SampleAddressWithOffset(uint logicalColumn, uint sampleIndex, int rollingOffset)
{
    uint width = max((uint)round(tubeShape.x), 1u);
    uint height = max((uint)round(tubeShape.y), 1u);
    uint firstColumn = (uint)max(round(tubeShape.w), 0.0);
    uint columnStride = max((uint)round(tubeColumns.y), 1u);
    uint rollingModulo = (uint)max(round(tubeColumns.z), 0.0);
    uint physicalColumn = firstColumn + logicalColumn * columnStride;
    if (rollingModulo > 0u)
    {
        physicalColumn = PositiveModulo((int)physicalColumn + rollingOffset, rollingModulo);
    }

    physicalColumn = min(physicalColumn, height - 1u);
    uint strideBytes = max((uint)round(tubeShape.z), 4u);
    return (physicalColumn * width + min(sampleIndex, width - 1u)) * strideBytes;
}

uint PhysicalColumnWithOffset(uint logicalColumn, int rollingOffset)
{
    uint height = max((uint)round(tubeShape.y), 1u);
    uint firstColumn = (uint)max(round(tubeShape.w), 0.0);
    uint columnStride = max((uint)round(tubeColumns.y), 1u);
    uint rollingModulo = (uint)max(round(tubeColumns.z), 0.0);
    uint physicalColumn = firstColumn + logicalColumn * columnStride;
    if (rollingModulo > 0u)
    {
        physicalColumn = PositiveModulo((int)physicalColumn + rollingOffset, rollingModulo);
    }

    return min(physicalColumn, height - 1u);
}

uint SampleAddress(uint logicalColumn, uint sampleIndex)
{
    return SampleAddressWithOffset(logicalColumn, sampleIndex, (int)round(tubeColumns.w));
}

float RawSampleWithOffset(uint logicalColumn, int sampleIndex, int rollingOffset)
{
    uint width = max((uint)round(tubeShape.x), 1u);
    uint clamped = (uint)clamp(sampleIndex, 0, (int)width - 1);
    return asfloat(TubeFieldSamples.Load(SampleAddressWithOffset(logicalColumn, clamped, rollingOffset)));
}

float RawSample(uint logicalColumn, int sampleIndex)
{
    return RawSampleWithOffset(logicalColumn, sampleIndex, (int)round(tubeColumns.w));
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

float FilteredSampleWithOffset(uint logicalColumn, float x, int rollingOffset)
{
    int center = (int)floor(x + 0.5);
    float s0 = RawSampleWithOffset(logicalColumn, center - 2, rollingOffset);
    float s1 = RawSampleWithOffset(logicalColumn, center - 1, rollingOffset);
    float s2 = RawSampleWithOffset(logicalColumn, center, rollingOffset);
    float s3 = RawSampleWithOffset(logicalColumn, center + 1, rollingOffset);
    float s4 = RawSampleWithOffset(logicalColumn, center + 2, rollingOffset);
    return (s0 + s4 + 4.0 * (s1 + s3) + 6.0 * s2) / 16.0;
}


float NormalizedSample(uint logicalColumn, float x)
{
    float value = FilteredSample(logicalColumn, x);
    return saturate((value - tubeAmplitude.z) / max(tubeAmplitude.w - tubeAmplitude.z, 0.0001));
}

float NormalizedSampleWithOffset(uint logicalColumn, float x, int rollingOffset)
{
    float value = FilteredSampleWithOffset(logicalColumn, x, rollingOffset);
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

float SampleCurveWithOffset(uint logicalColumn, float x, int rollingOffset)
{
    int i1 = (int)floor(x);
    float t = frac(x);
    float p0 = NormalizedSampleWithOffset(logicalColumn, (float)(i1 - 1), rollingOffset);
    float p1 = NormalizedSampleWithOffset(logicalColumn, (float)i1, rollingOffset);
    float p2 = NormalizedSampleWithOffset(logicalColumn, (float)(i1 + 1), rollingOffset);
    float p3 = NormalizedSampleWithOffset(logicalColumn, (float)(i1 + 2), rollingOffset);
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

float3 TubePointWithOffset(uint logicalColumn, float x, int rollingOffset)
{
    float y = pow(SampleCurveWithOffset(logicalColumn, x, rollingOffset), max(tubeAmplitude.x, 0.0001)) * tubeAmplitude.y;
    return tubeOrigin + tubeAxisStep * x + tubeColumnStep * logicalColumn + float3(0.0, y, 0.0);
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
    TubeFieldVertices[vertexBase + 0u] = MakeTubeVertex(start, previous, start, end, next, -1.0, 0.0, -1.0, v0, v1, rampColor0, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 1u] = MakeTubeVertex(start, previous, start, end, next, 1.0, 0.0, -1.0, v0, v1, rampColor0, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 2u] = MakeTubeVertex(end, previous, start, end, next, -1.0, 1.0, 1.0, v0, v1, rampColor1, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 3u] = MakeTubeVertex(end, previous, start, end, next, 1.0, 1.0, 1.0, v0, v1, rampColor1, logicalColumn, x0, x1);
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
    nointerpolation float3 segmentStartWorld : TEXCOORD0;
    nointerpolation float3 segmentEndWorld : TEXCOORD1;
    nointerpolation float3 previousWorld : TEXCOORD2;
    nointerpolation float3 nextWorld : TEXCOORD3;
    nointerpolation float2 segmentRadiusWorld : TEXCOORD4;
    nointerpolation float2 joinRadiusWorld : TEXCOORD5;
    nointerpolation float4 segmentValidity : TEXCOORD6;
    nointerpolation float2 segmentStartPx : TEXCOORD7;
    nointerpolation float2 segmentEndPx : TEXCOORD8;
    nointerpolation float2 previousPx : TEXCOORD9;
    nointerpolation float2 nextPx : TEXCOORD10;
    nointerpolation float2 segmentRadiusPx : TEXCOORD11;
    nointerpolation float2 joinRadiusPx : TEXCOORD12;
    float2 segmentUv : TEXCOORD13;
    float4 color : COLOR;
    float4 material : TEXCOORD14;
    nointerpolation float feather : TEXCOORD15;
    nointerpolation float4 tubeData : TEXCOORD16;
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

struct TubeAnalyticHit
{
    bool hit;
    float travel;
    float axisT;
    float radius;
    float3 worldPosition;
    float3 normal;
};

TubeAnalyticHit emptyTubeAnalyticHit()
{
    TubeAnalyticHit hit;
    hit.hit = false;
    hit.travel = farDistance + 1.0;
    hit.axisT = 0.0;
    hit.radius = 0.0;
    hit.worldPosition = 0.0;
    hit.normal = 0.0;
    return hit;
}

void acceptTubeAnalyticHit(inout TubeAnalyticHit best, TubeAnalyticHit candidate)
{
    if (candidate.hit && candidate.travel > 0.0 && candidate.travel < best.travel)
    {
        best = candidate;
    }
}

TubeAnalyticHit intersectSphereTubeCap(float3 rayOrigin, float3 rayDirection, float3 center, float radius, float axisT)
{
    TubeAnalyticHit hit = emptyTubeAnalyticHit();
    float3 oc = rayOrigin - center;
    float b = dot(oc, rayDirection);
    float c = dot(oc, oc) - radius * radius;
    float discriminant = b * b - c;
    if (discriminant < 0.0)
    {
        return hit;
    }

    float root = sqrt(discriminant);
    float travel = -b - root;
    if (travel <= 0.0)
    {
        travel = -b + root;
    }

    if (travel <= 0.0 || travel > farDistance)
    {
        return hit;
    }

    float3 worldPosition = rayOrigin + rayDirection * travel;
    hit.hit = true;
    hit.travel = travel;
    hit.axisT = axisT;
    hit.radius = radius;
    hit.worldPosition = worldPosition;
    hit.normal = normalize(worldPosition - center);
    return hit;
}

TubeAnalyticHit intersectTubeFrustum(
    float3 rayOrigin,
    float3 rayDirection,
    float3 start,
    float3 end,
    float startRadius,
    float endRadius)
{
    TubeAnalyticHit best = emptyTubeAnalyticHit();
    float3 axis = end - start;
    float axisLength = length(axis);
    if (axisLength <= 0.0001)
    {
        return intersectSphereTubeCap(rayOrigin, rayDirection, start, max(startRadius, endRadius), 0.0);
    }

    float3 axisDirection = axis / axisLength;
    float radiusSlope = (endRadius - startRadius) / axisLength;
    float3 originDelta = rayOrigin - start;
    float originAxis = dot(originDelta, axisDirection);
    float rayAxis = dot(rayDirection, axisDirection);
    float3 originPerp = originDelta - axisDirection * originAxis;
    float3 rayPerp = rayDirection - axisDirection * rayAxis;
    float radiusAtOrigin = startRadius + radiusSlope * originAxis;

    float a = dot(rayPerp, rayPerp) - radiusSlope * radiusSlope * rayAxis * rayAxis;
    float b = 2.0 * (dot(originPerp, rayPerp) - radiusAtOrigin * radiusSlope * rayAxis);
    float c = dot(originPerp, originPerp) - radiusAtOrigin * radiusAtOrigin;
    float discriminant = b * b - 4.0 * a * c;
    if (abs(a) > 0.000001 && discriminant >= 0.0)
    {
        float root = sqrt(discriminant);
        float invDenominator = 0.5 / a;
        float travel0 = (-b - root) * invDenominator;
        float travel1 = (-b + root) * invDenominator;

        [unroll]
        for (uint index = 0u; index < 2u; index++)
        {
            float travel = index == 0u ? travel0 : travel1;
            float axisDistance = originAxis + travel * rayAxis;
            if (travel > 0.0 && travel <= farDistance && axisDistance >= 0.0 && axisDistance <= axisLength)
            {
                float axisT = saturate(axisDistance / axisLength);
                float3 worldPosition = rayOrigin + rayDirection * travel;
                float3 center = start + axisDirection * axisDistance;
                float radius = lerp(startRadius, endRadius, axisT);
                float3 radial = worldPosition - center;
                float3 normal = normalize(radial - axisDirection * (radius * radiusSlope));
                TubeAnalyticHit candidate;
                candidate.hit = true;
                candidate.travel = travel;
                candidate.axisT = axisT;
                candidate.radius = radius;
                candidate.worldPosition = worldPosition;
                candidate.normal = normal;
                acceptTubeAnalyticHit(best, candidate);
            }
        }
    }

    acceptTubeAnalyticHit(best, intersectSphereTubeCap(rayOrigin, rayDirection, start, startRadius, 0.0));
    acceptTubeAnalyticHit(best, intersectSphereTubeCap(rayOrigin, rayDirection, end, endRadius, 1.0));
    return best;
}

TubeAnalyticHit intersectTubeNeighborhood(TubeFieldVertexOut input, float3 rayOrigin, float3 rayDirection)
{
    TubeAnalyticHit best = intersectTubeFrustum(
        rayOrigin,
        rayDirection,
        input.segmentStartWorld,
        input.segmentEndWorld,
        input.segmentRadiusWorld.x,
        input.segmentRadiusWorld.y);

    if (input.segmentValidity.x > 0.5)
    {
        TubeAnalyticHit previous = intersectTubeFrustum(
            rayOrigin,
            rayDirection,
            input.previousWorld,
            input.segmentStartWorld,
            input.joinRadiusWorld.x,
            input.segmentRadiusWorld.x);
        previous.axisT = 0.0;
        acceptTubeAnalyticHit(best, previous);
    }

    if (input.segmentValidity.y > 0.5)
    {
        TubeAnalyticHit next = intersectTubeFrustum(
            rayOrigin,
            rayDirection,
            input.segmentEndWorld,
            input.nextWorld,
            input.segmentRadiusWorld.y,
            input.joinRadiusWorld.y);
        next.axisT = 1.0;
        acceptTubeAnalyticHit(best, next);
    }

    return best;
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

void injectFieldReservoirSample(FieldReservoirSample sample, float2 pixel)
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
    FieldReservoirSample current = FieldReservoirCandidates[baseIndex + FieldReservoirRowCurrent];
    FieldReservoirCandidates[baseIndex + FieldReservoirRowCurrent] = mergeFieldReservoirVisibilityProposals(
        current,
        sample,
        fieldReservoirRandom01(clampedPixel, (uint)frameIndex, 211u + (uint)round(sample.metadata.x)),
        farDistance);

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
    output.position = float4(pixelToNdc(expandedPx + jitterPixels), endpointClip.z, 1.0);
    output.segmentStartWorld = input.segmentStart;
    output.segmentEndWorld = input.segmentEnd;
    output.previousWorld = input.previousPoint;
    output.nextWorld = input.nextPoint;
    output.segmentRadiusWorld = input.radiusData.xy;
    output.joinRadiusWorld = input.radiusData.xy;
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
    output.tubeData = input.tubeData;
    return output;
}

SceneOut D3D12TubeFieldPS(TubeFieldVertexOut input)
{
    float2 baseSamplePx = input.position.xy;
    float3 ray = rayDirectionForPixel(baseSamplePx, -jitterPixels, cameraPosition, cameraTarget);
    TubeAnalyticHit baseHit = intersectTubeNeighborhood(input, cameraPosition, ray);
    if (!baseHit.hit)
    {
        discard;
    }

    float baseSdf;
    float baseClosestT;
    float baseRadiusPx;
    float2 baseNormalPx;
    nearestTubeDistancePx(input, baseSamplePx, baseSdf, baseClosestT, baseRadiusPx, baseNormalPx);

    uint jitterSalt = (uint)round(input.tubeData.w * 131.0 + input.tubeData.x * 17.0 + frameIndex * 97.0);
    float2 jitter01 = float2(
        blueNoiseAt(baseSamplePx, jitterSalt),
        blueNoiseAt(baseSamplePx + float2(37.0, 73.0), jitterSalt + 19u));
    float2 jitterPx = (jitter01 * 2.0 - 1.0) * min(1.25, max(0.25, baseRadiusPx * 0.08));
    float2 samplePx = baseSamplePx + jitterPx;
    float jitterSdf;
    float jitterClosestT;
    float jitterRadiusPx;
    float2 jitterNormalPx;
    nearestTubeDistancePx(input, samplePx, jitterSdf, jitterClosestT, jitterRadiusPx, jitterNormalPx);

    float sdf = baseSdf;
    float closestT = baseHit.axisT;
    float radiusWorld = baseHit.radius;
    float3 tubeNormal = baseHit.normal;
    float supportAa = max(0.55, min(baseRadiusPx * 0.18, 1.75));
    float supportCoverage = 1.0 - smoothstep(0.0, supportAa, baseSdf);
    if (supportCoverage > 0.02 && jitterSdf <= supportAa)
    {
        closestT = lerp(baseHit.axisT, jitterClosestT, 0.35);
    }

    float sampleX = lerp(input.tubeData.y, input.tubeData.z, closestT);
    float value = SampleCurve((uint)round(input.tubeData.x), sampleX);
    float materialValue = saturate(value);
    float3 rampColor = TubeFieldRamp.SampleLevel(TubeFieldRampSampler, float2(materialValue, 0.5), 0.0).rgb;
    float normalFacing = saturate(-dot(ray, tubeNormal));
    float glowFacing = pow(normalFacing, max(input.material.z, 0.0001));
    float alphaFacing = pow(normalFacing, max(input.material.w, 0.0001));
    float claimCoverage = saturate(input.color.a * supportCoverage * alphaFacing);
    if (claimCoverage <= 0.004)
    {
        discard;
    }

    bool exactTubeMaterial = input.tubeData.w < 0.0;
    float3 emissionColor = exactTubeMaterial ? float3(1.0, 0.035560537, 0.0) : rampColor;
    float emission = exactTubeMaterial ? max(input.material.x, 0.0) : materialValue * materialValue * max(input.material.x, 0.0);
    float3 color = emissionColor * emission * glowFacing * claimCoverage;
    float travel = baseHit.travel;
    float4 motion = 0.0;
    int currentRollingOffset = (int)round(tubeColumns.w);
    int previousRollingOffset = (int)round(tubeDraw.w);
    float columnStride = max(round(tubeColumns.y), 1.0);
    float previousLogicalColumn = round(input.tubeData.x) + ((float)(currentRollingOffset - previousRollingOffset) / columnStride);
    float visibleColumnCount = max(round(tubeColumns.x), 1.0);
    if (previousLogicalColumn >= 0.0 && previousLogicalColumn < visibleColumnCount)
    {
        uint previousLogical = (uint)round(previousLogicalColumn);
        float3 previousWorld = TubePointWithOffset(previousLogical, sampleX, previousRollingOffset);
        float2 previousUv = projectWorldToPreviousHistoryUv(previousWorld);
        float expectedPreviousTravel = distance(previousCameraPosition, previousWorld);
        motion = float4(previousUv, expectedPreviousTravel, 1.0);
    }

    SceneOut output;
    output.colorTravel = float4(color, min(travel, farDistance + 1.0));
    float candidateFieldId = abs(input.tubeData.w) + (float)PhysicalColumnWithOffset((uint)round(input.tubeData.x), currentRollingOffset) * 0.01;
    output.metadata = float4(candidateFieldId, tubeNormal);
    output.control = float4(claimCoverage, supportCoverage, saturate(radiusWorld / max(viewRadius, 0.0001)), value);
    output.reservoirGuide = float4(claimCoverage, 0.0, supportCoverage, value);
    output.depth = saturate(travel / max(farDistance, 0.0001));
    float target = fieldReservoirDefaultTarget(output.colorTravel, output.control, output.reservoirGuide);
    float selectedColumn = round(input.tubeData.x);
    float2 selectedUv = (baseSamplePx + 0.5) / max(resolution, float2(1.0, 1.0));
    float2 selectedProducerCoord = float2(selectedColumn, sampleX);
    float2 supportFootprintPx = max(float2(baseRadiusPx, baseRadiusPx), float2(0.5, 0.5));
    float shiftKind = motion.w > 0.5 ? FieldShiftKindExplicitMotion : FieldShiftKindNone;
    FieldReservoirSample sample = makeFieldReservoirSample(
        output.colorTravel,
        output.metadata,
        output.control,
        output.reservoirGuide,
        motion,
        float4(saturate(selectedUv), selectedProducerCoord),
        float4(supportFootprintPx, FieldDomainKindTube, shiftKind),
        target,
        1.0,
        1.0,
        FieldProposalKindDeterministicStructural);
    injectFieldReservoirSample(sample, baseSamplePx);
    return output;
}
