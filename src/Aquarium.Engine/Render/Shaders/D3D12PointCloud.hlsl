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

Texture2D<float> disparityTexture : register(t0);
RWStructuredBuffer<PointCloudVertex> pointVertices : register(u0);
RWStructuredBuffer<uint> pointIndices : register(u1);

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

void cameraBasis(float3 camera, float3 target, out float3 forward, out float3 right, out float3 up)
{
    forward = normalize(target - camera);
    float3 worldUp = abs(forward.y) > 0.96 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    right = normalize(cross(worldUp, forward));
    up = normalize(cross(forward, right));
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
    float minDepth = max(0.001, depthRange.x);
    float maxDepth = max(minDepth + 0.001, depthRange.y);
    float confidence = disparity > 0.0001 ? 1.0 : 0.0;
    float fallbackDepth = maxDepth;
    float depth = confidence > 0.0
        ? saturate(projection.x / max(disparity, 0.0001)) * (maxDepth - minDepth) + minDepth
        : fallbackDepth;

    float focalPixels = max(1.0, projection.y);
    float principalX = projection.z;
    float principalY = projection.w;
    float3 position = float3(
        ((float)x - principalX) / focalPixels * depth,
        -((float)y - principalY) / focalPixels * depth,
        depth);
    float normalizedDepth = saturate((depth - minDepth) / max(maxDepth - minDepth, 0.0001));
    PointCloudVertex vertex;
    vertex.position = position;
    vertex.normal = float3(0.0, 0.0, -1.0);
    vertex.uv = float2((float)x / max(1.0, (float)(width - 1)), (float)y / max(1.0, (float)(height - 1)));
    vertex.color = confidence > 0.0
        ? float4(lerp(float3(0.2, 0.75, 1.0), float3(1.0, 0.95, 0.35), normalizedDepth), 1.0)
        : float4(0.0, 0.0, 0.0, 0.0);
    pointVertices[pointIndex] = vertex;
    pointIndices[pointIndex] = pointIndex;
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
