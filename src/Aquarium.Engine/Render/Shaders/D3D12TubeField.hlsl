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
};

cbuffer TubeFieldConstants : register(b3)
{
    float4 tubeShape;
    float4 tubeColumns;
    float4 tubeAmplitude;
    float4 tubeMaterial;
    float4 tubeDispatch;
    float3 tubeOrigin;
    float tubePadding0;
    float3 tubeAxisStep;
    float tubePadding1;
    float3 tubeColumnStep;
    float tubePadding2;
};

ByteAddressBuffer TubeFieldSamples : register(t42);
RWStructuredBuffer<TubeFieldVertex> TubeFieldVertices : register(u10);
RWStructuredBuffer<uint> TubeFieldIndices : register(u11);
RWStructuredBuffer<uint> TubeFieldStats : register(u12);

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
    float normalized = saturate((value - tubeAmplitude.z) / max(tubeAmplitude.w - tubeAmplitude.z, 0.0001));
    return pow(normalized, max(tubeAmplitude.x, 0.0001));
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

float3 TubePoint(uint logicalColumn, float x)
{
    float value = SampleCurve(logicalColumn, x);
    return tubeOrigin +
        tubeAxisStep * x +
        tubeColumnStep * (float)logicalColumn +
        float3(0.0, value * tubeAmplitude.y, 0.0);
}

TubeFieldVertex MakeTubeVertex(float3 position, float3 previous, float3 start, float3 end, float3 next, float side, float endpointT, float capSign, float value)
{
    TubeFieldVertex vertex;
    float radius = max(tubeMaterial.x + value * tubeMaterial.y, 0.0001);
    float3 rampColor = lerp(float3(0.0, 0.0, 0.0), float3(1.0, 0.62, 0.18), value);
    vertex.position = position;
    vertex.segmentStart = start;
    vertex.segmentEnd = end;
    vertex.previousPoint = previous;
    vertex.nextPoint = next;
    vertex.shapeData = float4(side, endpointT, capSign, 0.0);
    vertex.radiusData = float4(radius, radius, tubeMaterial.w, 0.0);
    vertex.color = float4(rampColor, tubeMaterial.z);
    vertex.material = float4(max(value * value * tubeDispatch.x, 0.0), tubeMaterial.z, 0.55, 0.55);
    return vertex;
}

[numthreads(256, 1, 1)]
void D3D12TubeFieldExpandCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint localSegment = dispatchThreadId.x;
    uint dispatchSegments = (uint)round(tubeDispatch.w);
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
    TubeFieldVertices[vertexBase + 0u] = MakeTubeVertex(start, previous, start, end, next, -1.0, 0.0, -1.0, v0);
    TubeFieldVertices[vertexBase + 1u] = MakeTubeVertex(start, previous, start, end, next, 1.0, 0.0, -1.0, v0);
    TubeFieldVertices[vertexBase + 2u] = MakeTubeVertex(end, previous, start, end, next, -1.0, 1.0, 1.0, v1);
    TubeFieldVertices[vertexBase + 3u] = MakeTubeVertex(end, previous, start, end, next, 1.0, 1.0, 1.0, v1);
    TubeFieldIndices[indexBase + 0u] = vertexBase + 0u;
    TubeFieldIndices[indexBase + 1u] = vertexBase + 1u;
    TubeFieldIndices[indexBase + 2u] = vertexBase + 2u;
    TubeFieldIndices[indexBase + 3u] = vertexBase + 2u;
    TubeFieldIndices[indexBase + 4u] = vertexBase + 1u;
    TubeFieldIndices[indexBase + 5u] = vertexBase + 3u;
    TubeFieldStats[0] = globalSegment + 1u;
}
