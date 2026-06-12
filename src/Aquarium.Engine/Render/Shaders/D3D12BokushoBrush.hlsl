#include "CultMath/CultMath.hlsl"

cbuffer BokushoBrushConstants : register(b4)
{
    float4 brushShape;     // sampleCount, tuftCount, physicsHz, timeSeconds
    float4 brushMaterial;  // radius, pressure, inkLoad, wetness
    float4 brushDynamics;  // splay, bend, friction, reserved
    float4 strokeP0;
    float4 strokeP1;
    float4 strokeP2;
    float4 strokeP3;
};

RWStructuredBuffer<float> BokushoTraceField : register(u20);
RWStructuredBuffer<float> BokushoCanvasField : register(u21);

float2 StrokePoint(float t)
{
    return cultmath_catmullrom(strokeP0.xy, strokeP1.xy, strokeP2.xy, strokeP3.xy, t);
}

float2 StrokeTangent(float t)
{
    float dt = 1.0 / max(brushShape.x - 1.0, 1.0);
    float2 before = StrokePoint(saturate(t - dt));
    float2 after = StrokePoint(saturate(t + dt));
    return cultmath_normalize(after - before);
}

[numthreads(128, 1, 1)]
void D3D12BokushoBrushCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint tuft = dispatchThreadId.x;
    uint sampleCount = max((uint)round(brushShape.x), 2u);
    uint tuftCount = max((uint)round(brushShape.y), 1u);
    if (tuft >= tuftCount)
    {
        return;
    }

    float laneT = tuftCount <= 1u ? 0.5 : (float)tuft / (float)(tuftCount - 1u);
    float restOffset = laneT * 2.0 - 1.0;
    float edge = abs(restOffset);
    float pressure = saturate(brushMaterial.y * 0.5) * 2.0;
    float wetness = saturate(brushMaterial.w / 1.6);
    float load = saturate(brushMaterial.z / 2.0);
    float splay = saturate(brushDynamics.x / 2.0);
    float bend = saturate(brushDynamics.y / 2.4);
    float friction = saturate(brushDynamics.z);
    float radius = max(brushMaterial.x, 0.0001);
    float2 tip = StrokePoint(0.0);
    float offset = restOffset * radius * (0.42 + splay * 0.38);
    float stateLoad = load * (1.0 - edge * 0.36);
    float stateWet = wetness * (0.86 + (1.0 - edge) * 0.14);
    float shed = 0.0;

    [loop]
    for (uint sample = 0u; sample < sampleCount; sample += 1u)
    {
        float t = sampleCount <= 1u ? 0.0 : (float)sample / (float)(sampleCount - 1u);
        float2 center = StrokePoint(t);
        float2 tangent = StrokeTangent(t);
        float2 normal = float2(-tangent.y, tangent.x);
        float turn = sin(t * 6.28318530718);
        float targetOffset = restOffset * radius * (0.38 + splay * 0.52 + pressure * 0.08 - wetness * 0.10) + turn * radius * 0.14 * (1.0 - edge);
        float recovery = saturate(0.08 + stateWet * 0.12 + pressure * 0.08 + (1.0 - edge) * 0.07);
        offset = cultmath_lerp(offset, targetOffset, recovery);

        float lag = radius * (0.12 + bend * 0.72 + friction * pressure * 0.34 + edge * 0.18);
        float2 desiredTip = center + normal * offset - tangent * lag;
        float2 slip = desiredTip - tip;
        float contact = saturate(pressure * stateWet * stateLoad * (0.70 + (1.0 - edge) * 0.26));
        float drag = contact * friction * (0.38 + stateWet * 0.22 + edge * 0.18);
        tip = tip + slip * (1.0 - drag);

        float velocity = length(slip) * brushShape.z / max(radius, 0.001);
        float tension = saturate(abs(targetOffset - offset) / max(radius, 0.001) * 0.38 + velocity * 0.018 + drag * 0.46);
        float separation = saturate(edge * 0.22 + tension * 0.34 + velocity * 0.010 - stateWet * 0.16);
        float adhesion = saturate(stateWet * (0.52 + pressure * 0.20) - separation * 0.18 - tension * 0.08);
        float deposition = contact * stateLoad * stateWet * saturate(0.10 + drag * 0.72 + velocity * 0.012) * (0.82 + separation * 0.22);
        stateLoad = max(0.0, stateLoad - deposition * (0.040 + pressure * 0.025));
        stateWet = max(0.0, stateWet - deposition * 0.010);
        shed += deposition;

        uint index = tuft * sampleCount + sample;
        float trace = saturate(contact * (0.30 + stateLoad * 0.42 + adhesion * 0.20) + deposition * 1.8);
        BokushoTraceField[index] = trace;
        BokushoCanvasField[index] = saturate(shed);
    }
}
