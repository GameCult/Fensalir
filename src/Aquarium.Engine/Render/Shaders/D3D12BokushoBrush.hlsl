#include "CultMath/CultMath.hlsl"

static const float BOKUSHO_PAGE_DENSITY_SCALE = 65535.0;
static const float BOKUSHO_PAGE_PATCH_TRANSFER_GAIN = 0.0044;
static const float BOKUSHO_PAGE_PATCH_TANGENT_RADIUS_SCALE = 0.32;
static const float BOKUSHO_PAGE_PATCH_NORMAL_RADIUS_SCALE = 0.32;
static const float BOKUSHO_PAGE_PATCH_MINIMUM_RADIUS = 0.001;
static const float BOKUSHO_PAGE_PATCH_WORLD_QUANTUM = 1.0 / 1024.0;
static const float BOKUSHO_BRISTLE_TANGENT_WORLD_QUANTUM = 1.0 / 4096.0;
static const float BOKUSHO_STROKE_RHYTHM_QUANTUM = 1.0 / 4096.0;
static const uint BOKUSHO_PAGE_PATCH_INK_UNITS = 2048u;
static const uint BOKUSHO_PAGE_PATCH_COVERAGE_UNITS = 1024u;
static const uint BOKUSHO_PAGE_PATCH_TRANSFER_ENCODED_GAIN = 288u;
static const uint BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM = 8u;

cbuffer BokushoBrushConstants : register(b4)
{
    float4 brushShape;     // sampleCount, tuftCount, physicsHz, strokeCount
    float4 brushMaterial;  // radius, pressure, inkLoad, wetness
    float4 brushDynamics;  // splay, bend, friction, reserved
    float4 brushProfile;   // radiusScale, pressureScale, normalScale, tangentScale
    float4 strokeP0;
    float4 strokeP1;
    float4 strokeP2;
    float4 strokeP3;
    float4 pageView;       // center.xy, radius, pageSize
};

struct BokushoBrushStroke
{
    float4 profile;
    float4 dynamics;
    float4 pose;
    float4 p0;
    float4 p1;
    float4 p2;
    float4 p3;
};

RWStructuredBuffer<float> BokushoTraceField : register(u20);
RWStructuredBuffer<float> BokushoCanvasField : register(u21);
RWStructuredBuffer<float4> BokushoTipField : register(u22);
RWStructuredBuffer<uint> BokushoPageDensityField : register(u23);
StructuredBuffer<BokushoBrushStroke> BokushoBrushStrokes : register(t78);
StructuredBuffer<float4> BokushoSourcePoints : register(t81);

float QuantizeScalar(float value, float quantum)
{
    return quantum > 0.0 ? floor((value / quantum) + 0.5) * quantum : value;
}

float2 QuantizeFloat2(float2 value, float quantum)
{
    return float2(QuantizeScalar(value.x, quantum), QuantizeScalar(value.y, quantum));
}

float QuantizePositive(float value, float quantum)
{
    return QuantizeScalar(max(value, 0.0), quantum);
}

uint QuantizePositiveUnits(float value, uint units)
{
    return (uint)floor(saturate(value) * (float)units + 0.5);
}

float2 PacketStrokePoint(BokushoBrushStroke stroke, float t)
{
    return cultmath_catmullrom(stroke.p0.xy, stroke.p1.xy, stroke.p2.xy, stroke.p3.xy, t);
}

bool TrySampleSourcePoint(BokushoBrushStroke stroke, float sourceT, out float2 sampledPoint)
{
    sampledPoint = float2(0.0, 0.0);
    uint sourcePointCount = (uint)round(max(brushDynamics.w, 0.0));
    uint start = (uint)round(max(stroke.p1.z, 0.0));
    uint count = (uint)round(max(stroke.p1.w, 0.0));
    if (count < 2u || sourcePointCount == 0u || start >= sourcePointCount)
    {
        return false;
    }

    count = min(count, sourcePointCount - start);
    if (count < 2u)
    {
        return false;
    }

    float target = saturate(sourceT);
    float4 first = BokushoSourcePoints[start];
    if (target <= first.z)
    {
        sampledPoint = first.xy;
        return true;
    }

    uint lastIndex = start + count - 1u;
    float4 last = BokushoSourcePoints[lastIndex];
    if (target >= last.z)
    {
        sampledPoint = last.xy;
        return true;
    }

    uint low = start;
    uint high = lastIndex;
    [loop]
    while (low + 1u < high)
    {
        uint mid = (low + high) / 2u;
        if (BokushoSourcePoints[mid].z <= target)
        {
            low = mid;
        }
        else
        {
            high = mid;
        }
    }

    float4 a = BokushoSourcePoints[low];
    float4 b = BokushoSourcePoints[high];
    float span = max(b.z - a.z, 0.000001);
    float local = saturate((target - a.z) / span);
    sampledPoint = lerp(a.xy, b.xy, local);
    return true;
}

float2 StrokePoint(BokushoBrushStroke stroke, float t)
{
    float2 sampledPoint;
    float sourceT = saturate(lerp(stroke.p0.z, max(stroke.p0.z, stroke.p0.w), t));
    if (TrySampleSourcePoint(stroke, sourceT, sampledPoint))
    {
        return QuantizeFloat2(sampledPoint, BOKUSHO_BRISTLE_TANGENT_WORLD_QUANTUM);
    }

    return PacketStrokePoint(stroke, t);
}

float2 StrokeTangent(BokushoBrushStroke stroke, float t)
{
    float dt = 1.0 / max(brushShape.x - 1.0, 1.0);
    float2 before = StrokePoint(stroke, saturate(t - dt));
    float2 after = StrokePoint(stroke, saturate(t + dt));
    return cultmath_normalize(after - before);
}

float2 StrokeTangent(BokushoBrushStroke stroke, float t, uint stepCount)
{
    float dt = 1.0 / max((float)stepCount - 1.0, 1.0);
    float2 before = StrokePoint(stroke, saturate(t - dt));
    float2 after = StrokePoint(stroke, saturate(t + dt));
    return cultmath_normalize(after - before);
}

bool SameSourceBoundary(BokushoBrushStroke previous, BokushoBrushStroke next)
{
    return previous.p3.w >= 0.0 &&
        abs(previous.p3.w - next.p3.w) <= 0.5 &&
        abs(saturate(previous.p0.w) - saturate(next.p0.z)) <= 0.001;
}

float2 StrokeTangent(BokushoBrushStroke stroke, uint strokeIndex, float t, uint stepCount)
{
    float dt = 1.0 / max((float)stepCount - 1.0, 1.0);
    uint sourcePointCount = (uint)round(max(brushDynamics.w, 0.0));
    uint sourceRangeCount = (uint)round(max(stroke.p1.w, 0.0));
    if (sourcePointCount > 0u && sourceRangeCount >= 2u)
    {
        float sourceStart = saturate(stroke.p0.z);
        float sourceEnd = saturate(max(stroke.p0.z, stroke.p0.w));
        float sourceSpan = max(sourceEnd - sourceStart, 1.0 / max((float)(stepCount - 1u), 1.0));
        float sourceT = saturate(lerp(sourceStart, sourceEnd, t));
        float sourceDt = sourceSpan * dt;
        float2 sourceBefore;
        float2 sourceAfter;
        if (TrySampleSourcePoint(stroke, saturate(sourceT - sourceDt), sourceBefore) &&
            TrySampleSourcePoint(stroke, saturate(sourceT + sourceDt), sourceAfter))
        {
            sourceBefore = QuantizeFloat2(sourceBefore, BOKUSHO_BRISTLE_TANGENT_WORLD_QUANTUM);
            sourceAfter = QuantizeFloat2(sourceAfter, BOKUSHO_BRISTLE_TANGENT_WORLD_QUANTUM);
            return cultmath_normalize(sourceAfter - sourceBefore);
        }
    }

    float beforeT = t - dt;
    float afterT = t + dt;
    float2 before = StrokePoint(stroke, saturate(beforeT));
    float2 after = StrokePoint(stroke, saturate(afterT));
    uint strokeCount = max((uint)round(brushShape.w), 1u);
    if (beforeT < 0.0 && strokeIndex > 0u)
    {
        BokushoBrushStroke previous = BokushoBrushStrokes[strokeIndex - 1u];
        if (SameSourceBoundary(previous, stroke))
        {
            before = StrokePoint(previous, saturate(1.0 + beforeT));
        }
    }

    if (afterT > 1.0 && strokeIndex + 1u < strokeCount)
    {
        BokushoBrushStroke next = BokushoBrushStrokes[strokeIndex + 1u];
        if (SameSourceBoundary(stroke, next))
        {
            after = StrokePoint(next, saturate(afterT - 1.0));
        }
    }

    before = QuantizeFloat2(before, BOKUSHO_BRISTLE_TANGENT_WORLD_QUANTUM);
    after = QuantizeFloat2(after, BOKUSHO_BRISTLE_TANGENT_WORLD_QUANTUM);
    return cultmath_normalize(after - before);
}

float StrokeTaper(float t)
{
    float entry = cultmath_smoothstep(0.0, 0.10, t);
    float exit = 1.0 - cultmath_smoothstep(0.86, 1.0, t);
    return 0.04 + entry * exit * 0.96;
}

float StrokeProgress(BokushoBrushStroke stroke, float t)
{
    return saturate(lerp(stroke.p0.z, max(stroke.p0.z, stroke.p0.w), t));
}

float StrokeTaper(BokushoBrushStroke stroke, float t)
{
    float strokeT = StrokeProgress(stroke, t);
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float entry = cultmath_smoothstep(0.0, entryTaper, strokeT);
    float exit = 1.0 - cultmath_smoothstep(1.0 - exitTaper, 1.0, strokeT);
    float x = saturate(entry * exit);
    float contact = x * (0.76 + x * 0.24);
    return 0.018 + contact * 0.982;
}

uint StrokeNoiseStep(float strokeT)
{
    return (uint)floor(saturate(strokeT) * 4096.0 + 0.5);
}

float LaneCohesion(float edge, float split, float wetness, float pressure)
{
    return saturate(0.50 + (1.0 - edge) * 0.38 + wetness * 0.22 + pressure * 0.12 - split * 0.18);
}

float StrokeSegmentSpan(BokushoBrushStroke stroke, uint sampleCount)
{
    float start = saturate(stroke.p0.z);
    float endValue = saturate(max(stroke.p0.z, stroke.p0.w));
    return max(endValue - start, 1.0 / max((float)(sampleCount - 1u), 1.0));
}

float CanvasPigment(float value)
{
    return 0.96 * (1.0 - exp(-max(value, 0.0) * 0.82));
}

float4 StrokeRhythm(BokushoBrushStroke stroke, float strokeT)
{
    float entry = cultmath_smoothstep(0.0, 0.18, strokeT);
    float release = cultmath_smoothstep(0.74, 1.0, strokeT);
    float belly = entry * (1.0 - cultmath_smoothstep(0.62, 0.94, strokeT));
    float pose = saturate(abs(stroke.pose.x) * 0.34 + abs(stroke.pose.y) * 0.26 + (stroke.pose.w - 1.0) * 0.18);
    return float4(
        QuantizeScalar(0.90 + belly * (0.15 + pose * 0.04) + entry * 0.04 - release * (0.12 + pose * 0.03), BOKUSHO_STROKE_RHYTHM_QUANTUM),
        QuantizeScalar(0.94 + belly * (0.09 + pose * 0.03) - release * 0.08, BOKUSHO_STROKE_RHYTHM_QUANTUM),
        QuantizeScalar(0.96 + belly * 0.08 + pose * 0.02 - release * 0.04, BOKUSHO_STROKE_RHYTHM_QUANTUM),
        QuantizeScalar(0.94 + belly * (0.12 + pose * 0.04) - release * 0.10, BOKUSHO_STROKE_RHYTHM_QUANTUM));
}

uint EncodePageDensityContribution(float contribution)
{
    float c = saturate(contribution);
    float c2 = c * c;
    float c3 = c2 * c;
    float c4 = c2 * c2;
    float density = c + (c2 * 0.5) + (c3 * (1.0 / 3.0)) + (c4 * 0.25);
    uint encoded = (uint)floor(min(density * BOKUSHO_PAGE_DENSITY_SCALE, BOKUSHO_PAGE_DENSITY_SCALE) + 0.5);
    return BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM > 1u
        ? ((encoded + (BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM / 2u)) / BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM) * BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM
        : encoded;
}

uint EncodePageDensityContribution(uint inkUnits, uint coverageUnits)
{
    uint product = inkUnits * coverageUnits;
    uint denominator = BOKUSHO_PAGE_PATCH_INK_UNITS * BOKUSHO_PAGE_PATCH_COVERAGE_UNITS;
    uint encoded = (product * BOKUSHO_PAGE_PATCH_TRANSFER_ENCODED_GAIN + denominator / 2u) / denominator;
    return BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM > 1u
        ? ((encoded + (BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM / 2u)) / BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM) * BOKUSHO_PAGE_DENSITY_CONTRIBUTION_QUANTUM
        : encoded;
}

float PageToothHash(uint x, uint y)
{
    uint value = (x + 1u) * 0x9E3779B9u ^ (y + 1u) * 0x85EBCA6Bu ^ 0xC2B2AE35u;
    value ^= value >> 16;
    value *= 0x7FEB352Du;
    value ^= value >> 15;
    value *= 0x846CA68Bu;
    value ^= value >> 16;
    return (float)(value & 0x00FFFFFFu) / 16777215.0;
}

void DepositSweptPatch(float2 previousTip, float2 tip, float previousNormalRadius, float previousTangentRadius, float normalRadius, float tangentRadius, float pigment, float paperTooth)
{
    uint inkUnits = QuantizePositiveUnits(pigment, BOKUSHO_PAGE_PATCH_INK_UNITS);
    if (inkUnits == 0u)
    {
        return;
    }

    previousTip = QuantizeFloat2(previousTip, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    tip = QuantizeFloat2(tip, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    previousNormalRadius = QuantizePositive(previousNormalRadius, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    previousTangentRadius = QuantizePositive(previousTangentRadius, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    normalRadius = QuantizePositive(normalRadius, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    tangentRadius = QuantizePositive(tangentRadius, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    float2 sweep = tip - previousTip;
    float sweepLength = length(sweep);
    float2 tangent = sweepLength > 0.000001 ? sweep / sweepLength : float2(1.0, 0.0);
    float2 normal = float2(-tangent.y, tangent.x);
    float previousPatchTangentRadius = max(previousTangentRadius * BOKUSHO_PAGE_PATCH_TANGENT_RADIUS_SCALE, BOKUSHO_PAGE_PATCH_MINIMUM_RADIUS);
    float previousPatchNormalRadius = max(previousNormalRadius * BOKUSHO_PAGE_PATCH_NORMAL_RADIUS_SCALE, BOKUSHO_PAGE_PATCH_MINIMUM_RADIUS);
    float currentPatchTangentRadius = max(tangentRadius * BOKUSHO_PAGE_PATCH_TANGENT_RADIUS_SCALE, BOKUSHO_PAGE_PATCH_MINIMUM_RADIUS);
    float currentPatchNormalRadius = max(normalRadius * BOKUSHO_PAGE_PATCH_NORMAL_RADIUS_SCALE, BOKUSHO_PAGE_PATCH_MINIMUM_RADIUS);
    float patchTangentRadius = max(previousPatchTangentRadius, currentPatchTangentRadius);
    float patchNormalRadius = max(previousPatchNormalRadius, currentPatchNormalRadius);
    float2 center = (previousTip + tip) * 0.5;
    float halfSweepLength = sweepLength * 0.5;
    float2 extents = float2(
        abs(tangent.x) * (halfSweepLength + patchTangentRadius) + abs(normal.x) * patchNormalRadius,
        abs(tangent.y) * (halfSweepLength + patchTangentRadius) + abs(normal.y) * patchNormalRadius);
    float pageSize = max(pageView.w, 1.0);
    float2 minUv = (center - extents - pageView.xy) / max(pageView.z, 0.001) * 0.5 + 0.5;
    float2 maxUv = (center + extents - pageView.xy) / max(pageView.z, 0.001) * 0.5 + 0.5;
    int minX = clamp((int)floor(min(minUv.x, maxUv.x) * (pageSize - 1.0)), 0, (int)pageSize - 1);
    int maxX = clamp((int)ceil(max(minUv.x, maxUv.x) * (pageSize - 1.0)), 0, (int)pageSize - 1);
    int minY = clamp((int)floor(min(minUv.y, maxUv.y) * (pageSize - 1.0)), 0, (int)pageSize - 1);
    int maxY = clamp((int)ceil(max(minUv.y, maxUv.y) * (pageSize - 1.0)), 0, (int)pageSize - 1);

    [loop]
    for (int y = minY; y <= maxY; y += 1)
    {
        float uvY = pageSize <= 1.0 ? 0.0 : (float)y / (pageSize - 1.0);
        [loop]
        for (int x = minX; x <= maxX; x += 1)
        {
            float uvX = pageSize <= 1.0 ? 0.0 : (float)x / (pageSize - 1.0);
            float2 world = pageView.xy + (float2(uvX, uvY) * 2.0 - 1.0) * pageView.z;
            float2 local = world - center;
            float axialExcess = max(abs(dot(local, tangent)) - halfSweepLength, 0.0);
            float tangentDistance = axialExcess / patchTangentRadius;
            float normalDistance = dot(local, normal) / patchNormalRadius;
            float ellipse = sqrt(tangentDistance * tangentDistance + normalDistance * normalDistance);
            float coverage = cultmath_smoothstep(1.0, 0.0, ellipse);
            float tooth = PageToothHash((uint)x, (uint)y);
            float interiorTooth = (1.0 - cultmath_smoothstep(0.0, 0.34, ellipse)) * saturate(paperTooth);
            coverage *= 1.0 - interiorTooth * (0.03 + tooth * 0.10);
            float edgeTooth = cultmath_smoothstep(0.38, 1.0, ellipse) * saturate(paperTooth);
            if (edgeTooth > 0.0)
            {
                coverage *= 1.0 - edgeTooth * (0.18 + tooth * 0.34);
            }

            uint coverageUnits = QuantizePositiveUnits(coverage, BOKUSHO_PAGE_PATCH_COVERAGE_UNITS);
            if (coverageUnits == 0u)
            {
                continue;
            }

            InterlockedAdd(BokushoPageDensityField[(uint)(y * (int)pageSize + x)], EncodePageDensityContribution(inkUnits, coverageUnits));
        }
    }
}

float LaneHash(uint strokeIndex, uint tuft, uint salt)
{
    uint value = (strokeIndex + 1u) * 0x9E3779B9u ^ (tuft + 1u) * 0x85EBCA6Bu ^ salt;
    value ^= value >> 16;
    value *= 0x7FEB352Du;
    value ^= value >> 15;
    value *= 0x846CA68Bu;
    value ^= value >> 16;
    return (float)(value & 0x00FFFFFFu) / 16777215.0;
}

uint StrokeChainStart(uint strokeIndex)
{
    uint chainStart = strokeIndex;
    float expectedStart = saturate(BokushoBrushStrokes[strokeIndex].p0.z);
    float sourceStrokeId = BokushoBrushStrokes[strokeIndex].p3.w;
    [loop]
    while (chainStart > 0u && expectedStart > 0.0001)
    {
        if (sourceStrokeId >= 0.0 && abs(BokushoBrushStrokes[chainStart - 1u].p3.w - sourceStrokeId) > 0.5)
        {
            break;
        }

        float previousEnd = saturate(BokushoBrushStrokes[chainStart - 1u].p0.w);
        if (abs(previousEnd - expectedStart) > 0.001)
        {
            break;
        }

        chainStart = chainStart - 1u;
        expectedStart = saturate(BokushoBrushStrokes[chainStart].p0.z);
    }

    return chainStart;
}

void SimulateBokushoTuftSegment(
    BokushoBrushStroke stroke,
    uint outputStrokeIndex,
    uint laneKey,
    uint tuft,
    uint sampleCount,
    uint tuftCount,
    float restOffset,
    float edge,
    inout float offset,
    inout float2 tip,
    inout float stateLoad,
    inout float stateWet,
    bool writeOutput)
{
    float pressure = saturate(brushMaterial.y * stroke.profile.y * 0.5) * 2.0;
    float splay = saturate(brushDynamics.x / 2.0);
    float bend = saturate(brushDynamics.y / 2.4);
    float friction = saturate(brushDynamics.z);
    float radius = max(brushMaterial.x * stroke.profile.x, 0.0001);
    float normalRadius = max(radius * stroke.profile.z, 0.0001);
    float tangentRadius = max(radius * stroke.profile.w, 0.0001);
    float segmentSpan = StrokeSegmentSpan(stroke, sampleCount);
    float integrationSpan = segmentSpan / max((float)sampleCount - 1.0, 1.0);
    float segmentVelocityScale = 1.0 / segmentSpan;
    float split = saturate((0.28 - LaneHash(laneKey, tuft, 53u)) * 3.0) * saturate((edge - 0.20) * 1.5) * stroke.dynamics.w;

    [loop]
    for (uint sample = 0u; sample < sampleCount; sample += 1u)
    {
        float t = sampleCount <= 1u ? 0.0 : (float)sample / (float)(sampleCount - 1u);
        float2 center = StrokePoint(stroke, t);
        float2 tangent = StrokeTangent(stroke, outputStrokeIndex, t, sampleCount);
        float2 normal = float2(-tangent.y, tangent.x);
        float strokeT = StrokeProgress(stroke, t);
        float taper = StrokeTaper(stroke, t);
        float4 rhythm = StrokeRhythm(stroke, strokeT);
        float localPressure = pressure * taper * rhythm.x;
        float laneCore = cultmath_smoothstep(0.0, 0.78, 1.0 - edge);
        float cohesion = saturate(0.28 + stateWet * 0.44 + laneCore * 0.24 + localPressure * 0.10 - split * 0.18);
        float localNormalRadius = max(normalRadius * rhythm.y * (0.18 + taper * 0.82) * (0.72 + splay * 0.34 + localPressure * 0.16 - cohesion * 0.08), 0.0001);
        float localTangentRadius = max(tangentRadius * rhythm.z * (0.24 + taper * 0.76) * (0.86 + bend * 0.18), 0.0001);
        float targetOffset = (restOffset + stroke.pose.y * 0.12) * localNormalRadius * (0.58 + splay * 0.34 + localPressure * 0.18 - cohesion * 0.20);
        float recovery = saturate(0.05 + stroke.pose.w * 0.08 + stateWet * 0.08 + localPressure * 0.10);
        offset = cultmath_lerp(offset, targetOffset, recovery);

        float poseLead = stroke.pose.x * 0.36 + stroke.pose.y * 0.16;
        float lag = localTangentRadius * (0.10 + bend * 0.32 + friction * localPressure * 0.16 + edge * 0.06);
        float2 dragVector = cultmath_normalize(tangent + normal * poseLead);
        float2 desiredTip = center + normal * offset - dragVector * lag;
        float2 slip = desiredTip - tip;
        float velocity = length(slip) * brushShape.z * segmentVelocityScale / max(radius, 0.001);
        float contact = saturate(localPressure * stateLoad * (0.24 + stateWet * 0.62 + laneCore * 0.18));
        float drag = contact * friction * (0.28 + stateWet * 0.34);
        float2 previousTip = tip;
        tip = tip + slip * (0.18 + recovery * 0.82) * (1.0 - drag * 0.52);
        float sweptDistance = length(tip - previousTip);
        float sweptPatch = saturate(sweptDistance / max(localTangentRadius, 0.001) * 0.42);

        float tension = saturate(velocity * 0.014 + abs(targetOffset - offset) / max(localNormalRadius, 0.001) * 0.22 + edge * 0.12);
        float separation = saturate(split * 0.34 + tension * 0.46 + edge * 0.20 - cohesion * 0.24);
        float adhesion = saturate(stateWet * (0.36 + cohesion * 0.40) + localPressure * 0.10 - separation * 0.22);
        float dryMemory = saturate((1.0 - stateWet) * 0.62 + separation * 0.32 + velocity * 0.004);
        float fiberNoise = LaneHash(laneKey + StrokeNoiseStep(strokeT) * 13u, tuft, 101u);
        float continuity = 1.0 - cultmath_smoothstep(0.18 + dryMemory * 0.28, 0.96, fiberNoise) * dryMemory * (0.38 + edge * 0.22);
        float inkFilm = saturate(stateLoad * (0.28 + stateWet * 0.34));
        float bristleTooth = (0.62 + continuity * (0.30 + inkFilm * 0.08)) * (0.82 + inkFilm * 0.24 - edge * 0.06);
        float contactTransfer = contact * stateLoad * (0.16 + stateWet * 0.92) * (0.30 + drag * 0.64 + localPressure * 0.22 + sweptPatch * 0.18) * (0.68 + laneCore * 0.50 - separation * 0.14) * bristleTooth;
        float airborneRelease = (1.0 - contact) * stateLoad * stateWet * saturate(velocity * 0.010 - adhesion * 0.16) * (0.20 + separation * 0.42 + edge * 0.18);
        float consumedPigment = contactTransfer + airborneRelease;
        stateLoad = max(0.0, stateLoad - consumedPigment * integrationSpan * (0.014 + localPressure * 0.009 + dryMemory * 0.004));
        stateWet = max(0.0, stateWet - consumedPigment * integrationSpan * (0.006 + dryMemory * 0.003));

        if (writeOutput)
        {
            uint index = ((outputStrokeIndex * tuftCount) + tuft) * sampleCount + sample;
            float pigment = (contactTransfer * (0.95 + localPressure * 0.34 + sweptPatch * 0.24) + airborneRelease * (1.6 + velocity * 0.002)) * stroke.dynamics.z * rhythm.w;
            float localPigment = CanvasPigment(pigment);
            float trace = saturate(contact * 0.70 + airborneRelease * 2.0 + stateLoad * 0.18);
            BokushoTraceField[index] = trace;
            BokushoCanvasField[index] = localPigment;
            BokushoTipField[index] = float4(
                tip,
                localNormalRadius * (0.74 + inkFilm * 0.10 + laneCore * 0.20 + localPressure * 0.14 + sweptPatch * 0.08 - separation * 0.10),
                localTangentRadius * (0.86 + inkFilm * 0.06 + bend * 0.22 + drag * 0.16) + sweptDistance * 0.36);
        }
    }
}

void SimulateBokushoTuftSegmentToPage(
    BokushoBrushStroke stroke,
    uint strokeIndex,
    uint laneKey,
    uint tuft,
    uint sampleCount,
    float restOffset,
    float edge,
    inout float offset,
    inout float2 tip,
    inout float stateLoad,
    inout float stateWet,
    inout float previousPatchNormalRadius,
    inout float previousPatchTangentRadius,
    bool writeOutput,
    bool writeInitialStep)
{
    float pressure = saturate(brushMaterial.y * stroke.profile.y * 0.5) * 2.0;
    float splay = saturate(brushDynamics.x / 2.0);
    float bend = saturate(brushDynamics.y / 2.4);
    float friction = saturate(brushDynamics.z);
    float radius = max(brushMaterial.x * stroke.profile.x, 0.0001);
    float normalRadius = max(radius * stroke.profile.z, 0.0001);
    float tangentRadius = max(radius * stroke.profile.w, 0.0001);
    float segmentSpan = StrokeSegmentSpan(stroke, sampleCount);
    uint stepCount = min(max((uint)ceil(segmentSpan * brushShape.z), 2u), 4096u);
    float integrationSpan = segmentSpan / max((float)stepCount - 1.0, 1.0);
    float segmentVelocityScale = 1.0 / segmentSpan;
    float split = saturate((0.28 - LaneHash(laneKey, tuft, 53u)) * 3.0) * saturate((edge - 0.20) * 1.5) * stroke.dynamics.w;

    [loop]
    for (uint step = 0u; step < stepCount; step += 1u)
    {
        float t = stepCount <= 1u ? 0.0 : (float)step / (float)(stepCount - 1u);
        float2 center = StrokePoint(stroke, t);
        float2 tangent = StrokeTangent(stroke, strokeIndex, t, stepCount);
        float2 normal = float2(-tangent.y, tangent.x);
        float strokeT = StrokeProgress(stroke, t);
        float taper = StrokeTaper(stroke, t);
        float4 rhythm = StrokeRhythm(stroke, strokeT);
        float localPressure = pressure * taper * rhythm.x;
        float laneCore = cultmath_smoothstep(0.0, 0.78, 1.0 - edge);
        float cohesion = saturate(0.28 + stateWet * 0.44 + laneCore * 0.24 + localPressure * 0.10 - split * 0.18);
        float localNormalRadius = max(normalRadius * rhythm.y * (0.18 + taper * 0.82) * (0.72 + splay * 0.34 + localPressure * 0.16 - cohesion * 0.08), 0.0001);
        float localTangentRadius = max(tangentRadius * rhythm.z * (0.24 + taper * 0.76) * (0.86 + bend * 0.18), 0.0001);
        float targetOffset = (restOffset + stroke.pose.y * 0.12) * localNormalRadius * (0.58 + splay * 0.34 + localPressure * 0.18 - cohesion * 0.20);
        float recovery = saturate(0.05 + stroke.pose.w * 0.08 + stateWet * 0.08 + localPressure * 0.10);
        offset = cultmath_lerp(offset, targetOffset, recovery);

        float poseLead = stroke.pose.x * 0.36 + stroke.pose.y * 0.16;
        float lag = localTangentRadius * (0.10 + bend * 0.32 + friction * localPressure * 0.16 + edge * 0.06);
        float2 dragVector = cultmath_normalize(tangent + normal * poseLead);
        float2 desiredTip = center + normal * offset - dragVector * lag;
        float2 slip = desiredTip - tip;
        float velocity = length(slip) * brushShape.z * segmentVelocityScale / max(radius, 0.001);
        float contact = saturate(localPressure * stateLoad * (0.24 + stateWet * 0.62 + laneCore * 0.18));
        float drag = contact * friction * (0.28 + stateWet * 0.34);
        float2 previousTip = tip;
        tip = tip + slip * (0.18 + recovery * 0.82) * (1.0 - drag * 0.52);
        float sweptDistance = length(tip - previousTip);
        float sweptPatch = saturate(sweptDistance / max(localTangentRadius, 0.001) * 0.42);

        float tension = saturate(velocity * 0.014 + abs(targetOffset - offset) / max(localNormalRadius, 0.001) * 0.22 + edge * 0.12);
        float separation = saturate(split * 0.34 + tension * 0.46 + edge * 0.20 - cohesion * 0.24);
        float adhesion = saturate(stateWet * (0.36 + cohesion * 0.40) + localPressure * 0.10 - separation * 0.22);
        float dryMemory = saturate((1.0 - stateWet) * 0.62 + separation * 0.32 + velocity * 0.004);
        float fiberNoise = LaneHash(laneKey + StrokeNoiseStep(strokeT) * 13u, tuft, 101u);
        float continuity = 1.0 - cultmath_smoothstep(0.18 + dryMemory * 0.28, 0.96, fiberNoise) * dryMemory * (0.38 + edge * 0.22);
        float inkFilm = saturate(stateLoad * (0.28 + stateWet * 0.34));
        float bristleTooth = (0.62 + continuity * (0.30 + inkFilm * 0.08)) * (0.82 + inkFilm * 0.24 - edge * 0.06);
        float paperTooth = saturate(dryMemory * 0.42 + separation * 0.22 + edge * 0.10 + (1.0 - inkFilm) * 0.18);
        float contactTransfer = contact * stateLoad * (0.16 + stateWet * 0.92) * (0.30 + drag * 0.64 + localPressure * 0.22 + sweptPatch * 0.18) * (0.68 + laneCore * 0.50 - separation * 0.14) * bristleTooth;
        float airborneRelease = (1.0 - contact) * stateLoad * stateWet * saturate(velocity * 0.010 - adhesion * 0.16) * (0.20 + separation * 0.42 + edge * 0.18);
        float consumedPigment = contactTransfer + airborneRelease;
        stateLoad = max(0.0, stateLoad - consumedPigment * integrationSpan * (0.014 + localPressure * 0.009 + dryMemory * 0.004));
        stateWet = max(0.0, stateWet - consumedPigment * integrationSpan * (0.006 + dryMemory * 0.003));

        if (writeOutput && (writeInitialStep || step > 0u))
        {
            float pigment = (contactTransfer * (0.95 + localPressure * 0.34 + sweptPatch * 0.24) + airborneRelease * (1.6 + velocity * 0.002)) * stroke.dynamics.z * rhythm.w;
            float patchNormalRadius = localNormalRadius * (0.74 + inkFilm * 0.10 + laneCore * 0.20 + localPressure * 0.14 + sweptPatch * 0.08 - separation * 0.10);
            float patchTangentRadius = localTangentRadius * (0.86 + inkFilm * 0.06 + bend * 0.22 + drag * 0.16) + sweptDistance * 0.36;
            float sweepStartNormalRadius = previousPatchNormalRadius > 0.0 ? previousPatchNormalRadius : patchNormalRadius;
            float sweepStartTangentRadius = previousPatchTangentRadius > 0.0 ? previousPatchTangentRadius : patchTangentRadius;
            DepositSweptPatch(previousTip, tip, sweepStartNormalRadius, sweepStartTangentRadius, patchNormalRadius, patchTangentRadius, pigment, paperTooth);
        }

        previousPatchNormalRadius = localNormalRadius * (0.74 + inkFilm * 0.10 + laneCore * 0.20 + localPressure * 0.14 + sweptPatch * 0.08 - separation * 0.10);
        previousPatchTangentRadius = localTangentRadius * (0.86 + inkFilm * 0.06 + bend * 0.22 + drag * 0.16) + sweptDistance * 0.36;
    }
}

[numthreads(128, 1, 1)]
void D3D12BokushoBrushCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint sampleCount = max((uint)round(brushShape.x), 2u);
    uint tuftCount = max((uint)round(brushShape.y), 1u);
    uint strokeCount = max((uint)round(brushShape.w), 1u);
    uint globalIndex = dispatchThreadId.x;
    uint strokeIndex = globalIndex / tuftCount;
    uint tuft = globalIndex - strokeIndex * tuftCount;
    if (strokeIndex >= strokeCount || tuft >= tuftCount)
    {
        return;
    }

    BokushoBrushStroke stroke = BokushoBrushStrokes[strokeIndex];
    uint chainStart = StrokeChainStart(strokeIndex);
    BokushoBrushStroke seedStroke = BokushoBrushStrokes[chainStart];
    float laneT = tuftCount <= 1u ? 0.5 : (float)tuft / (float)(tuftCount - 1u);
    float restOffset = laneT * 2.0 - 1.0;
    float edge = abs(restOffset);
    float laneHash = LaneHash(chainStart, tuft, 17u);
    float laneLoad = 0.76 + laneHash * 0.34;
    float seedPressure = saturate(brushMaterial.y * seedStroke.profile.y * 0.5) * 2.0;
    float wetness = saturate(brushMaterial.w / 1.6);
    float load = clamp(brushMaterial.z / 1.05, 0.0, 2.7);
    float splay = saturate(brushDynamics.x / 2.0);
    float seedRadius = max(brushMaterial.x * seedStroke.profile.x, 0.0001);
    float seedNormalRadius = max(seedRadius * seedStroke.profile.z, 0.0001);
    float seedSplit = saturate((0.28 - LaneHash(chainStart, tuft, 53u)) * 3.0) * saturate((edge - 0.20) * 1.5) * seedStroke.dynamics.w;
    float initialCohesion = LaneCohesion(edge, seedSplit, wetness, seedPressure);
    float2 tip = StrokePoint(seedStroke, 0.0);
    float poseBias = seedStroke.pose.x * 0.18 + seedStroke.pose.y * 0.08;
    float offset = (restOffset + poseBias * (1.0 - edge * 0.35)) * seedNormalRadius * (0.42 + splay * 0.38) + (laneHash - 0.5) * seedNormalRadius * 0.05;
    float stateLoad = load * (0.86 + initialCohesion * 0.48) * (1.0 - edge * 0.08) * laneLoad * (1.0 - seedSplit * 0.18);
    float stateWet = wetness * (0.74 + initialCohesion * 0.24 - seedSplit * 0.08);

    [loop]
    for (uint replayStrokeIndex = chainStart; replayStrokeIndex < strokeIndex; replayStrokeIndex += 1u)
    {
        SimulateBokushoTuftSegment(BokushoBrushStrokes[replayStrokeIndex], replayStrokeIndex, chainStart, tuft, sampleCount, tuftCount, restOffset, edge, offset, tip, stateLoad, stateWet, false);
    }

    SimulateBokushoTuftSegment(stroke, strokeIndex, chainStart, tuft, sampleCount, tuftCount, restOffset, edge, offset, tip, stateLoad, stateWet, true);
}

[numthreads(128, 1, 1)]
void D3D12BokushoPageClearCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint pageValueCount = (uint)(max(pageView.w, 1.0) * max(pageView.w, 1.0));
    uint index = dispatchThreadId.x;
    if (index < pageValueCount)
    {
        BokushoPageDensityField[index] = 0u;
    }
}

[numthreads(128, 1, 1)]
void D3D12BokushoPageDepositCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint sampleCount = max((uint)round(brushShape.x), 2u);
    uint tuftCount = max((uint)round(brushShape.y), 1u);
    uint strokeCount = max((uint)round(brushShape.w), 1u);
    uint globalIndex = dispatchThreadId.x;
    uint strokeIndex = globalIndex / tuftCount;
    uint tuft = globalIndex - strokeIndex * tuftCount;
    if (strokeIndex >= strokeCount || tuft >= tuftCount)
    {
        return;
    }

    BokushoBrushStroke stroke = BokushoBrushStrokes[strokeIndex];
    uint chainStart = StrokeChainStart(strokeIndex);
    BokushoBrushStroke seedStroke = BokushoBrushStrokes[chainStart];
    float laneT = tuftCount <= 1u ? 0.5 : (float)tuft / (float)(tuftCount - 1u);
    float restOffset = laneT * 2.0 - 1.0;
    float edge = abs(restOffset);
    float laneHash = LaneHash(chainStart, tuft, 17u);
    float laneLoad = 0.76 + laneHash * 0.34;
    float seedPressure = saturate(brushMaterial.y * seedStroke.profile.y * 0.5) * 2.0;
    float wetness = saturate(brushMaterial.w / 1.6);
    float load = clamp(brushMaterial.z / 1.05, 0.0, 2.7);
    float splay = saturate(brushDynamics.x / 2.0);
    float seedRadius = max(brushMaterial.x * seedStroke.profile.x, 0.0001);
    float seedNormalRadius = max(seedRadius * seedStroke.profile.z, 0.0001);
    float seedSplit = saturate((0.28 - LaneHash(chainStart, tuft, 53u)) * 3.0) * saturate((edge - 0.20) * 1.5) * seedStroke.dynamics.w;
    float initialCohesion = LaneCohesion(edge, seedSplit, wetness, seedPressure);
    float2 tip = StrokePoint(seedStroke, 0.0);
    float poseBias = seedStroke.pose.x * 0.18 + seedStroke.pose.y * 0.08;
    float offset = (restOffset + poseBias * (1.0 - edge * 0.35)) * seedNormalRadius * (0.42 + splay * 0.38) + (laneHash - 0.5) * seedNormalRadius * 0.05;
    float stateLoad = load * (0.86 + initialCohesion * 0.48) * (1.0 - edge * 0.08) * laneLoad * (1.0 - seedSplit * 0.18);
    float stateWet = wetness * (0.74 + initialCohesion * 0.24 - seedSplit * 0.08);
    float previousPatchNormalRadius = 0.0;
    float previousPatchTangentRadius = 0.0;

    [loop]
    for (uint replayStrokeIndex = chainStart; replayStrokeIndex < strokeIndex; replayStrokeIndex += 1u)
    {
        SimulateBokushoTuftSegmentToPage(BokushoBrushStrokes[replayStrokeIndex], replayStrokeIndex, chainStart, tuft, sampleCount, restOffset, edge, offset, tip, stateLoad, stateWet, previousPatchNormalRadius, previousPatchTangentRadius, false, true);
    }

    SimulateBokushoTuftSegmentToPage(stroke, strokeIndex, chainStart, tuft, sampleCount, restOffset, edge, offset, tip, stateLoad, stateWet, previousPatchNormalRadius, previousPatchTangentRadius, true, strokeIndex == chainStart);
}
