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

TubeFieldVertex MakeTubeVertex(float3 position, float3 previous, float3 start, float3 end, float3 next, float side, float endpointT, float capSign, float value, uint logicalColumn, float x0, float x1)
{
    TubeFieldVertex vertex;
    float radius = max(tubeMaterial.x + value * tubeMaterial.y, 0.0001);
    float3 rampColor = value.xxx;
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
    float3 previous = TubePoint(logicalColumn, xPrev);
    float3 start = TubePoint(logicalColumn, x0);
    float3 end = TubePoint(logicalColumn, x1);
    float3 next = TubePoint(logicalColumn, xNext);

    uint globalSegment = (uint)round(tubeDispatch.z) + localSegment;
    uint vertexBase = globalSegment * 4u;
    uint indexBase = globalSegment * 6u;
    TubeFieldVertices[vertexBase + 0u] = MakeTubeVertex(start, previous, start, end, next, -1.0, 0.0, -1.0, v0, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 1u] = MakeTubeVertex(start, previous, start, end, next, 1.0, 0.0, -1.0, v0, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 2u] = MakeTubeVertex(end, previous, start, end, next, -1.0, 1.0, 1.0, v1, logicalColumn, x0, x1);
    TubeFieldVertices[vertexBase + 3u] = MakeTubeVertex(end, previous, start, end, next, 1.0, 1.0, 1.0, v1, logicalColumn, x0, x1);
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
    float2 ndc = uv * 2.0 - 1.0;
    ndc.y = -ndc.y;
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(camera, target, forward, right, up);
    float aspect = resolution.x / max(resolution.y, 1.0);
    float halfHeight = max(viewRadius, 0.0001);
    float halfWidth = halfHeight * aspect;
    float3 targetPoint = target + right * ndc.x * halfWidth + up * ndc.y * halfHeight;
    return normalize(targetPoint - camera);
}

float4 projectCameraSpace(float3 view)
{
    float z = max(view.z, 0.0001);
    float x = view.x / max(cameraFrustumXy.y * z, 0.0001);
    float y = view.y / max(cameraFrustumXy.w * z, 0.0001);
    float depth = saturate((z - cameraFrustumZ.x) / max(cameraFrustumZ.y - cameraFrustumZ.x, 0.0001));
    return float4(x, y, depth, 1.0);
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
    return output;
}
