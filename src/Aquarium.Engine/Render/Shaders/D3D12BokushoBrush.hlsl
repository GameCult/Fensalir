#include "CultMath/CultMath.hlsl"

static const float BOKUSHO_PAGE_DENSITY_SCALE = 65535.0;
static const float BOKUSHO_PAGE_PATCH_TRANSFER_GAIN = 0.0032;
static const float BOKUSHO_PAGE_PATCH_TANGENT_RADIUS_SCALE = 0.26;
static const float BOKUSHO_PAGE_PATCH_SWEEP_RADIUS_SCALE = 0.5;
static const float BOKUSHO_PAGE_PATCH_NORMAL_RADIUS_SCALE = 0.20;
static const float BOKUSHO_PAGE_PATCH_MINIMUM_RADIUS = 0.001;
static const float BOKUSHO_PAGE_PATCH_WORLD_QUANTUM = 1.0 / 1024.0;
static const float BOKUSHO_PAGE_PATCH_INK_QUANTUM = 1.0 / 2048.0;
static const float BOKUSHO_PAGE_PATCH_COVERAGE_QUANTUM = 1.0 / 1024.0;
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

float2 StrokePoint(BokushoBrushStroke stroke, float t)
{
    return cultmath_catmullrom(stroke.p0.xy, stroke.p1.xy, stroke.p2.xy, stroke.p3.xy, t);
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

float StrokeTaper(float t)
{
    float entry = cultmath_smoothstep(0.0, 0.10, t);
    float exit = 1.0 - cultmath_smoothstep(0.86, 1.0, t);
    return 0.04 + entry * exit * 0.96;
}

float StrokeTaper(BokushoBrushStroke stroke, float t)
{
    float strokeT = saturate(lerp(stroke.p0.z, max(stroke.p0.z, stroke.p0.w), t));
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float entry = cultmath_smoothstep(0.0, entryTaper, strokeT);
    float exit = 1.0 - cultmath_smoothstep(1.0 - exitTaper, 1.0, strokeT);
    float x = saturate(entry * exit);
    float contact = x * (0.76 + x * 0.24);
    return 0.018 + contact * 0.982;
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

void DepositSweptPatch(float2 previousTip, float2 tip, float normalRadius, float tangentRadius, float pigment)
{
    float ink = QuantizePositive(saturate(pigment), BOKUSHO_PAGE_PATCH_INK_QUANTUM);
    if (ink <= 0.000001)
    {
        return;
    }

    previousTip = QuantizeFloat2(previousTip, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    tip = QuantizeFloat2(tip, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    normalRadius = QuantizePositive(normalRadius, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    tangentRadius = QuantizePositive(tangentRadius, BOKUSHO_PAGE_PATCH_WORLD_QUANTUM);
    float2 sweep = tip - previousTip;
    float sweepLength = length(sweep);
    float2 tangent = sweepLength > 0.000001 ? sweep / sweepLength : float2(1.0, 0.0);
    float2 normal = float2(-tangent.y, tangent.x);
    float patchTangentRadius = max(tangentRadius * BOKUSHO_PAGE_PATCH_TANGENT_RADIUS_SCALE + sweepLength * BOKUSHO_PAGE_PATCH_SWEEP_RADIUS_SCALE, BOKUSHO_PAGE_PATCH_MINIMUM_RADIUS);
    float patchNormalRadius = max(normalRadius * BOKUSHO_PAGE_PATCH_NORMAL_RADIUS_SCALE, BOKUSHO_PAGE_PATCH_MINIMUM_RADIUS);
    float2 center = (previousTip + tip) * 0.5;
    float2 extents = float2(
        abs(tangent.x) * patchTangentRadius + abs(normal.x) * patchNormalRadius,
        abs(tangent.y) * patchTangentRadius + abs(normal.y) * patchNormalRadius);
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
            float tangentDistance = dot(local, tangent) / patchTangentRadius;
            float normalDistance = dot(local, normal) / patchNormalRadius;
            float ellipse = sqrt(tangentDistance * tangentDistance + normalDistance * normalDistance);
            float coverage = QuantizePositive(cultmath_smoothstep(1.0, 0.0, ellipse), BOKUSHO_PAGE_PATCH_COVERAGE_QUANTUM);
            if (coverage <= 0.0)
            {
                continue;
            }

            float contribution = ink * coverage * BOKUSHO_PAGE_PATCH_TRANSFER_GAIN;
            InterlockedAdd(BokushoPageDensityField[(uint)(y * (int)pageSize + x)], EncodePageDensityContribution(contribution));
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
    float segmentVelocityScale = 1.0 / segmentSpan;
    float split = saturate((0.28 - LaneHash(laneKey, tuft, 53u)) * 3.0) * saturate((edge - 0.20) * 1.5) * stroke.dynamics.w;

    [loop]
    for (uint sample = 0u; sample < sampleCount; sample += 1u)
    {
        float t = sampleCount <= 1u ? 0.0 : (float)sample / (float)(sampleCount - 1u);
        float2 center = StrokePoint(stroke, t);
        float2 tangent = StrokeTangent(stroke, t);
        float2 normal = float2(-tangent.y, tangent.x);
        float taper = StrokeTaper(stroke, t);
        float localPressure = pressure * taper;
        float laneCore = cultmath_smoothstep(0.0, 0.78, 1.0 - edge);
        float cohesion = saturate(0.28 + stateWet * 0.44 + laneCore * 0.24 + localPressure * 0.10 - split * 0.18);
        float localNormalRadius = max(normalRadius * (0.18 + taper * 0.82) * (0.72 + splay * 0.34 + localPressure * 0.16 - cohesion * 0.08), 0.0001);
        float localTangentRadius = max(tangentRadius * (0.24 + taper * 0.76) * (0.86 + bend * 0.18), 0.0001);
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
        float fiberNoise = LaneHash(laneKey + sample * 13u, tuft, 101u);
        float continuity = 1.0 - cultmath_smoothstep(0.18 + dryMemory * 0.28, 0.96, fiberNoise) * dryMemory * (0.38 + edge * 0.22);
        float contactTransfer = contact * stateLoad * (0.16 + stateWet * 0.92) * (0.30 + drag * 0.64 + localPressure * 0.22 + sweptPatch * 0.18) * (0.68 + laneCore * 0.50 - separation * 0.14) * continuity;
        float airborneRelease = (1.0 - contact) * stateLoad * stateWet * saturate(velocity * 0.010 - adhesion * 0.16) * (0.20 + separation * 0.42 + edge * 0.18);
        float consumedPigment = contactTransfer + airborneRelease;
        stateLoad = max(0.0, stateLoad - consumedPigment * segmentSpan * (0.010 + localPressure * 0.006));
        stateWet = max(0.0, stateWet - consumedPigment * segmentSpan * (0.006 + dryMemory * 0.003));

        if (writeOutput)
        {
            uint index = ((outputStrokeIndex * tuftCount) + tuft) * sampleCount + sample;
            float pigment = (contactTransfer * (0.95 + localPressure * 0.34 + sweptPatch * 0.24) + airborneRelease * (1.6 + velocity * 0.002)) * stroke.dynamics.z;
            float localPigment = CanvasPigment(pigment);
            float trace = saturate(contact * 0.70 + airborneRelease * 2.0 + stateLoad * 0.18);
            BokushoTraceField[index] = trace;
            BokushoCanvasField[index] = localPigment;
            BokushoTipField[index] = float4(
                tip,
                localNormalRadius * (0.84 + laneCore * 0.20 + localPressure * 0.14 + sweptPatch * 0.08 - separation * 0.10),
                localTangentRadius * (0.92 + bend * 0.22 + drag * 0.16) + sweptDistance * 0.36);
        }
    }
}

void SimulateBokushoTuftSegmentToPage(
    BokushoBrushStroke stroke,
    uint laneKey,
    uint tuft,
    uint sampleCount,
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
    uint stepCount = min(max((uint)ceil(segmentSpan * brushShape.z), 2u), 4096u);
    float segmentVelocityScale = 1.0 / segmentSpan;
    float split = saturate((0.28 - LaneHash(laneKey, tuft, 53u)) * 3.0) * saturate((edge - 0.20) * 1.5) * stroke.dynamics.w;

    [loop]
    for (uint step = 0u; step < stepCount; step += 1u)
    {
        float t = stepCount <= 1u ? 0.0 : (float)step / (float)(stepCount - 1u);
        float2 center = StrokePoint(stroke, t);
        float2 tangent = StrokeTangent(stroke, t, stepCount);
        float2 normal = float2(-tangent.y, tangent.x);
        float taper = StrokeTaper(stroke, t);
        float localPressure = pressure * taper;
        float laneCore = cultmath_smoothstep(0.0, 0.78, 1.0 - edge);
        float cohesion = saturate(0.28 + stateWet * 0.44 + laneCore * 0.24 + localPressure * 0.10 - split * 0.18);
        float localNormalRadius = max(normalRadius * (0.18 + taper * 0.82) * (0.72 + splay * 0.34 + localPressure * 0.16 - cohesion * 0.08), 0.0001);
        float localTangentRadius = max(tangentRadius * (0.24 + taper * 0.76) * (0.86 + bend * 0.18), 0.0001);
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
        float fiberNoise = LaneHash(laneKey + step * 13u, tuft, 101u);
        float continuity = 1.0 - cultmath_smoothstep(0.18 + dryMemory * 0.28, 0.96, fiberNoise) * dryMemory * (0.38 + edge * 0.22);
        float contactTransfer = contact * stateLoad * (0.16 + stateWet * 0.92) * (0.30 + drag * 0.64 + localPressure * 0.22 + sweptPatch * 0.18) * (0.68 + laneCore * 0.50 - separation * 0.14) * continuity;
        float airborneRelease = (1.0 - contact) * stateLoad * stateWet * saturate(velocity * 0.010 - adhesion * 0.16) * (0.20 + separation * 0.42 + edge * 0.18);
        float consumedPigment = contactTransfer + airborneRelease;
        stateLoad = max(0.0, stateLoad - consumedPigment * segmentSpan * (0.010 + localPressure * 0.006));
        stateWet = max(0.0, stateWet - consumedPigment * segmentSpan * (0.006 + dryMemory * 0.003));

        if (writeOutput)
        {
            float pigment = (contactTransfer * (0.95 + localPressure * 0.34 + sweptPatch * 0.24) + airborneRelease * (1.6 + velocity * 0.002)) * stroke.dynamics.z;
            float patchNormalRadius = localNormalRadius * (0.84 + laneCore * 0.20 + localPressure * 0.14 + sweptPatch * 0.08 - separation * 0.10);
            float patchTangentRadius = localTangentRadius * (0.92 + bend * 0.22 + drag * 0.16) + sweptDistance * 0.36;
            DepositSweptPatch(previousTip, tip, patchNormalRadius, patchTangentRadius, pigment);
        }
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

    [loop]
    for (uint replayStrokeIndex = chainStart; replayStrokeIndex < strokeIndex; replayStrokeIndex += 1u)
    {
        SimulateBokushoTuftSegmentToPage(BokushoBrushStrokes[replayStrokeIndex], chainStart, tuft, sampleCount, restOffset, edge, offset, tip, stateLoad, stateWet, false);
    }

    SimulateBokushoTuftSegmentToPage(stroke, chainStart, tuft, sampleCount, restOffset, edge, offset, tip, stateLoad, stateWet, true);
}
