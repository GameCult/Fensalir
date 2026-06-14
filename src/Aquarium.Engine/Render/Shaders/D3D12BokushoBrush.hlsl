#include "CultMath/CultMath.hlsl"

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

float StrokeTaper(float t)
{
    float entry = smoothstep(0.0, 0.10, t);
    float exit = 1.0 - smoothstep(0.86, 1.0, t);
    return 0.04 + entry * exit * 0.96;
}

float StrokeTaper(BokushoBrushStroke stroke, float t)
{
    float strokeT = saturate(lerp(stroke.p0.z, max(stroke.p0.z, stroke.p0.w), t));
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float entry = smoothstep(0.0, entryTaper, strokeT);
    float exit = 1.0 - smoothstep(1.0 - exitTaper, 1.0, strokeT);
    float contact = pow(entry * exit, 1.12);
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
        float laneCore = smoothstep(0.0, 0.78, 1.0 - edge);
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
        float continuity = 1.0 - smoothstep(0.18 + dryMemory * 0.28, 0.96, fiberNoise) * dryMemory * (0.38 + edge * 0.22);
        float contactTransfer = contact * stateLoad * (0.16 + stateWet * 0.92) * (0.30 + drag * 0.64 + localPressure * 0.22 + sweptPatch * 0.18) * (0.68 + laneCore * 0.50 - separation * 0.14) * continuity;
        float airborneRelease = (1.0 - contact) * stateLoad * stateWet * saturate(velocity * 0.010 - adhesion * 0.16) * (0.20 + separation * 0.42 + edge * 0.18);
        float consumedPigment = contactTransfer + airborneRelease;
        stateLoad = max(0.0, stateLoad - consumedPigment * segmentSpan * (0.010 + localPressure * 0.006));
        stateWet = max(0.0, stateWet - consumedPigment * segmentSpan * (0.006 + dryMemory * 0.003));

        if (writeOutput)
        {
            uint index = ((outputStrokeIndex * tuftCount) + tuft) * sampleCount + sample;
            float pigment = (contactTransfer * (0.95 + localPressure * 0.34 + sweptPatch * 0.24) + airborneRelease * (1.6 + velocity * 0.002)) * stroke.dynamics.z;
            float localPigment = saturate(pigment);
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
    float load = clamp(brushMaterial.z / 1.25, 0.0, 1.8);
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
