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
    float4 p0;
    float4 p1;
    float4 p2;
    float4 p3;
};

RWStructuredBuffer<float> BokushoTraceField : register(u20);
RWStructuredBuffer<float> BokushoCanvasField : register(u21);
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
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float entry = smoothstep(0.0, entryTaper, t);
    float exit = 1.0 - smoothstep(1.0 - exitTaper, 1.0, t);
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
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float pressIn = smoothstep(0.0, min(entryTaper * 1.6, 0.62), t);
    float liftOut = 1.0 - smoothstep(max(1.0 - exitTaper * 1.35, 0.20), 1.0, t);
    float belly = smoothstep(0.12, 0.42, t) * (1.0 - smoothstep(0.68, 0.96, t));
    return saturate(0.54 + pressIn * liftOut * 0.28 + belly * 0.30);
}

float LaneCohesion(float edge, float split, float wetness, float pressure)
{
    return saturate(0.50 + (1.0 - edge) * 0.38 + wetness * 0.22 + pressure * 0.12 - split * 0.18);
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
    float laneT = tuftCount <= 1u ? 0.5 : (float)tuft / (float)(tuftCount - 1u);
    float restOffset = laneT * 2.0 - 1.0;
    float edge = abs(restOffset);
    float laneHash = LaneHash(strokeIndex, tuft, 17u);
    float laneLoad = 0.76 + laneHash * 0.34;
    float split = saturate((0.20 - LaneHash(strokeIndex, tuft, 53u)) * 4.0) * saturate((edge - 0.18) * 1.7) * stroke.dynamics.w;
    float pressure = saturate(brushMaterial.y * stroke.profile.y * 0.5) * 2.0;
    float wetness = saturate(brushMaterial.w / 1.6);
    float initialCohesion = LaneCohesion(edge, split, wetness, pressure);
    float load = saturate(brushMaterial.z / 2.0);
    float splay = saturate(brushDynamics.x / 2.0);
    float bend = saturate(brushDynamics.y / 2.4);
    float friction = saturate(brushDynamics.z);
    float radius = max(brushMaterial.x * stroke.profile.x, 0.0001);
    float normalRadius = max(radius * stroke.profile.z, 0.0001);
    float tangentRadius = max(radius * stroke.profile.w, 0.0001);
    float2 tip = StrokePoint(stroke, 0.0);
    float offset = restOffset * normalRadius * (0.42 + splay * 0.38) + (laneHash - 0.5) * normalRadius * 0.05;
    float stateLoad = load * (0.62 + initialCohesion * 0.46) * (1.0 - edge * 0.18) * laneLoad * (1.0 - split * 0.36);
    float stateWet = wetness * (0.74 + initialCohesion * 0.24 - split * 0.08);

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
        float localPressure = pressure * taper * pressureShape;
        float cohesion = LaneCohesion(edge, split, stateWet, localPressure);
        float localNormalRadius = max(normalRadius * normalShape, 0.0001);
        float localTangentRadius = max(tangentRadius * tangentShape, 0.0001);
        float turn = sin(t * 6.28318530718 + (float)strokeIndex * 0.37);
        float targetOffset = restOffset * localNormalRadius * (0.38 + splay * 0.52 + localPressure * 0.08 - wetness * 0.10) + turn * localNormalRadius * 0.14 * (1.0 - edge);
        float recovery = saturate(0.08 + stateWet * 0.12 + localPressure * 0.08 + (1.0 - edge) * 0.07);
        offset = cultmath_lerp(offset, targetOffset, recovery);

        float lag = localTangentRadius * (0.12 + bend * 0.72 + friction * localPressure * 0.34 + edge * 0.18);
        float2 desiredTip = center + normal * offset - tangent * lag;
        float2 slip = desiredTip - tip;
        float contact = saturate(localPressure * stateWet * stateLoad * (0.58 + cohesion * 0.40 + (1.0 - edge) * 0.18));
        float drag = contact * friction * (0.38 + stateWet * 0.22 + edge * 0.18);
        tip = tip + slip * (1.0 - drag);

        float velocity = length(slip) * brushShape.z / max(radius, 0.001);
        float tension = saturate(abs(targetOffset - offset) / max(localNormalRadius, 0.001) * 0.38 + velocity * 0.018 + drag * 0.46);
        float separation = saturate(edge * 0.18 + tension * (0.24 + split * 0.18) + velocity * 0.008 - stateWet * (0.12 + cohesion * 0.10));
        float adhesion = saturate(stateWet * (0.44 + cohesion * 0.28 + localPressure * 0.20) - separation * 0.16 - tension * 0.07);
        float deposition = contact * stateLoad * stateWet * saturate(0.10 + drag * 0.72 + velocity * 0.010) * (0.74 + separation * 0.18 + cohesion * 0.26);
        stateLoad = max(0.0, stateLoad - deposition * (0.032 + localPressure * 0.020 - cohesion * 0.008));
        stateWet = max(0.0, stateWet - deposition * 0.010);

        uint index = ((strokeIndex * tuftCount) + tuft) * sampleCount + sample;
        float pigmentSurvival = 0.74 + cohesion * 0.36 - split * 0.08;
        float localPigment = saturate((deposition * (7.5 + contact * 2.5) + contact * stateLoad * stateWet * 0.24 + adhesion * contact * 0.10) * pigmentSurvival * stroke.dynamics.z);
        float trace = saturate(contact * (0.30 + stateLoad * 0.42 + adhesion * 0.20) + deposition * 1.8);
        BokushoTraceField[index] = trace;
        BokushoCanvasField[index] = localPigment;
    }
}
