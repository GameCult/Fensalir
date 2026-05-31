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

cbuffer D3D12PointCloudConstants : register(b1)
{
    float4 sourceShape;
    float4 projection;
    float4 depthRange;
};

static const float FIELD_ID_POINT_CLOUD = 71.0;

struct PointCloudVertex
{
    float3 position;
    float3 normal;
    float2 uv;
    float4 color;
};

struct FractalSdfSplat
{
    float4 centerRadius;
    float4 orientation;
    float4 radiiFalloff;
    float4 materialConfidence;
    float4 key;
};

struct SdfEnvelopeReservoir
{
    float4 centerRadius;
    float4 radiiFalloff;
    float4 weightTargetCount;
    float4 validation;
};

struct PbrMaterialReservoir
{
    float4 baseColorRoughMetal;
    float4 normalVariance;
    float4 weightTargetCount;
    float4 validation;
};

struct RadiosityReservoir
{
    float4 radianceDistance;
    float4 directionOcclusion;
    float4 weightTargetCount;
    float4 validation;
};

Texture2D<float> disparityTexture : register(t0);
RWStructuredBuffer<PointCloudVertex> pointVertices : register(u0);
RWStructuredBuffer<uint> pointIndices : register(u1);
RWStructuredBuffer<FractalSdfSplat> surfaceSplats : register(u2);
RWStructuredBuffer<SdfEnvelopeReservoir> surfaceSdfReservoirs : register(u3);
RWStructuredBuffer<PbrMaterialReservoir> surfacePbrReservoirs : register(u4);
RWStructuredBuffer<RadiosityReservoir> surfaceRadiosityReservoirs : register(u5);

struct VertexIn
{
    float3 position : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float4 color : COLOR0;
};

struct VertexOut
{
    float4 position : SV_Position;
    float3 worldPosition : TEXCOORD0;
    float3 normal : TEXCOORD1;
    float2 uv : TEXCOORD2;
    float4 color : COLOR0;
};

struct SceneOut
{
    float4 colorTravel : SV_Target0;
    float4 metadata : SV_Target1;
    float4 control : SV_Target2;
    float4 reservoirGuide : SV_Target3;
    float depth : SV_Depth;
};

float DepthFromDisparity(float disparity)
{
    float minDepth = max(0.001, depthRange.x);
    float maxDepth = max(minDepth + 0.001, depthRange.y);
    return disparity > 0.0001
        ? saturate(projection.x / max(disparity, 0.0001)) * (maxDepth - minDepth) + minDepth
        : maxDepth;
}

void cameraBasis(float3 camera, float3 target, out float3 forward, out float3 right, out float3 up)
{
    forward = normalize(target - camera);
    float3 worldUp = abs(forward.y) > 0.96 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    right = normalize(cross(worldUp, forward));
    up = normalize(cross(forward, right));
}

float3 PositionFromPixel(uint2 pixel, float depth)
{
    float focalPixels = max(1.0, projection.y);
    float principalX = projection.z;
    float principalY = projection.w;
    float minDepth = max(0.001, depthRange.x);
    float maxDepth = max(minDepth + 0.001, depthRange.y);
    float lateralDisplayScale = max(1.0, depthRange.z);
    float forwardDisplayAnchor = depthRange.w > 0.0 ? depthRange.w : (minDepth + maxDepth) * 0.5;
    float3 cameraLocal = float3(
        ((float)pixel.x - principalX) / focalPixels * depth,
        -((float)pixel.y - principalY) / focalPixels * depth,
        depth);
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);
    return cameraTarget +
        right * cameraLocal.x * lateralDisplayScale +
        up * cameraLocal.y * lateralDisplayScale +
        forward * (cameraLocal.z - forwardDisplayAnchor);
}

float DisparityAt(int2 pixel, uint width, uint height)
{
    pixel.x = clamp(pixel.x, 0, (int)width - 1);
    pixel.y = clamp(pixel.y, 0, (int)height - 1);
    return disparityTexture.Load(int3(pixel, 0));
}

float Hash01(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    value ^= value >> 16;
    return (float)(value & 0x00ffffffu) / 16777216.0;
}

float4 projectWorld(float3 worldPosition)
{
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);
    float3 relative = worldPosition - cameraPosition;
    float3 view = float3(dot(relative, right), dot(relative, up), dot(relative, forward));
    float z = max(view.z, 0.0001);
    float2 slope = view.xy / z;
    float2 frustumMin = float2(cameraFrustumXy.x, cameraFrustumXy.z);
    float2 frustumMax = float2(cameraFrustumXy.y, cameraFrustumXy.w);
    float2 uv = (slope - frustumMin) / max(frustumMax - frustumMin, float2(0.0001, 0.0001));
    return float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), saturate(z / max(farDistance, 0.0001)), 1.0);
}

[numthreads(128, 1, 1)]
void D3D12PointCloudFromDisparityCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint pointIndex = dispatchThreadId.x;
    uint pointCount = (uint)max(1.0, sourceShape.z);
    if (pointIndex >= pointCount)
    {
        return;
    }

    uint width = (uint)max(1.0, sourceShape.x);
    uint height = (uint)max(1.0, sourceShape.y);
    uint stride = (uint)max(1.0, sourceShape.w);
    uint sampledWidth = max(1, (width + stride - 1) / stride);
    uint x = (pointIndex % sampledWidth) * stride;
    uint y = (pointIndex / sampledWidth) * stride;
    x = min(x, width - 1);
    y = min(y, height - 1);

    float disparity = disparityTexture.Load(int3(x, y, 0));
    float confidence = disparity > 0.0001 ? 1.0 : 0.0;
    float depth = DepthFromDisparity(disparity);

    float3 position = PositionFromPixel(uint2(x, y), depth);
    float minDepth = max(0.001, depthRange.x);
    float maxDepth = max(minDepth + 0.001, depthRange.y);
    float normalizedDepth = saturate((depth - minDepth) / max(maxDepth - minDepth, 0.0001));
    float leftDepth = DepthFromDisparity(DisparityAt(int2((int)x - (int)stride, (int)y), width, height));
    float rightDepth = DepthFromDisparity(DisparityAt(int2((int)x + (int)stride, (int)y), width, height));
    float upDepth = DepthFromDisparity(DisparityAt(int2((int)x, (int)y - (int)stride), width, height));
    float downDepth = DepthFromDisparity(DisparityAt(int2((int)x, (int)y + (int)stride), width, height));
    float3 leftPosition = PositionFromPixel(uint2(max((int)x - (int)stride, 0), y), leftDepth);
    float3 rightPosition = PositionFromPixel(uint2(min(x + stride, width - 1), y), rightDepth);
    float3 upPosition = PositionFromPixel(uint2(x, max((int)y - (int)stride, 0)), upDepth);
    float3 downPosition = PositionFromPixel(uint2(x, min(y + stride, height - 1)), downDepth);
    float3 normal = normalize(cross(rightPosition - leftPosition, downPosition - upPosition));
    normal = dot(normal, normal) > 0.25 ? normal : float3(0.0, 0.0, -1.0);
    float curvature = saturate((abs(leftDepth + rightDepth - 2.0 * depth) + abs(upDepth + downDepth - 2.0 * depth)) * 4.0);

    PointCloudVertex vertex;
    vertex.position = position;
    vertex.normal = normal;
    vertex.uv = float2((float)x / max(1.0, (float)(width - 1)), (float)y / max(1.0, (float)(height - 1)));
    vertex.color = confidence > 0.0
        ? float4(lerp(float3(0.2, 0.75, 1.0), float3(1.0, 0.95, 0.35), normalizedDepth), 1.0)
        : float4(0.08, 0.14, 0.22, 0.18);
    pointVertices[pointIndex] = vertex;
    pointIndices[pointIndex] = pointIndex;

    float reservoirConfidence = confidence;
    float sampleRadius = lerp(0.080, 0.035, saturate(confidence)) * (1.0 + curvature * 2.5);
    FractalSdfSplat splat;
    splat.centerRadius = float4(position, sampleRadius);
    splat.orientation = float4(normal, 1.0);
    splat.radiiFalloff = float4(sampleRadius, sampleRadius * (1.0 + curvature), sampleRadius * 0.35, 3.0 + curvature * 4.0);
    splat.materialConfidence = float4(saturate(1.0 - normalizedDepth), 1.0, 1.0, reservoirConfidence);
    splat.key = float4((float)pointIndex, frameIndex, curvature, Hash01(pointIndex + (uint)frameIndex * 1664525u));
    surfaceSplats[pointIndex] = splat;

    SdfEnvelopeReservoir sdf;
    sdf.centerRadius = splat.centerRadius;
    sdf.radiiFalloff = splat.radiiFalloff;
    sdf.weightTargetCount = float4(reservoirConfidence, reservoirConfidence, 1.0, reservoirConfidence);
    sdf.validation = float4(reservoirConfidence, frameIndex, curvature, 1.0);
    surfaceSdfReservoirs[pointIndex] = sdf;

    PbrMaterialReservoir pbr;
    pbr.baseColorRoughMetal = float4(vertex.color.rgb, 0.38 + curvature * 0.28);
    pbr.normalVariance = float4(normal, curvature);
    pbr.weightTargetCount = sdf.weightTargetCount;
    pbr.validation = sdf.validation;
    surfacePbrReservoirs[pointIndex] = pbr;

    RadiosityReservoir radiosity;
    radiosity.radianceDistance = float4(vertex.color.rgb * 0.18, depth);
    radiosity.directionOcclusion = float4(normalize(-normal + 0.01), 0.35 + confidence * 0.45);
    radiosity.weightTargetCount = sdf.weightTargetCount;
    radiosity.validation = sdf.validation;
    surfaceRadiosityReservoirs[pointIndex] = radiosity;
}

VertexOut D3D12PointCloudVS(VertexIn input)
{
    VertexOut output;
    output.position = projectWorld(input.position);
    output.worldPosition = input.position;
    output.normal = input.normal;
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

SceneOut D3D12PointCloudPS(VertexOut input)
{
    SceneOut output;
    float travel = length(input.worldPosition - cameraPosition);
    float alpha = saturate(input.color.a);
    output.colorTravel = float4(input.color.rgb * alpha, travel);
    output.metadata = float4(FIELD_ID_POINT_CLOUD, normalize(input.normal));
    output.control = float4(alpha, 1.0, 0.35, 0.0);
    output.reservoirGuide = float4(alpha, 0.0, alpha, 0.0);
    output.depth = input.position.z / input.position.w;
    return output;
}
