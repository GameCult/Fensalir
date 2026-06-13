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

float bokushoStrokeNormalRadiusShape(float taper)
{
    return 0.08 + taper * 0.92;
}

float bokushoStrokePressureShape(BokushoBrushStroke stroke, float t)
{
    float strokeT = saturate(cultmath_lerp(stroke.p0.z, max(stroke.p0.z, stroke.p0.w), t));
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float pressIn = smoothstep(0.0, min(entryTaper * 1.6, 0.62), strokeT);
    float liftOut = 1.0 - smoothstep(max(1.0 - exitTaper * 1.35, 0.20), 1.0, strokeT);
    float belly = smoothstep(0.10, 0.36, strokeT) * (1.0 - smoothstep(0.66, 0.94, strokeT));
    float entryLift = 1.0 - smoothstep(0.0, min(entryTaper * 1.15, 0.42), strokeT);
    float exitLift = smoothstep(max(1.0 - exitTaper * 1.45, 0.18), 1.0, strokeT);
    return saturate(0.40 + pressIn * liftOut * 0.34 + belly * 0.42 - entryLift * 0.12 - exitLift * 0.18);
}

float bokushoStrokeSpeed(BokushoBrushStroke stroke, float t)
{
    float dt = 1.0 / max(bokushoShape.x - 1.0, 1.0);
    float2 before = bokushoStrokePoint(stroke, saturate(t - dt));
    float2 after = bokushoStrokePoint(stroke, saturate(t + dt));
    return length(after - before) / max(dt * 2.0, 0.001);
}

float2 bokushoStrokeLocalTangent(BokushoBrushStroke stroke, float t, float dt)
{
    float2 before = bokushoStrokePoint(stroke, saturate(t - dt));
    float2 after = bokushoStrokePoint(stroke, saturate(t + dt));
    return cultmath_normalize(after - before);
}

float bokushoStrokeTurn(BokushoBrushStroke stroke, float t)
{
    float dt = 1.0 / max(bokushoShape.x - 1.0, 1.0);
    float2 before = bokushoStrokeLocalTangent(stroke, saturate(t - dt), dt);
    float2 after = bokushoStrokeLocalTangent(stroke, saturate(t + dt), dt);
    return saturate(abs(before.x * after.y - before.y * after.x) * 1.8);
}

float bokushoStrokeGesturePressureShape(BokushoBrushStroke stroke, float t)
{
    float localSpeed = bokushoStrokeSpeed(stroke, t);
    float expectedSpeed = max(length(stroke.p3.xy - stroke.p0.xy), 0.001);
    float slowPress = saturate((expectedSpeed * 1.18 - localSpeed) / max(expectedSpeed * 0.80, 0.001));
    float turnPress = bokushoStrokeTurn(stroke, t);
    return saturate(0.88 + slowPress * 0.30 + turnPress * 0.20);
}

float bokushoStrokeGestureWidthShape(BokushoBrushStroke stroke, float t)
{
    float localSpeed = bokushoStrokeSpeed(stroke, t);
    float expectedSpeed = max(length(stroke.p3.xy - stroke.p0.xy), 0.001);
    float slowSpread = saturate((expectedSpeed * 1.08 - localSpeed) / max(expectedSpeed * 0.85, 0.001));
    float turnSpread = bokushoStrokeTurn(stroke, t);
    float strokeT = saturate(cultmath_lerp(stroke.p0.z, max(stroke.p0.z, stroke.p0.w), t));
    float entryTaper = clamp(stroke.dynamics.x, 0.01, 0.50);
    float exitTaper = clamp(stroke.dynamics.y, 0.01, 0.50);
    float belly = smoothstep(0.10, 0.42, strokeT) * (1.0 - smoothstep(0.68, 0.97, strokeT));
    float entryLift = 1.0 - smoothstep(0.0, min(entryTaper * 1.35, 0.44), strokeT);
    float exitLift = smoothstep(max(1.0 - exitTaper * 1.55, 0.16), 1.0, strokeT);
    return saturate(0.68 + slowSpread * 0.26 + turnSpread * 0.18 + belly * 0.36 - entryLift * 0.10 - exitLift * 0.24);
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

float bokushoHashNoiseCell(float2 paperPoint, uint strokeIndex, uint salt)
{
    int2 cell = int2(floor(paperPoint));
    uint x = (uint)cell.x;
    uint y = (uint)cell.y;
    uint value = (strokeIndex + 1u) * 0x9E3779B9u ^ x * 0x85EBCA6Bu ^ y * 0xC2B2AE35u ^ salt;
    value ^= value >> 16;
    value *= 0x7FEB352Du;
    value ^= value >> 15;
    value *= 0x846CA68Bu;
    value ^= value >> 16;
    return (float)(value & 0x00FFFFFFu) / 16777215.0;
}

float bokushoPaperTooth(float2 world, uint strokeIndex)
{
    float fine = bokushoHashNoiseCell(world * 22.0, strokeIndex, 0x6A09E667u);
    float fiber = bokushoHashNoiseCell(float2(world.x * 7.0 + world.y * 0.35, world.y * 2.4), strokeIndex, 0xBB67AE85u);
    return saturate(fine * 0.58 + fiber * 0.42);
}

bool bokushoCanBorrowSourceSample(uint strokeIndex, int candidateIndex, uint strokeCount)
{
    if (candidateIndex < 0 || (uint)candidateIndex >= strokeCount)
    {
        return false;
    }

    float sourceStrokeId = BokushoBrushStrokes[strokeIndex].p3.w;
    return sourceStrokeId >= 0.0 && abs(BokushoBrushStrokes[(uint)candidateIndex].p3.w - sourceStrokeId) <= 0.5;
}

float bokushoInternalSegmentProjectionGate(uint strokeIndex, BokushoBrushStroke stroke, float t, uint strokeCount)
{
    float gate = 1.0;
    if (stroke.p0.z > 0.0001 && bokushoCanBorrowSourceSample(strokeIndex, (int)strokeIndex - 1, strokeCount))
    {
        gate *= 0.34 + smoothstep(0.0, 0.16, t) * 0.66;
    }

    if (stroke.p0.w < 0.9999 && bokushoCanBorrowSourceSample(strokeIndex, (int)strokeIndex + 1, strokeCount))
    {
        gate *= 0.34 + (1.0 - smoothstep(0.84, 1.0, t)) * 0.66;
    }

    return gate;
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
    uint strokeCount = min(max((uint)round(bokushoShape.w), 0u), 64u);
    float2 center = bokushoStrokePoint(stroke, bestT);
    float2 tangent = bokushoStrokeTangent(stroke, bestT);
    float2 normal = float2(-tangent.y, tangent.x);
    float projectionGate = bokushoInternalSegmentProjectionGate(strokeIndex, stroke, bestT, strokeCount);
    float lateral = dot(world - center, normal);
    float taper = bokushoStrokeTaper(stroke, bestT);
    float gestureWidth = bokushoStrokeGestureWidthShape(stroke, bestT);
    float poseSpread = saturate(0.92 + abs(stroke.pose.x) * 0.22 + stroke.pose.w * 0.10 - stroke.pose.z * 0.04);
    float radius = max(bokushoMaterial.x * stroke.profile.x * stroke.profile.z * bokushoStrokeNormalRadiusShape(taper) * gestureWidth * poseSpread, 0.0001);
    float splay = saturate(bokushoDynamics.x / 2.0);
    float poseContact = 0.90 + stroke.pose.w * 0.10 + (1.0 - saturate(stroke.pose.z / 2.4)) * 0.10;
    float pressure = saturate(bokushoMaterial.y * stroke.profile.y * 0.5) * taper * bokushoStrokePressureShape(stroke, bestT) * bokushoStrokeGesturePressureShape(stroke, bestT) * poseContact;
    float footprint = radius * (0.42 + splay * 0.74 + pressure * 0.18);
    float tuftT = saturate(lateral / max(footprint, 0.001) * 0.5 + 0.5);
    float distance = sqrt(bestDistance);
    float contact = smoothstep(1.0, 0.0, distance / max(footprint * 0.80, 0.001));
    float contactCore = contact * contact * (3.0 - 2.0 * contact);
    uint paperKey = stroke.p3.w >= 0.0 ? (uint)round(stroke.p3.w) : strokeIndex;
    float tooth = bokushoPaperTooth(world, paperKey);
    float edge = saturate(distance / max(footprint * 0.80, 0.001));
    float dryIslandLift = smoothstep(0.66, 0.20, saturate(bokushoMaterial.w / 1.6));
    float dryBreak = smoothstep(0.18 + tooth * 0.18, 0.92, edge)
        * (1.0 - saturate(bokushoMaterial.w / 1.6))
        * (0.34 + stroke.dynamics.w * 0.12);
    float hold = saturate(0.52 + tooth * 0.42 + pressure * 0.28 - dryBreak);
    uint centerSample = min((uint)round(bestT * (float)(sampleCount - 1u)), sampleCount - 1u);
    uint centerTuft = min((uint)round(tuftT * (float)max((int)tuftCount - 1, 0)), tuftCount - 1u);
    float pigmentPeak = 0.0;
    float pigmentFlow = 0.0;
    float dryIslandPeak = 0.0;
    float dryFleckPeak = 0.0;

    [unroll]
    for (int sampleDelta = -3; sampleDelta <= 3; sampleDelta += 1)
    {
        int rawSampleIndex = (int)centerSample + sampleDelta;
        uint sampleStrokeIndex = strokeIndex;
        BokushoBrushStroke sampleStroke = stroke;
        int resolvedSampleIndex = rawSampleIndex;
        if (rawSampleIndex < 0)
        {
            if (bokushoCanBorrowSourceSample(strokeIndex, (int)strokeIndex - 1, strokeCount))
            {
                sampleStrokeIndex = strokeIndex - 1u;
                sampleStroke = BokushoBrushStrokes[sampleStrokeIndex];
                resolvedSampleIndex = (int)sampleCount + rawSampleIndex;
            }
            else
            {
                resolvedSampleIndex = 0;
            }
        }
        else if (rawSampleIndex >= (int)sampleCount)
        {
            if (bokushoCanBorrowSourceSample(strokeIndex, (int)strokeIndex + 1, strokeCount))
            {
                sampleStrokeIndex = strokeIndex + 1u;
                sampleStroke = BokushoBrushStrokes[sampleStrokeIndex];
                resolvedSampleIndex = rawSampleIndex - (int)sampleCount;
            }
            else
            {
                resolvedSampleIndex = (int)sampleCount - 1;
            }
        }

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
            int previousSampleIndexValue = (int)sampleIndex - 1;
            if (previousSampleIndexValue < 0)
            {
                if (bokushoCanBorrowSourceSample(sampleStrokeIndex, (int)sampleStrokeIndex - 1, strokeCount))
                {
                    previousStrokeIndex = sampleStrokeIndex - 1u;
                    previousSampleIndexValue = (int)sampleCount - 1;
                }
                else
                {
                    previousSampleIndexValue = 0;
                }
            }

            uint previousSampleIndex = min((uint)max(previousSampleIndexValue, 0), sampleCount - 1u);
            float4 previousTip = bokushoTipSample(previousStrokeIndex, previousSampleIndex, tuftIndex);
            uint nextSampleIndex = min(sampleIndex + 1u, sampleCount - 1u);
            float previousPigment = BokushoCanvasField[((previousStrokeIndex * tuftCount) + tuftIndex) * sampleCount + previousSampleIndex];
            float nextPigment = BokushoCanvasField[((sampleStrokeIndex * tuftCount) + tuftIndex) * sampleCount + nextSampleIndex];
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
            float neighborPigment = max(previousPigment, nextPigment);
            float releasePigment = smoothstep(0.12, 0.68, pigment) * smoothstep(0.030, 0.16, pigment - neighborPigment);
            float contribution = pigment
                * tipContact
                * (0.006 + contactCore * 0.994)
                * (0.06 + longitudinalGate * 0.94)
                * (1.0 - abs((float)sampleDelta) * 0.070)
                * (0.82 + sweepContact * 0.24);
            pigmentPeak = max(pigmentPeak, contribution);
            pigmentFlow += contribution;

            float laneT = tuftCount <= 1u ? 0.5 : (float)tuftIndex / (float)(tuftCount - 1u);
            float laneEdge = abs(laneT * 2.0 - 1.0);
            float2 islandSeedPoint = contactPoint * 34.0 + float2((float)tuftIndex * 0.19, (float)sampleIndex * 0.13);
            float islandSeed = bokushoHashNoiseCell(islandSeedPoint, paperKey, 0x510E527Fu);
            float thinPigment = smoothstep(0.006, 0.075, pigment) * (1.0 - smoothstep(0.22, 0.55, pigment));
            float islandPigment = max(thinPigment, releasePigment * 0.72);
            float islandGate = smoothstep(0.38, 0.88, laneEdge)
                * dryIslandLift
                * islandPigment
                * smoothstep(0.25, 0.88, tooth)
                * smoothstep(0.42, 0.92, islandSeed);
            if (islandGate > 0.0)
            {
                float side = islandSeed < 0.78 ? -1.0 : 1.0;
                float2 islandCenter = contactPoint
                    + sampleNormal * side * tip.z * (0.88 + laneEdge * 0.55)
                    + sampleTangent * (islandSeed - 0.5) * tip.w * 0.22;
                float islandRadius = max(tip.z * (0.30 + laneEdge * 0.22), 0.060);
                float islandDistance = length(world - islandCenter);
                float islandContact = smoothstep(1.0, 0.0, islandDistance / islandRadius);
                dryIslandPeak = max(dryIslandPeak, pigment * islandGate * islandContact * 5.2);

                float2 fleckSeedPoint = contactPoint * 61.0 + float2((float)sampleIndex * 0.17, (float)tuftIndex * 0.23);
                float fleckSeed = bokushoHashNoiseCell(fleckSeedPoint, paperKey, 0xF1E57A2Du);
                float fleckGate = islandGate * smoothstep(0.44 - releasePigment * 0.18, 0.88, fleckSeed) * (1.0 + releasePigment * 1.35);
                if (fleckGate > 0.0)
                {
                    float fleckSide = fleckSeed < 0.86 ? side : -side;
                    float2 fleckCenter = contactPoint
                        + sampleNormal * fleckSide * tip.z * (2.30 + laneEdge * 1.05 + fleckSeed * 0.55)
                        + sampleTangent * (fleckSeed - 0.5) * tip.w * 0.50;
                    float fleckRadius = max(tip.z * (0.12 + laneEdge * 0.05), 0.030);
                    float fleckDistance = length(world - fleckCenter);
                    float fleckContact = smoothstep(1.0, 0.0, fleckDistance / fleckRadius);
                    dryFleckPeak = max(dryFleckPeak, pigment * fleckGate * fleckContact * 22.0);
                }

                float2 fraySeedPoint = contactPoint * 47.0 + float2((float)sampleIndex * 0.29, (float)tuftIndex * 0.37);
                float fraySeed = bokushoHashNoiseCell(fraySeedPoint, paperKey, 0x8D3F9A21u);
                float frayGate = islandGate
                    * releasePigment
                    * smoothstep(0.28, 0.82, fraySeed);
                if (frayGate > 0.0)
                {
                    float fraySide = fraySeed < 0.70 ? side : -side;
                    float2 frayCenter = contactPoint
                        + sampleNormal * fraySide * tip.z * (1.48 + laneEdge * 0.62 + fraySeed * 0.30)
                        + sampleTangent * (fraySeed - 0.5) * tip.w * 0.34;
                    float frayRadius = max(tip.z * (0.095 + laneEdge * 0.040), 0.024);
                    float frayDistance = length(world - frayCenter);
                    float frayContact = smoothstep(1.0, 0.0, frayDistance / frayRadius);
                    dryFleckPeak = max(dryFleckPeak, pigment * frayGate * frayContact * 26.0);
                }

                float2 satelliteSeedPoint = contactPoint * 83.0 + float2((float)tuftIndex * 0.31, (float)sampleIndex * 0.11);
                float satelliteSeed = bokushoHashNoiseCell(satelliteSeedPoint, paperKey, 0x5A771EAFu);
                float satelliteGate = islandGate
                    * releasePigment
                    * smoothstep(0.36, 0.86, satelliteSeed);
                if (satelliteGate > 0.0)
                {
                    float satelliteSide = satelliteSeed < 0.52 ? side : -side;
                    float2 satelliteCenter = contactPoint
                        + sampleNormal * satelliteSide * tip.z * (2.34 + laneEdge * 0.96 + satelliteSeed * 0.46)
                        + sampleTangent * (satelliteSeed - 0.5) * tip.w * 0.64;
                    float satelliteRadius = max(tip.z * (0.075 + laneEdge * 0.035), 0.020);
                    float satelliteDistance = length(world - satelliteCenter);
                    float satelliteContact = smoothstep(1.0, 0.0, satelliteDistance / satelliteRadius);
                    dryFleckPeak = max(dryFleckPeak, pigment * satelliteGate * satelliteContact * 38.0);
                }
            }
        }
    }

    float decisiveInk = saturate((pigmentPeak - 0.012) * 2.25 + pigmentFlow * 0.002);
    float islandInk = saturate((dryIslandPeak - 0.001) * 14.0);
    float fleckInk = saturate((dryFleckPeak - 0.001) * 36.0);
    return saturate(decisiveInk + islandInk + fleckInk) * hold * projectionGate * (0.36 + pressure * 0.44 + saturate(bokushoMaterial.z * 0.5) * 0.20);
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
