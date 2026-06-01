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
    float4 cameraFrustumXy;
    float4 cameraFrustumZ;
};

Texture2D<float4> heightFieldTexture : register(t0);
TextureCube<float4> studioPmremTexture : register(t22);
Texture2D<float> blueNoiseTexture : register(t28);
SamplerState linearSampler : register(s0);

static const float FIELD_ID_HEIGHT_FIELD = 4.0;
static const float PI = 3.14159265359;
static const float HEIGHT_FIELD_TEXEL_COUNT = 128.0;
static const float SURFACE_FLAT_REFLECTION_MAX_LOD = 3.0;
static const float BACKGROUND_PMREM_LOD = 3.0;
static const float BACKGROUND_PMREM_CONE = 0.16;
static const float SURFACE_FLAT_SLOPE_START = 0.018;
static const float SURFACE_FLAT_SLOPE_END = 0.16;

struct VertexOut
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

struct SceneOut
{
    // alpha does not live in colorTravel.a. That lane is camera travel for temporal
    // reprojection; coverage/confidence belong in control and reservoirGuide.
    float4 colorTravel : SV_Target0;
    float4 metadata : SV_Target1;
    float4 control : SV_Target2;
    float4 reservoirGuide : SV_Target3;
    float overdraw : SV_Target4;
    float depth : SV_Depth;
};

struct RayMarchResult
{
    float3 color;
    float travel;
    float fieldId;
    float3 normal;
    float coverage;
    float stepCount;
};

VertexOut FullscreenTriangleVS(uint vertexId : SV_VertexID)
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    VertexOut output;
    output.position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.uv = uv;
    return output;
}

void cameraBasis(float3 camera, float3 target, out float3 forward, out float3 right, out float3 up)
{
    forward = normalize(target - camera);
    float3 worldUp = abs(forward.y) > 0.96 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    right = normalize(cross(worldUp, forward));
    up = normalize(cross(forward, right));
}

float3 rayDirectionForPixel(float2 pixel, float2 jitter, float3 camera, float3 target)
{
    float2 uv = (pixel + jitter) / max(resolution, float2(1.0, 1.0));
    float x = lerp(cameraFrustumXy.x, cameraFrustumXy.y, uv.x);
    float y = lerp(cameraFrustumXy.z, cameraFrustumXy.w, uv.y);
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(camera, target, forward, right, up);
    return normalize(forward + right * x + up * y);
}

float4 projectCameraSpace(float3 view)
{
    float z = max(view.z, 0.0001);
    float2 slope = view.xy / z;
    float2 frustumMin = float2(cameraFrustumXy.x, cameraFrustumXy.z);
    float2 frustumMax = float2(cameraFrustumXy.y, cameraFrustumXy.w);
    float2 uv = (slope - frustumMin) / max(frustumMax - frustumMin, float2(0.0001, 0.0001));
    return float4(uv * 2.0 - 1.0, saturate(z / max(farDistance, 0.0001)), 1.0);
}

float2 viewLocal(float2 p)
{
    return (p - viewCenter) / max(viewRadius, 0.001);
}

float2 viewUv(float2 p)
{
    return viewLocal(p) * 0.5 + 0.5;
}

float terrainHeight(float2 p)
{
    return heightFieldTexture.SampleLevel(linearSampler, saturate(viewUv(p)), 0.0).r;
}

float2 terrainGradient(float2 p)
{
    float2 uv = saturate(viewUv(p));
    float2 texel = 1.0 / HEIGHT_FIELD_TEXEL_COUNT;
    float texelWorld = max((viewRadius * 2.0) / HEIGHT_FIELD_TEXEL_COUNT, 0.001);

    float hLeft = heightFieldTexture.SampleLevel(linearSampler, uv - float2(texel.x, 0.0), 0.0).r;
    float hRight = heightFieldTexture.SampleLevel(linearSampler, uv + float2(texel.x, 0.0), 0.0).r;
    float hDown = heightFieldTexture.SampleLevel(linearSampler, uv - float2(0.0, texel.y), 0.0).r;
    float hUp = heightFieldTexture.SampleLevel(linearSampler, uv + float2(0.0, texel.y), 0.0).r;

    return float2(hRight - hLeft, hUp - hDown) / (texelWorld * 2.0);
}

bool traceHeightFieldSurfaceDirect(float3 origin, float3 direction, float intervalStart, float intervalEnd, out float3 hitPosition, out float travel)
{
    travel = max(intervalStart, 0.0);
    float previousTravel = travel;
    hitPosition = origin + direction * travel;
    float previousGap = hitPosition.z - terrainHeight(hitPosition.xy);
    float radius = max(viewRadius, 0.001);

    [loop]
    for (int stepIndex = 0; stepIndex < 96; stepIndex++)
    {
        hitPosition = origin + direction * travel;
        float2 local = (hitPosition.xy - viewCenter) / radius;
        if (length(local) > 1.08 && hitPosition.z < 4.0)
        {
            return false;
        }

        float gap = hitPosition.z - terrainHeight(hitPosition.xy);
        float hitEpsilon = max(0.002, travel * 0.00035);
        if (length(local) <= 1.0 && (abs(gap) <= hitEpsilon || (previousGap > 0.0 && gap <= 0.0)))
        {
            float alpha = previousGap / max(previousGap - gap, 0.0001);
            travel = lerp(previousTravel, travel, saturate(alpha));
            hitPosition = origin + direction * travel;
            return travel > intervalStart && travel < intervalEnd && travel < farDistance;
        }

        float2 slope = terrainGradient(hitPosition.xy);
        float terrainRate = abs(direction.z - dot(slope, direction.xy));
        float terrainStep = gap > 0.0 ? gap / max(terrainRate, 0.22) : 0.026;
        terrainStep = min(terrainStep * 0.62, max(viewRadius * 0.08, 0.026));
        previousTravel = travel;
        previousGap = gap;
        travel += max(terrainStep, 0.026);
        if (travel > intervalEnd || travel > farDistance)
        {
            return false;
        }
    }

    return false;
}

float3 studioPmremDirection(float3 worldDirection)
{
    return normalize(float3(worldDirection.x, worldDirection.z, worldDirection.y));
}

void directionBasis(float3 direction, out float3 tangent, out float3 bitangent)
{
    float3 up = abs(direction.z) > 0.94 ? float3(0.0, 1.0, 0.0) : float3(0.0, 0.0, 1.0);
    tangent = normalize(cross(up, direction));
    bitangent = cross(direction, tangent);
}

float3 studioPmremSample(float3 worldDirection, float lod)
{
    return studioPmremTexture.SampleLevel(linearSampler, studioPmremDirection(worldDirection), lod).rgb;
}

float3 studioPmremConeSample(float3 worldDirection, float lod, float cone)
{
    float3 direction = normalize(worldDirection);
    float3 tangent;
    float3 bitangent;
    directionBasis(direction, tangent, bitangent);

    float3 sum = studioPmremSample(direction, lod) * 2.0;
    sum += studioPmremSample(normalize(direction + tangent * cone), lod);
    sum += studioPmremSample(normalize(direction - tangent * cone), lod);
    sum += studioPmremSample(normalize(direction + bitangent * cone), lod);
    sum += studioPmremSample(normalize(direction - bitangent * cone), lod);
    sum += studioPmremSample(normalize(direction + (tangent + bitangent) * (cone * 0.7071)), lod);
    sum += studioPmremSample(normalize(direction + (-tangent + bitangent) * (cone * 0.7071)), lod);
    return sum * 0.125;
}

float hash11(float value)
{
    return frac(sin(value * 127.1) * 43758.5453123);
}

float hash31(float3 p)
{
    return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float valueNoise3(float3 p)
{
    float3 cell = floor(p);
    float3 local = frac(p);
    local = local * local * (3.0 - 2.0 * local);

    float c000 = hash31(cell + float3(0.0, 0.0, 0.0));
    float c100 = hash31(cell + float3(1.0, 0.0, 0.0));
    float c010 = hash31(cell + float3(0.0, 1.0, 0.0));
    float c110 = hash31(cell + float3(1.0, 1.0, 0.0));
    float c001 = hash31(cell + float3(0.0, 0.0, 1.0));
    float c101 = hash31(cell + float3(1.0, 0.0, 1.0));
    float c011 = hash31(cell + float3(0.0, 1.0, 1.0));
    float c111 = hash31(cell + float3(1.0, 1.0, 1.0));

    float x00 = lerp(c000, c100, local.x);
    float x10 = lerp(c010, c110, local.x);
    float x01 = lerp(c001, c101, local.x);
    float x11 = lerp(c011, c111, local.x);
    float y0 = lerp(x00, x10, local.y);
    float y1 = lerp(x01, x11, local.y);
    return lerp(y0, y1, local.z);
}

float fractalNoise3(float3 p)
{
    float sum = 0.0;
    float amplitude = 0.5;
    [unroll]
    for (int octave = 0; octave < 5; octave++)
    {
        sum += valueNoise3(p) * amplitude;
        p = p * 2.03 + float3(13.7, 5.1, 9.3);
        amplitude *= 0.52;
    }

    return sum;
}

float3 ifsFoldSky(float3 p)
{
    [unroll]
    for (int index = 0; index < 5; index++)
    {
        p = abs(p) / max(dot(p, p), 0.18) - float3(0.72, 0.58, 0.49);
        p = p.yzx * float3(0.91, 1.07, 0.98);
    }

    return p;
}

float3 nebulaRadiance(float3 direction)
{
    float3 p = direction * 1.15 + float3(0.17, -0.41, 0.29);
    float3 folded = ifsFoldSky(p);
    float galacticPlane = direction.z * 0.82 + direction.x * 0.18 - direction.y * 0.10;
    float band = pow(saturate(1.0 - abs(galacticPlane) * 2.15), 2.4);
    float filament = exp(-5.0 * abs(folded.x + folded.z * 0.16)) * 0.035;
    float cloud = pow(saturate(fractalNoise3(direction * 3.2 + folded * 0.07) - 0.28), 2.6);
    float veil = band * (0.10 + cloud * 0.14 + filament);

    float3 cold = float3(0.010, 0.035, 0.095);
    float3 violet = float3(0.10, 0.035, 0.18);
    float3 ember = float3(0.28, 0.095, 0.045);
    float thermal = saturate(fractalNoise3(direction.zxy * 5.7 + 8.0));
    float3 color = lerp(cold, violet, thermal);
    color = lerp(color, ember, saturate(band * 0.45 + folded.z * 0.06));
    return color * veil;
}

float starClusterLayer(float3 direction, float scale, float threshold, float sharpness, float seed)
{
    float3 cell = floor(direction * scale + seed);
    float3 local = frac(direction * scale + seed) - 0.5;
    float n = hash31(cell);
    float3 offset = float3(hash31(cell + 13.0), hash31(cell + 37.0), hash31(cell + 71.0)) - 0.5;
    float d = length(local - offset * 0.72);
    float star = smoothstep(threshold, 1.0, n) * exp(-d * d * sharpness);

    float parent = hash31(floor(direction * (scale * 0.09) + seed * 0.31));
    return star * smoothstep(0.72, 0.99, parent);
}

float angularCluster(float3 direction, float3 center, float radius, float seed)
{
    float falloff = smoothstep(radius, 0.0, distance(direction, normalize(center)));
    float granular = fractalNoise3(direction * 84.0 + seed);
    return falloff * falloff * (0.28 + granular * 0.72);
}

float3 fractalStarClusters(float3 direction)
{
    float stars = 0.0;
    float clusterMask = 0.18;
    clusterMask += angularCluster(direction, float3(-0.82, 0.21, 0.39), 0.34, 11.0) * 1.15;
    clusterMask += angularCluster(direction, float3(0.36, -0.58, 0.73), 0.22, 29.0) * 0.95;
    clusterMask += angularCluster(direction, float3(0.74, 0.48, -0.19), 0.27, 47.0) * 0.80;
    stars += starClusterLayer(direction, 180.0, 0.985, 620.0, 3.0) * 0.35;
    stars += starClusterLayer(direction, 520.0, 0.994, 1200.0, 17.0) * 0.85;
    stars += starClusterLayer(direction, 1500.0, 0.998, 2100.0, 43.0) * 1.45;
    stars *= clusterMask;

    float3 clusterP = ifsFoldSky(direction * 0.95 + float3(0.31, 0.07, -0.22));
    float clusterCore = exp(-260.0 * dot(clusterP.xy, clusterP.xy)) * smoothstep(0.16, 0.52, clusterP.z + 0.25);
    float3 warm = float3(1.0, 0.82, 0.55);
    float3 blue = float3(0.55, 0.70, 1.0);
    return lerp(blue, warm, hash11(direction.x + direction.y * 3.1)) * stars + warm * clusterCore * 0.75;
}

float3 surfaceMirrorRadiance(float3 p, float3 direction, out float3 normal)
{
    float2 gradient = terrainGradient(p.xy);
    normal = normalize(float3(-gradient.x, -gradient.y, 1.0));
    float3 reflectionDirection = reflect(direction, normal);
    float flatness = 1.0 - smoothstep(SURFACE_FLAT_SLOPE_START, SURFACE_FLAT_SLOPE_END, length(gradient));
    float lod = flatness * SURFACE_FLAT_REFLECTION_MAX_LOD;
    return studioPmremConeSample(reflectionDirection, lod, flatness * 0.055);
}

float3 backgroundRadiance(float3 direction)
{
    uint flags = (uint)round(sceneFlags);
    bool useStudioBackground = (flags & 4u) != 0u;
    if (!useStudioBackground)
    {
        return 0.0;
    }

    float3 studio = studioPmremConeSample(direction, BACKGROUND_PMREM_LOD, BACKGROUND_PMREM_CONE) * 0.22;
    bool useStarfieldBackground = (flags & 2u) != 0u;
    if (!useStarfieldBackground)
    {
        return studio;
    }

    float3 space = float3(0.0006, 0.0014, 0.0045);
    float3 nebula = nebulaRadiance(direction);
    float3 clusters = fractalStarClusters(direction);
    float darkDust = pow(saturate(fractalNoise3(direction * 7.0 + 2.4)), 3.8);
    nebula *= lerp(1.0, 0.46, darkDust);
    return space + nebula + clusters + studio * 0.28;
}

RayMarchResult traverseRay(float3 origin, float3 direction)
{
    RayMarchResult result;
    result.color = backgroundRadiance(direction);
    result.travel = farDistance + 1.0;
    result.fieldId = 0.0;
    result.normal = 0.0;
    result.coverage = 0.0;
    result.stepCount = 0.0;

    float3 surfacePosition;
    float surfaceTravel;
    uint flags = (uint)round(sceneFlags);
    bool traceHeightField = (flags & 1u) != 0u;
    bool surfaceHit = traceHeightField && traceHeightFieldSurfaceDirect(origin, direction, 0.0, farDistance, surfacePosition, surfaceTravel);
    if (surfaceHit)
    {
        float3 surfaceNormal;
        result.color = surfaceMirrorRadiance(surfacePosition, direction, surfaceNormal);
        result.travel = surfaceTravel;
        result.fieldId = FIELD_ID_HEIGHT_FIELD;
        result.normal = surfaceNormal;
        result.coverage = 1.0;
    }

    return result;
}

SceneOut D3D12ScenePS(VertexOut input)
{
    float2 screenUv = float2(input.uv.x, 1.0 - input.uv.y);
    float2 pixel = screenUv * resolution;
    float3 rayDirection = rayDirectionForPixel(pixel, jitterPixels, cameraPosition, cameraTarget);

    RayMarchResult result = traverseRay(cameraPosition, rayDirection);

    SceneOut output;
    output.colorTravel = float4(result.color, min(result.travel, farDistance + 1.0));
    output.metadata = float4(result.fieldId, result.normal);
    output.control = float4(result.coverage, result.stepCount / 72.0, 0.0, 0.0);
    output.reservoirGuide = float4(1.0, 0.0, 1.0, 0.0);
    output.overdraw = 1.0;
    output.depth = saturate(result.travel / max(farDistance, 0.001));
    return output;
}

struct SplineVertexIn
{
    float3 position : POSITION;
    float3 segmentStart : TEXCOORD0;
    float3 segmentEnd : TEXCOORD1;
    float3 previousPoint : TEXCOORD2;
    float3 nextPoint : TEXCOORD3;
    float4 shapeData : TEXCOORD4;
    float4 radiusData : TEXCOORD5;
    float4 color : COLOR;
    float4 material : TEXCOORD6;
};

struct SplineVertexOut
{
    float4 position : SV_Position;
    nointerpolation float2 segmentStartPx : TEXCOORD0;
    nointerpolation float2 segmentEndPx : TEXCOORD1;
    nointerpolation float2 previousPx : TEXCOORD2;
    nointerpolation float2 nextPx : TEXCOORD3;
    nointerpolation float2 segmentRadiusPx : TEXCOORD4;
    nointerpolation float2 joinRadiusPx : TEXCOORD5;
    nointerpolation float4 segmentValidity : TEXCOORD6;
    float2 segmentUv : TEXCOORD7;
    float4 color : COLOR;
    float4 material : TEXCOORD8;
    nointerpolation float feather : TEXCOORD9;
    nointerpolation float2 segmentTravel : TEXCOORD10;
};

float2 ndcToPixel(float2 ndc)
{
    return float2((ndc.x * 0.5 + 0.5) * resolution.x, (1.0 - (ndc.y * 0.5 + 0.5)) * resolution.y);
}

float2 pixelToNdc(float2 pixel)
{
    return float2(pixel.x / max(resolution.x, 1.0) * 2.0 - 1.0, 1.0 - pixel.y / max(resolution.y, 1.0) * 2.0);
}

float splineRadiusToPixels(float radiusWorld, float viewDepth)
{
    float frustumHeight = max(cameraFrustumXy.w - cameraFrustumXy.z, 0.0001);
    return max(radiusWorld * resolution.y / max(frustumHeight * max(viewDepth, 0.0001), 0.0001), 0.5);
}

float3 worldToCameraSpace(float3 worldPoint, float3 right, float3 up, float3 forward)
{
    return float3(
        dot(worldPoint - cameraPosition, right),
        dot(worldPoint - cameraPosition, up),
        dot(worldPoint - cameraPosition, forward));
}

float2 safeDirection(float2 value, float2 fallback)
{
    float lengthValue = length(value);
    return lengthValue > 0.001 ? value / lengthValue : fallback;
}

void capsuleDistancePx(
    float2 samplePx,
    float2 startPx,
    float2 endPx,
    float startRadiusPx,
    float endRadiusPx,
    out float sdf,
    out float closestT,
    out float radiusPx,
    out float2 normalPx)
{
    float2 segment = endPx - startPx;
    float segmentLength2 = max(dot(segment, segment), 0.0001);
    closestT = saturate(dot(samplePx - startPx, segment) / segmentLength2);
    float2 closest = startPx + segment * closestT;
    radiusPx = lerp(startRadiusPx, endRadiusPx, closestT);
    float2 normalPxRaw = samplePx - closest;
    float normalPxLength = length(normalPxRaw);
    float2 tangent = safeDirection(segment, float2(1.0, 0.0));
    normalPx = normalPxLength > 0.0001 ? normalPxRaw / normalPxLength : float2(-tangent.y, tangent.x);
    sdf = normalPxLength - radiusPx;
}

float blueNoiseAt(float2 pixel, uint salt)
{
    uint width;
    uint height;
    blueNoiseTexture.GetDimensions(width, height);
    uint2 dimensions = max(uint2(width, height), uint2(1, 1));
    uint frame = (uint)frameIndex;
    uint2 offset = uint2(frame * 17u + salt * 43u, frame * 29u + salt * 71u);
    uint2 coord = (uint2(max(pixel, float2(0.0, 0.0))) + offset) % dimensions;
    return blueNoiseTexture.Load(int3(coord, 0));
}

SplineVertexOut D3D12SplineVS(SplineVertexIn input)
{
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);

    bool isEndVertex = input.shapeData.y > 0.5;
    float3 segmentStart = input.segmentStart;
    float3 segmentEnd = input.segmentEnd;
    float3 endpoint = input.position;
    float endpointT = isEndVertex ? 1.0 : 0.0;

    float3 startView = worldToCameraSpace(segmentStart, right, up, forward);
    float3 endView = worldToCameraSpace(segmentEnd, right, up, forward);
    float3 previousView = worldToCameraSpace(input.previousPoint, right, up, forward);
    float3 nextView = worldToCameraSpace(input.nextPoint, right, up, forward);
    float3 endpointView = worldToCameraSpace(endpoint, right, up, forward);
    float4 startClip = projectCameraSpace(startView);
    float4 endClip = projectCameraSpace(endView);
    float4 previousClip = projectCameraSpace(previousView);
    float4 nextClip = projectCameraSpace(nextView);
    float4 endpointClip = projectCameraSpace(endpointView);

    float2 startPx = ndcToPixel(startClip.xy);
    float2 endPx = ndcToPixel(endClip.xy);
    float2 previousPx = ndcToPixel(previousClip.xy);
    float2 nextPx = ndcToPixel(nextClip.xy);
    float2 endpointPx = ndcToPixel(endpointClip.xy);
    float2 segmentPx = endPx - startPx;
    float2 tangent = safeDirection(segmentPx, float2(1.0, 0.0));
    float2 normal = float2(-tangent.y, tangent.x);

    float startRadiusPx = splineRadiusToPixels(input.radiusData.x, startView.z);
    float endRadiusPx = splineRadiusToPixels(input.radiusData.y, endView.z);
    float previousRadiusPx = splineRadiusToPixels(input.radiusData.x, previousView.z);
    float nextRadiusPx = splineRadiusToPixels(input.radiusData.y, nextView.z);
    float endpointRadiusPx = lerp(startRadiusPx, endRadiusPx, endpointT);
    float envelopePaddingPx = max(2.0, endpointRadiusPx * max(input.radiusData.z, 0.0) + 1.5);
    float envelopeRadiusPx = endpointRadiusPx + envelopePaddingPx;
    float capSign = input.shapeData.z;

    float2 previousSegmentPx = startPx - previousPx;
    float2 nextSegmentPx = nextPx - endPx;
    bool hasPrevious = dot(previousSegmentPx, previousSegmentPx) > 0.0001;
    bool hasNext = dot(nextSegmentPx, nextSegmentPx) > 0.0001;
    float2 joinTangent = tangent;
    if (!isEndVertex && hasPrevious)
    {
        joinTangent = safeDirection(tangent + safeDirection(previousSegmentPx, tangent), tangent);
    }
    else if (isEndVertex && hasNext)
    {
        joinTangent = safeDirection(tangent + safeDirection(nextSegmentPx, tangent), tangent);
    }

    float2 joinNormal = float2(-joinTangent.y, joinTangent.x);
    float2 expandedPx = endpointPx + joinNormal * input.shapeData.x * envelopeRadiusPx + tangent * capSign * envelopeRadiusPx;

    SplineVertexOut output;
    output.position = float4(pixelToNdc(expandedPx), endpointClip.z, 1.0);
    output.segmentStartPx = startPx;
    output.segmentEndPx = endPx;
    output.previousPx = previousPx;
    output.nextPx = nextPx;
    output.segmentRadiusPx = float2(startRadiusPx, endRadiusPx);
    output.joinRadiusPx = float2(previousRadiusPx, nextRadiusPx);
    output.segmentValidity = float4(hasPrevious ? 1.0 : 0.0, hasNext ? 1.0 : 0.0, endpointT, input.shapeData.x);
    output.segmentUv = float2(endpointT, input.shapeData.x);
    output.color = input.color;
    output.material = input.material;
    output.feather = input.radiusData.z;
    output.segmentTravel = float2(startView.z, endView.z);
    return output;
}

SceneOut D3D12SplinePS(SplineVertexOut input)
{
    float2 jitter = 0.0;
    float2 samplePx = input.position.xy;

    float sdf;
    float closestT;
    float radiusPx;
    float2 normalPx;
    capsuleDistancePx(samplePx, input.segmentStartPx, input.segmentEndPx, input.segmentRadiusPx.x, input.segmentRadiusPx.y, sdf, closestT, radiusPx, normalPx);

    float previousSdf;
    float previousT;
    float previousRadius;
    float2 previousNormal;
    capsuleDistancePx(samplePx, input.previousPx, input.segmentStartPx, input.joinRadiusPx.x, input.segmentRadiusPx.x, previousSdf, previousT, previousRadius, previousNormal);
    if (input.segmentValidity.x > 0.5 && previousSdf < sdf)
    {
        sdf = previousSdf;
        closestT = 0.0;
        radiusPx = previousRadius;
        normalPx = previousNormal;
    }

    float nextSdf;
    float nextT;
    float nextRadius;
    float2 nextNormal;
    capsuleDistancePx(samplePx, input.segmentEndPx, input.nextPx, input.segmentRadiusPx.y, input.joinRadiusPx.y, nextSdf, nextT, nextRadius, nextNormal);
    if (input.segmentValidity.y > 0.5 && nextSdf < sdf)
    {
        sdf = nextSdf;
        closestT = 1.0;
        radiusPx = nextRadius;
        normalPx = nextNormal;
    }

    float aa = max(fwidth(sdf), max(0.75, radiusPx * max(input.feather, 0.0)));
    float coverage = 1.0 - smoothstep(0.0, aa, sdf);
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);
    float3 ray = rayDirectionForPixel(samplePx, jitterPixels, cameraPosition, cameraTarget);
    float rimBlend = saturate((sdf + radiusPx) / max(radiusPx, 0.0001));
    float frontBlend = sqrt(saturate(1.0 - rimBlend * rimBlend));
    float3 tubeNormal = normalize(((right * normalPx.x) - (up * normalPx.y)) * rimBlend - ray * frontBlend);
    float normalFacing = saturate(-dot(ray, tubeNormal));
    float glowFacing = pow(normalFacing, max(input.material.z, 0.0001));
    float alphaFacing = pow(normalFacing, max(input.material.w, 0.0001));
    float claimCoverage = saturate(input.color.a * coverage * alphaFacing);
    if (claimCoverage <= 0.004)
    {
        discard;
    }

    float3 color = input.color.rgb * input.material.x * glowFacing * claimCoverage;
    float travel = lerp(input.segmentTravel.x, input.segmentTravel.y, closestT);
    if (renderDebugMode >= 12.5 && renderDebugMode < 13.5)
    {
        color = lerp(float3(0.0, 0.25, 0.45), float3(0.0, 0.85, 1.0), saturate(abs(input.segmentValidity.w)));
        claimCoverage = max(claimCoverage, 0.24);
    }
    else if (renderDebugMode >= 13.5 && renderDebugMode < 14.5)
    {
        color = lerp(float3(0.15, 0.45, 1.0), float3(1.0, 0.15, 0.0), saturate(sdf / max(radiusPx, 1.0) * 0.5 + 0.5));
        claimCoverage = 1.0;
    }
    else if (renderDebugMode >= 14.5 && renderDebugMode < 15.5)
    {
        color = float3(coverage, closestT, 1.0 - coverage);
        claimCoverage = 1.0;
    }

    SceneOut output;
    output.colorTravel = float4(color, min(travel, farDistance + 1.0));
    output.metadata = float4(5000.0, closestT, sdf, radiusPx);
    output.control = float4(claimCoverage, coverage, saturate(radiusPx / 32.0), 0.0);
    output.reservoirGuide = float4(claimCoverage, 0.0, coverage, 0.0);
    output.overdraw = 1.0;
    output.depth = saturate(travel / max(farDistance, 0.0001));
    return output;
}
