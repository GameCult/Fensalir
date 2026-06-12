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
    float4 bokushoShape;     // sampleCount, tuftCount, physicsHz, hasInput
    float4 bokushoMaterial;  // radius, pressure, inkLoad, wetness
    float4 bokushoDynamics;  // splay, bend, friction, reserved
    float4 bokushoStrokeP0;
    float4 bokushoStrokeP1;
    float4 bokushoStrokeP2;
    float4 bokushoStrokeP3;
};

StructuredBuffer<float> BokushoCanvasField : register(t77);

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

float2 bokushoStrokePoint(float t)
{
    return cultmath_catmullrom(bokushoStrokeP0.xy, bokushoStrokeP1.xy, bokushoStrokeP2.xy, bokushoStrokeP3.xy, t);
}

float2 bokushoStrokeTangent(float t)
{
    float dt = 1.0 / max(bokushoShape.x - 1.0, 1.0);
    float2 before = bokushoStrokePoint(saturate(t - dt));
    float2 after = bokushoStrokePoint(saturate(t + dt));
    return cultmath_normalize(after - before);
}

float bokushoCanvasSample(float sampleIndex, float tuftIndex)
{
    uint sampleCount = max((uint)round(bokushoShape.x), 2u);
    uint tuftCount = max((uint)round(bokushoShape.y), 1u);
    uint sample = min((uint)round(sampleIndex), sampleCount - 1u);
    uint tuft = min((uint)round(tuftIndex), tuftCount - 1u);
    return saturate(BokushoCanvasField[tuft * sampleCount + sample]);
}

float bokushoPageHeight(float2 world)
{
    if (bokushoShape.w <= 0.0)
    {
        return 0.0;
    }

    float bestDistance = 1.0e20;
    float bestT = 0.0;
    [unroll]
    for (uint scan = 0u; scan < 16u; scan += 1u)
    {
        float t = (float)scan / 15.0;
        float2 center = bokushoStrokePoint(t);
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
        float2 left = bokushoStrokePoint(leftT);
        float2 right = bokushoStrokePoint(rightT);
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

    float2 center = bokushoStrokePoint(bestT);
    float2 tangent = bokushoStrokeTangent(bestT);
    float2 normal = float2(-tangent.y, tangent.x);
    float lateral = dot(world - center, normal);
    float radius = max(bokushoMaterial.x, 0.0001);
    float splay = saturate(bokushoDynamics.x / 2.0);
    float footprint = radius * (0.42 + splay * 0.74 + saturate(bokushoMaterial.y * 0.5) * 0.18);
    float tuftT = saturate(lateral / max(footprint, 0.001) * 0.5 + 0.5);
    float distance = sqrt(bestDistance);
    float contact = smoothstep(1.0, 0.0, distance / max(footprint * 0.80, 0.001));
    float sampleIndex = bestT * max(bokushoShape.x - 1.0, 1.0);
    float tuftIndex = tuftT * max(bokushoShape.y - 1.0, 0.0);
    float pigment = bokushoCanvasSample(sampleIndex, tuftIndex);
    float pressure = saturate(bokushoMaterial.y * 0.5);
    return pigment * contact * (0.10 + pressure * 0.18 + saturate(bokushoMaterial.z * 0.5) * 0.08);
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
    return slow + bokushoPageHeight(world);
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
