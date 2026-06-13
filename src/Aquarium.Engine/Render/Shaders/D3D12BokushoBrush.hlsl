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
    return 0.04 + entry * exit * 0.96;
}

float StrokeNormalRadiusShape(float taper)
{
    return 0.08 + taper * 0.92;
}

float StrokeTangentRadiusShape(float taper)
{
    return 0.16 + taper * 0.84;
}

float StrokePressureShape(BokushoBrushStroke stroke, float t)
{
    float strokeT = saturate(lerp(stroke.p0.z, max(stroke.p0.z, stroke.p0.w), t));
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float pressIn = smoothstep(0.0, min(entryTaper * 1.6, 0.62), strokeT);
    float liftOut = 1.0 - smoothstep(max(1.0 - exitTaper * 1.35, 0.20), 1.0, strokeT);
    float belly = smoothstep(0.12, 0.42, strokeT) * (1.0 - smoothstep(0.68, 0.96, strokeT));
    return saturate(0.54 + pressIn * liftOut * 0.28 + belly * 0.30);
}

float LaneCohesion(float edge, float split, float wetness, float pressure)
{
    return saturate(0.50 + (1.0 - edge) * 0.38 + wetness * 0.22 + pressure * 0.12 - split * 0.18);
}

float StrokeSpeed(BokushoBrushStroke stroke, float t, float dt)
{
    float2 before = StrokePoint(stroke, saturate(t - dt));
    float2 after = StrokePoint(stroke, saturate(t + dt));
    return length(after - before) / max(dt * 2.0, 0.001);
}

float2 StrokeLocalTangent(BokushoBrushStroke stroke, float t, float dt)
{
    float2 before = StrokePoint(stroke, saturate(t - dt));
    float2 after = StrokePoint(stroke, saturate(t + dt));
    return cultmath_normalize(after - before);
}

float StrokeTurn(BokushoBrushStroke stroke, float t, float dt)
{
    float2 before = StrokeLocalTangent(stroke, saturate(t - dt), dt);
    float2 after = StrokeLocalTangent(stroke, saturate(t + dt), dt);
    return saturate(abs(before.x * after.y - before.y * after.x) * 1.8);
}

float StrokeGesturePressureShape(BokushoBrushStroke stroke, float t)
{
    float dt = 1.0 / max(brushShape.x - 1.0, 1.0);
    float localSpeed = StrokeSpeed(stroke, t, dt);
    float expectedSpeed = max(length(stroke.p3.xy - stroke.p0.xy), 0.001);
    float slowPress = saturate((expectedSpeed * 1.18 - localSpeed) / max(expectedSpeed * 0.80, 0.001));
    float turnPress = StrokeTurn(stroke, t, dt);
    return saturate(0.88 + slowPress * 0.30 + turnPress * 0.20);
}

float StrokeGestureWidthShape(BokushoBrushStroke stroke, float t)
{
    float dt = 1.0 / max(brushShape.x - 1.0, 1.0);
    float localSpeed = StrokeSpeed(stroke, t, dt);
    float expectedSpeed = max(length(stroke.p3.xy - stroke.p0.xy), 0.001);
    float slowSpread = saturate((expectedSpeed * 1.08 - localSpeed) / max(expectedSpeed * 0.85, 0.001));
    float turnSpread = StrokeTurn(stroke, t, dt);
    return saturate(0.84 + slowSpread * 0.36 + turnSpread * 0.20);
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

float SegmentJoinBlend(BokushoBrushStroke stroke, float t)
{
    float blend = 1.0;
    if (stroke.p0.z > 0.0001)
    {
        blend *= 0.58 + smoothstep(0.0, 0.08, t) * 0.42;
    }

    if (stroke.p0.w < 0.9999)
    {
        blend *= 0.58 + (1.0 - smoothstep(0.92, 1.0, t)) * 0.42;
    }

    return saturate(blend);
}

uint StrokeChainStart(uint strokeIndex)
{
    uint chainStart = strokeIndex;
    float expectedStart = saturate(BokushoBrushStrokes[strokeIndex].p0.z);
    [loop]
    while (chainStart > 0u && expectedStart > 0.0001)
    {
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
    float wetness = saturate(brushMaterial.w / 1.6);
    float splay = saturate(brushDynamics.x / 2.0);
    float bend = saturate(brushDynamics.y / 2.4);
    float friction = saturate(brushDynamics.z);
    float radius = max(brushMaterial.x * stroke.profile.x, 0.0001);
    float normalRadius = max(radius * stroke.profile.z, 0.0001);
    float tangentRadius = max(radius * stroke.profile.w, 0.0001);
    float split = saturate((0.20 - LaneHash(laneKey, tuft, 53u)) * 4.0) * saturate((edge - 0.18) * 1.7) * stroke.dynamics.w;

    [loop]
    for (uint sample = 0u; sample < sampleCount; sample += 1u)
    {
        float t = sampleCount <= 1u ? 0.0 : (float)sample / (float)(sampleCount - 1u);
        float2 center = StrokePoint(stroke, t);
        float2 tangent = StrokeTangent(stroke, t);
        float2 normal = float2(-tangent.y, tangent.x);
        float taper = StrokeTaper(stroke, t);
        float normalShape = StrokeNormalRadiusShape(taper);
        float tangentShape = StrokeTangentRadiusShape(taper);
        float pressureShape = StrokePressureShape(stroke, t);
        float gesturePressure = StrokeGesturePressureShape(stroke, t);
        float gestureWidth = StrokeGestureWidthShape(stroke, t);
        float localPressure = pressure * taper * pressureShape * gesturePressure;
        float cohesion = LaneCohesion(edge, split, stateWet, localPressure);
        float poseSpread = saturate(0.92 + abs(stroke.pose.x) * 0.22 + stroke.pose.w * 0.10 - stroke.pose.z * 0.04);
        float localNormalRadius = max(normalRadius * normalShape * gestureWidth * poseSpread, 0.0001);
        float localTangentRadius = max(tangentRadius * tangentShape * (0.86 + gestureWidth * 0.08 + stroke.pose.z * 0.10), 0.0001);
        float turn = sin(t * 6.28318530718 + (float)outputStrokeIndex * 0.37);
        float rotatedRest = restOffset + stroke.pose.y * 0.10 * (1.0 - edge);
        float targetOffset = rotatedRest * localNormalRadius * (0.38 + splay * 0.52 + localPressure * 0.08 - wetness * 0.10) + (turn + stroke.pose.y * 0.22) * localNormalRadius * 0.14 * (1.0 - edge);
        float recovery = saturate(0.06 + stateWet * 0.10 + localPressure * 0.07 + (1.0 - edge) * 0.06 + stroke.pose.w * 0.07);
        offset = cultmath_lerp(offset, targetOffset, recovery);

        float lag = localTangentRadius * (0.04 + bend * 0.32 + stroke.pose.z * 0.10 + friction * localPressure * 0.16 + edge * 0.08);
        float2 desiredTip = center + normal * offset - tangent * lag;
        float2 slip = desiredTip - tip;
        float poseContact = 0.90 + stroke.pose.w * 0.10 + (1.0 - saturate(stroke.pose.z / 2.4)) * 0.10;
        float contact = saturate(localPressure * poseContact * stateWet * stateLoad * (0.58 + cohesion * 0.40 + (1.0 - edge) * 0.18));
        float drag = contact * friction * (0.38 + stateWet * 0.22 + edge * 0.18);
        tip = tip + slip * (1.0 - drag);

        float velocity = length(slip) * brushShape.z / max(radius, 0.001);
        float tension = saturate(abs(targetOffset - offset) / max(localNormalRadius, 0.001) * 0.38 + velocity * 0.018 + drag * 0.46);
        float separation = saturate(edge * 0.18 + tension * (0.24 + split * 0.18) + velocity * 0.008 - stateWet * (0.12 + cohesion * 0.10));
        float adhesion = saturate(stateWet * (0.44 + cohesion * 0.28 + localPressure * 0.20) - separation * 0.16 - tension * 0.07);
        float laneCore = smoothstep(0.0, 0.74, 1.0 - edge);
        float edgeComb = saturate(edge * 1.08 + split * 0.26 + separation * 0.34 - cohesion * 0.12);
        float dryMemory = saturate((1.0 - stateWet) * 0.68 + edgeComb * 0.42 + velocity * 0.004 - localPressure * 0.10);
        float fiberNoise = LaneHash(laneKey + sample * 13u, tuft, 101u);
        float fiberGate = smoothstep(0.20 + dryMemory * 0.24, 0.96, fiberNoise);
        float depositBody = 0.70 + laneCore * 0.58 - edgeComb * 0.14;
        float depositIntermittency = 1.0 - fiberGate * dryMemory * (0.48 + edge * 0.24);
        float deposition = contact * stateLoad * stateWet * saturate(0.10 + drag * 0.72 + velocity * 0.010) * (0.74 + separation * 0.18 + cohesion * 0.26) * depositBody * depositIntermittency;
        stateLoad = max(0.0, stateLoad - deposition * (0.032 + localPressure * 0.020 - cohesion * 0.008));
        stateWet = max(0.0, stateWet - deposition * (0.010 + dryMemory * 0.004 + edgeComb * 0.003));

        if (writeOutput)
        {
            uint index = ((outputStrokeIndex * tuftCount) + tuft) * sampleCount + sample;
            float pigmentSurvival = 0.62 + cohesion * 0.30 + laneCore * 0.36 - split * 0.08 - dryMemory * 0.12;
            float joinBlend = SegmentJoinBlend(stroke, t);
            float pigment = deposition * (8.4 + contact * 2.8) + contact * stateLoad * stateWet * (0.14 + laneCore * 0.18) + adhesion * contact * 0.08;
            float localPigment = saturate(pigment * pigmentSurvival * stroke.dynamics.z * joinBlend);
            float trace = saturate((contact * (0.24 + stateLoad * 0.36 + adhesion * 0.18 + laneCore * 0.12) + deposition * 1.9) * joinBlend);
            BokushoTraceField[index] = trace;
            BokushoCanvasField[index] = localPigment;
            BokushoTipField[index] = float4(tip, localNormalRadius * (0.54 + laneCore * 0.24 + localPressure * 0.22 + splay * 0.20 - split * 0.04), localTangentRadius * (0.84 + drag * 0.34 + bend * 0.18));
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
    float load = saturate(brushMaterial.z / 2.0);
    float splay = saturate(brushDynamics.x / 2.0);
    float seedRadius = max(brushMaterial.x * seedStroke.profile.x, 0.0001);
    float seedNormalRadius = max(seedRadius * seedStroke.profile.z, 0.0001);
    float seedSplit = saturate((0.20 - LaneHash(chainStart, tuft, 53u)) * 4.0) * saturate((edge - 0.18) * 1.7) * seedStroke.dynamics.w;
    float initialCohesion = LaneCohesion(edge, seedSplit, wetness, seedPressure);
    float2 tip = StrokePoint(seedStroke, 0.0);
    float poseBias = seedStroke.pose.x * 0.18 + seedStroke.pose.y * 0.08;
    float offset = (restOffset + poseBias * (1.0 - edge * 0.35)) * seedNormalRadius * (0.42 + splay * 0.38) + (laneHash - 0.5) * seedNormalRadius * 0.05;
    float stateLoad = load * (0.62 + initialCohesion * 0.46) * (1.0 - edge * 0.18) * laneLoad * (1.0 - seedSplit * 0.36);
    float stateWet = wetness * (0.74 + initialCohesion * 0.24 - seedSplit * 0.08);

    [loop]
    for (uint replayStrokeIndex = chainStart; replayStrokeIndex < strokeIndex; replayStrokeIndex += 1u)
    {
        SimulateBokushoTuftSegment(BokushoBrushStrokes[replayStrokeIndex], replayStrokeIndex, chainStart, tuft, sampleCount, tuftCount, restOffset, edge, offset, tip, stateLoad, stateWet, false);
    }

    SimulateBokushoTuftSegment(stroke, strokeIndex, chainStart, tuft, sampleCount, tuftCount, restOffset, edge, offset, tip, stateLoad, stateWet, true);
}
