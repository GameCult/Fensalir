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
};

cbuffer HeightFieldBrushes : register(b1)
{
    float4 brushCenterRadius[64];
    float4 brushShape[64];
    float4 brushWave[64];
    float4 brushDomain[64];
};

cbuffer BokushoPageConstants : register(b5)
{
    float4 bokushoShape;     // sampleCount, tuftCount, physicsHz, strokeCount
    float4 bokushoMaterial;  // radius, pressure, inkLoad, wetness
    float4 bokushoDynamics;  // splay, bend, friction, reserved
    float4 bokushoProfile;   // radiusScale, pressureScale, normalScale, tangentScale
    float4 bokushoStrokeP0;
    float4 bokushoStrokeP1;
    float4 bokushoStrokeP2;
    float4 bokushoStrokeP3;
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

StructuredBuffer<float> BokushoCanvasField : register(t77);
StructuredBuffer<BokushoBrushStroke> BokushoBrushStrokes : register(t78);
StructuredBuffer<float4> BokushoTipField : register(t79);

#include "CultMath/CultMath.hlsl"

struct VertexOut
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

struct BrushVertexOut
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
    nointerpolation uint brushIndex : TEXCOORD1;
    nointerpolation float4 centerRadius : TEXCOORD2;
    nointerpolation float4 shape : TEXCOORD3;
    nointerpolation float4 wave : TEXCOORD4;
};

float2 viewLocal(float2 p)
{
    return (p - viewCenter) / max(viewRadius, 0.001);
}

float2 viewUv(float2 p)
{
    return viewLocal(p) * 0.5 + 0.5;
}

float2 viewWorld(float2 uv)
{
    return viewCenter + (uv * 2.0 - 1.0) * viewRadius;
}

float2 bokushoStrokePoint(BokushoBrushStroke stroke, float t)
{
    return cultmath_catmullrom(stroke.p0.xy, stroke.p1.xy, stroke.p2.xy, stroke.p3.xy, t);
}

float2 bokushoStrokeTangent(BokushoBrushStroke stroke, float t)
{
    float dt = 1.0 / max(bokushoShape.x - 1.0, 1.0);
    float2 before = bokushoStrokePoint(stroke, saturate(t - dt));
    float2 after = bokushoStrokePoint(stroke, saturate(t + dt));
    return cultmath_normalize(after - before);
}

float bokushoStrokeTaper(BokushoBrushStroke stroke, float t)
{
    float strokeT = saturate(cultmath_lerp(stroke.p0.z, max(stroke.p0.z, stroke.p0.w), t));
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float entry = smoothstep(0.0, entryTaper, strokeT);
    float exit = 1.0 - smoothstep(1.0 - exitTaper, 1.0, strokeT);
    float contact = pow(entry * exit, 1.12);
    return 0.018 + contact * 0.982;
}

float bokushoCanvasSample(uint strokeIndex, float sampleIndex, float tuftIndex)
{
    uint sampleCount = max((uint)round(bokushoShape.x), 2u);
    uint tuftCount = max((uint)round(bokushoShape.y), 1u);
    float clampedSample = clamp(sampleIndex, 0.0, (float)(sampleCount - 1u));
    float clampedTuft = clamp(tuftIndex, 0.0, (float)(tuftCount - 1u));
    uint sample0 = min((uint)floor(clampedSample), sampleCount - 1u);
    uint sample1 = min(sample0 + 1u, sampleCount - 1u);
    uint tuft0 = min((uint)floor(clampedTuft), tuftCount - 1u);
    uint tuft1 = min(tuft0 + 1u, tuftCount - 1u);
    float sampleBlend = saturate(clampedSample - (float)sample0);
    float tuftBlend = saturate(clampedTuft - (float)tuft0);
    uint rowOffset = strokeIndex * tuftCount;
    float p00 = BokushoCanvasField[(rowOffset + tuft0) * sampleCount + sample0];
    float p10 = BokushoCanvasField[(rowOffset + tuft0) * sampleCount + sample1];
    float p01 = BokushoCanvasField[(rowOffset + tuft1) * sampleCount + sample0];
    float p11 = BokushoCanvasField[(rowOffset + tuft1) * sampleCount + sample1];
    float lower = cultmath_lerp(p00, p10, sampleBlend);
    float upper = cultmath_lerp(p01, p11, sampleBlend);
    return saturate(cultmath_lerp(lower, upper, tuftBlend));
}

float4 bokushoTipSample(uint strokeIndex, uint sampleIndex, uint tuftIndex)
{
    uint sampleCount = max((uint)round(bokushoShape.x), 2u);
    uint tuftCount = max((uint)round(bokushoShape.y), 1u);
    uint rowOffset = strokeIndex * tuftCount;
    return BokushoTipField[(rowOffset + min(tuftIndex, tuftCount - 1u)) * sampleCount + min(sampleIndex, sampleCount - 1u)];
}

float bokushoStrokePageHeight(float2 world, BokushoBrushStroke stroke, uint strokeIndex)
{
    float bestDistance = 1.0e20;
    float bestT = 0.0;
    [unroll]
    for (uint scan = 0u; scan < 16u; scan += 1u)
    {
        float t = (float)scan / 15.0;
        float2 center = bokushoStrokePoint(stroke, t);
        float distanceSquared = dot(world - center, world - center);
        if (distanceSquared < bestDistance)
        {
            bestDistance = distanceSquared;
            bestT = t;
        }
    }

    [unroll]
    for (uint refine = 0u; refine < 3u; refine += 1u)
    {
        float span = 1.0 / (15.0 * pow(2.0, (float)refine));
        float leftT = saturate(bestT - span);
        float rightT = saturate(bestT + span);
        float2 left = bokushoStrokePoint(stroke, leftT);
        float2 right = bokushoStrokePoint(stroke, rightT);
        float leftDistance = dot(world - left, world - left);
        float rightDistance = dot(world - right, world - right);
        if (leftDistance < bestDistance)
        {
            bestDistance = leftDistance;
            bestT = leftT;
        }

        if (rightDistance < bestDistance)
        {
            bestDistance = rightDistance;
            bestT = rightT;
        }
    }

    uint sampleCount = max((uint)round(bokushoShape.x), 2u);
    uint tuftCount = max((uint)round(bokushoShape.y), 1u);
    float2 center = bokushoStrokePoint(stroke, bestT);
    float2 tangent = bokushoStrokeTangent(stroke, bestT);
    float2 normal = float2(-tangent.y, tangent.x);
    float lateral = dot(world - center, normal);
    float taper = bokushoStrokeTaper(stroke, bestT);
    float radius = max(bokushoMaterial.x * stroke.profile.x * stroke.profile.z * (0.18 + taper * 0.82), 0.0001);
    float splay = saturate(bokushoDynamics.x / 2.0);
    float pressure = saturate(bokushoMaterial.y * stroke.profile.y * 0.5) * taper;
    float wetSpread = 0.86 + saturate(bokushoMaterial.w / 1.6) * 0.18;
    float footprint = radius * wetSpread * (0.72 + splay * 0.44 + pressure * 0.22);
    float tuftT = saturate(lateral / max(footprint, 0.001) * 0.5 + 0.5);
    float distance = sqrt(bestDistance);
    float contact = smoothstep(1.0, 0.0, distance / max(footprint * 0.96, 0.001));
    float contactCore = contact * contact * (3.0 - 2.0 * contact);
    uint centerSample = min((uint)round(bestT * (float)(sampleCount - 1u)), sampleCount - 1u);
    uint centerTuft = min((uint)round(tuftT * (float)max((int)tuftCount - 1, 0)), tuftCount - 1u);
    float pigmentPeak = 0.0;
    float pigmentFlow = 0.0;

    [unroll]
    for (int sampleDelta = -3; sampleDelta <= 3; sampleDelta += 1)
    {
        int rawSampleIndex = (int)centerSample + sampleDelta;
        uint sampleStrokeIndex = strokeIndex;
        BokushoBrushStroke sampleStroke = stroke;
        int resolvedSampleIndex = clamp(rawSampleIndex, 0, (int)sampleCount - 1);
        uint sampleIndex = min((uint)max(resolvedSampleIndex, 0), sampleCount - 1u);
        float sampleT = (float)sampleIndex / max((float)(sampleCount - 1u), 1.0);
        float2 sampleTangent = bokushoStrokeTangent(sampleStroke, sampleT);
        float2 sampleNormal = float2(-sampleTangent.y, sampleTangent.x);

        [unroll]
        for (int tuftDelta = -2; tuftDelta <= 2; tuftDelta += 1)
        {
            uint tuftIndex = min((uint)max((int)centerTuft + tuftDelta, 0), tuftCount - 1u);
            float4 tip = bokushoTipSample(sampleStrokeIndex, sampleIndex, tuftIndex);
            uint previousStrokeIndex = sampleStrokeIndex;
            int previousSampleIndexValue = max((int)sampleIndex - 1, 0);
            uint previousSampleIndex = min((uint)max(previousSampleIndexValue, 0), sampleCount - 1u);
            float4 previousTip = bokushoTipSample(previousStrokeIndex, previousSampleIndex, tuftIndex);
            float2 sweep = tip.xy - previousTip.xy;
            float sweepLengthSquared = dot(sweep, sweep);
            float sweepT = sweepLengthSquared <= 0.000001 ? 1.0 : saturate(dot(world - previousTip.xy, sweep) / sweepLengthSquared);
            float2 contactPoint = previousTip.xy + sweep * sweepT;
            float sweepDistance = length(world - contactPoint);
            float sweepContact = smoothstep(1.0, 0.0, sweepDistance / max(max(tip.z, previousTip.z) * 1.04, 0.001));
            float2 delta = world - contactPoint;
            float normalDistance = dot(delta, sampleNormal) / max(tip.z, 0.001);
            float tangentDistance = dot(delta, sampleTangent) / max(tip.w, 0.001);
            float ellipse = sqrt(normalDistance * normalDistance + tangentDistance * tangentDistance);
            float tipContact = max(smoothstep(1.0, 0.0, ellipse), sweepContact * 0.86);
            float longitudinalGate = smoothstep(1.0, 0.0, abs(tangentDistance) * 0.62);
            float pigment = BokushoCanvasField[((sampleStrokeIndex * tuftCount) + tuftIndex) * sampleCount + sampleIndex];
            float contribution = pigment
                * tipContact
                * (0.006 + contactCore * 0.994)
                * (0.06 + longitudinalGate * 0.94)
                * (1.0 - abs((float)sampleDelta) * 0.070)
                * (0.82 + sweepContact * 0.24);
            pigmentPeak = max(pigmentPeak, contribution);
            pigmentFlow += contribution;
        }
    }

    float decisiveInk = saturate((pigmentPeak - 0.012) * 2.25 + pigmentFlow * 0.002);
    return decisiveInk * (0.44 + pressure * 0.36 + saturate(bokushoMaterial.z * 0.5) * 0.20);
}

float bokushoPageHeight(float2 world)
{
    uint strokeCount = min(max((uint)round(bokushoShape.w), 0u), 64u);
    float height = 0.0;
    [loop]
    for (uint strokeIndex = 0u; strokeIndex < strokeCount; strokeIndex += 1u)
    {
        height += bokushoStrokePageHeight(world, BokushoBrushStrokes[strokeIndex], strokeIndex);
    }

    return saturate(height);
}

float powerPulse(float distanceValue, float radius, float power)
{
    float normalized = saturate(distanceValue / max(radius, 0.001));
    float shaped = pow(1.0 - normalized, power);
    return shaped * shaped * (3.0 - 2.0 * shaped);
}

float compactGaussianPulse(float normalizedRadiusSquared, float falloff, float shapePower)
{
    if (normalizedRadiusSquared >= 1.0)
    {
        return 0.0;
    }

    float edgeValue = exp(-falloff);
    float gaussianValue = exp(-falloff * normalizedRadiusSquared);
    float compactValue = (gaussianValue - edgeValue) / max(1.0 - edgeValue, 0.000001);
    return pow(saturate(compactValue), shapePower);
}

VertexOut FullscreenTriangleVS(uint vertexId : SV_VertexID)
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    VertexOut output;
    output.position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.uv = uv;
    return output;
}

float D3D12HeightFieldBasePS(VertexOut input) : SV_Target
{
    float2 world = viewWorld(saturate(input.uv));
    float slow = sin((world.x * 0.08 + world.y * 0.06) + timeSeconds * 0.27)
        * sin((world.x * -0.04 + world.y * 0.07) - timeSeconds * 0.19) * 0.035;
    return slow + bokushoPageHeight(world) * 5.0;
}

BrushVertexOut D3D12HeightFieldBrushVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    float2 corners[6] =
    {
        float2(-1.0, -1.0),
        float2(1.0, -1.0),
        float2(1.0, 1.0),
        float2(-1.0, -1.0),
        float2(1.0, 1.0),
        float2(-1.0, 1.0),
    };

    float4 centerRadius = brushCenterRadius[instanceId];
    float2 radii = float2(centerRadius.z, centerRadius.w > 0.0 ? centerRadius.w : centerRadius.z);
    float supportRadius = max(radii.x, radii.y);
    float2 world = centerRadius.xy + corners[vertexId] * supportRadius;
    float2 uv = viewUv(world);

    BrushVertexOut output;
    output.position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.uv = uv;
    output.brushIndex = instanceId;
    output.centerRadius = centerRadius;
    output.shape = brushShape[instanceId];
    output.wave = brushWave[instanceId];
    return output;
}

float D3D12HeightFieldBrushPS(BrushVertexOut input) : SV_Target
{
    float2 world = viewWorld(saturate(input.uv));
    float2 delta = world - input.centerRadius.xy;
    float2 radii = float2(input.centerRadius.z, input.centerRadius.w > 0.0 ? input.centerRadius.w : input.centerRadius.z);
    float distanceValue = length(delta);
    float normalizedDistance = saturate(distanceValue / max(input.centerRadius.z, 0.001));
    float well = powerPulse(distanceValue, input.centerRadius.z, input.shape.x);
    if (input.shape.w > 0.0)
    {
        float c = cos(input.shape.z);
        float s = sin(input.shape.z);
        float2 local = float2(delta.x * c + delta.y * s, -delta.x * s + delta.y * c);
        float2 normalized = local / max(radii, float2(0.001, 0.001));
        float normalizedRadiusSquared = dot(normalized, normalized);
        normalizedDistance = saturate(sqrt(normalizedRadiusSquared));
        well = compactGaussianPulse(normalizedRadiusSquared, input.shape.w, input.shape.x);
    }

    float legacyPhase = distanceValue * input.wave.y - timeSeconds * input.wave.z;
    float radialPhase = pow(normalizedDistance, input.wave.w) * input.wave.y - timeSeconds * input.wave.z;
    float ripple = input.wave.w > 0.0 ? cos(radialPhase) : sin(legacyPhase);
    float signedHeight = input.shape.y * well + ripple * well * input.wave.x;
    return signedHeight;
}
