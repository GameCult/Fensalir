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
    float4 gpuFusionInfo;
    float4 fractalReservoirInfo;
    float4 fractalReservoirFrame;
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

StructuredBuffer<FractalSdfSplat> fractalSdfSplats : register(t38);
StructuredBuffer<SdfEnvelopeReservoir> sdfEnvelopeReservoirs : register(t39);
StructuredBuffer<PbrMaterialReservoir> pbrMaterialReservoirs : register(t40);
StructuredBuffer<RadiosityReservoir> radiosityReservoirs : register(t41);

struct FractalSplatVertexOut
{
    float4 position : SV_Position;
    nointerpolation uint splatIndex : TEXCOORD0;
    float2 quad : TEXCOORD1;
    float travel : TEXCOORD2;
    nointerpolation float3 centerWorld : TEXCOORD3;
    nointerpolation float worldRadius : TEXCOORD4;
    nointerpolation float3 basisRight : TEXCOORD5;
    nointerpolation float3 basisUp : TEXCOORD6;
    nointerpolation float3 basisForward : TEXCOORD7;
};

struct SceneOut
{
    float4 colorTravel : SV_Target0;
    float4 metadata : SV_Target1;
    float4 control : SV_Target2;
    float4 reservoirGuide : SV_Target3;
    float depth : SV_Depth;
};

static const float FIELD_ID_FRACTAL_SPLAT_BASE = 4000.0;
static const float FIELD_ENCODING_DENSITY = 2.0;
static const float FIELD_ENCODING_EXTINCTION = 3.0;

void cameraBasis(float3 camera, float3 target, out float3 forward, out float3 right, out float3 up)
{
    forward = normalize(target - camera);
    float3 worldUp = abs(forward.y) > 0.96 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    right = normalize(cross(worldUp, forward));
    up = normalize(cross(forward, right));
}

float4 projectWorld(float3 world, float3 camera, float3 forward, float3 right, float3 up, out float travel)
{
    float3 delta = world - camera;
    travel = max(dot(delta, forward), 0.0001);
    float2 frustumMin = float2(cameraFrustumXy.x, cameraFrustumXy.z);
    float2 frustumMax = float2(cameraFrustumXy.y, cameraFrustumXy.w);
    float2 slope = float2(dot(delta, right), dot(delta, up)) / travel;
    float2 ndc = ((slope - frustumMin) / max(frustumMax - frustumMin, float2(0.0001, 0.0001))) * 2.0 - 1.0;
    return float4(ndc, saturate(travel / max(farDistance, 0.0001)), 1.0);
}

uint VisibleSplatIndex(uint instanceId)
{
    uint splatCount = max((uint)fractalReservoirInfo.x, 1u);
    uint visibleCount = max((uint)fractalReservoirInfo.y, 1u);
    return min((instanceId * splatCount) / visibleCount, splatCount - 1u);
}

FractalSplatVertexOut D3D12FractalSplatVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
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

    uint splatIndex = VisibleSplatIndex(instanceId);
    FractalSdfSplat splat = fractalSdfSplats[splatIndex];
    SdfEnvelopeReservoir sdf = sdfEnvelopeReservoirs[splatIndex];
    bool sdfResident = sdf.weightTargetCount.z > 0.5;
    float4 centerRadius = sdfResident ? sdf.centerRadius : splat.centerRadius;
    float3 forward;
    float3 right;
    float3 up;
    cameraBasis(cameraPosition, cameraTarget, forward, right, up);
    float fieldRadius = fractalReservoirFrame.w > 0.0 ? fractalReservoirFrame.w : max(viewRadius * 0.16, 0.05);
    float3 fieldCenter = fractalReservoirFrame.w > 0.0 ? fractalReservoirFrame.xyz : cameraTarget;
    float3 center = fieldCenter + centerRadius.xyz * fieldRadius;
    float3 surfaceNormal = normalize(centerRadius.xyz);
    float3 referenceUp = abs(surfaceNormal.z) < 0.92 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    float3 tangentRight = normalize(cross(referenceUp, surfaceNormal));
    float3 tangentUp = normalize(cross(surfaceNormal, tangentRight));
    float boundRadius = max(centerRadius.w * fieldRadius * 1.25, 0.035 * fieldRadius);
    float3 cornerWorld = center + ((tangentRight * corners[vertexId].x) + (tangentUp * corners[vertexId].y)) * boundRadius;
    float z;
    float4 projectedCorner = projectWorld(cornerWorld, cameraPosition, forward, right, up, z);

    FractalSplatVertexOut output;
    output.position = projectedCorner;
    output.splatIndex = splatIndex;
    output.quad = corners[vertexId];
    output.travel = z;
    output.centerWorld = center;
    output.worldRadius = boundRadius;
    output.basisRight = tangentRight;
    output.basisUp = tangentUp;
    output.basisForward = surfaceNormal;
    return output;
}

bool IsTransparentField(float encoding)
{
    return abs(encoding - FIELD_ENCODING_DENSITY) < 0.5 ||
        abs(encoding - FIELD_ENCODING_EXTINCTION) < 0.5;
}

SceneOut ResolveFractalSplat(FractalSplatVertexOut input, bool renderTransparent)
{
    float r2 = dot(input.quad, input.quad);
    if (r2 >= 1.0)
    {
        discard;
    }

    FractalSdfSplat splat = fractalSdfSplats[input.splatIndex];
    SdfEnvelopeReservoir sdf = sdfEnvelopeReservoirs[input.splatIndex];
    PbrMaterialReservoir pbr = pbrMaterialReservoirs[input.splatIndex];
    RadiosityReservoir radiosity = radiosityReservoirs[input.splatIndex];

    bool sdfResident = sdf.weightTargetCount.z > 0.5;
    bool pbrResident = pbr.weightTargetCount.z > 0.5;
    bool radiosityResident = radiosity.weightTargetCount.z > 0.5;
    bool transparentField = IsTransparentField(splat.materialConfidence.z);
    if (transparentField != renderTransparent)
    {
        discard;
    }

    float surfaceZ = sqrt(saturate(1.0 - r2));
    float3 tangentOffset = (input.basisRight * input.quad.x) + (input.basisUp * input.quad.y);
    float3 normal = normalize(input.basisForward * (0.92 + surfaceZ * 0.08) + tangentOffset * 0.16);
    float3 surfaceWorld = input.centerWorld + tangentOffset * input.worldRadius;
    float edgeCoverage = smoothstep(1.0, 0.82, r2);
    float viewFacing = saturate(dot(normalize(cameraPosition - surfaceWorld), normal));
    float starFacing = saturate(dot(normal, normalize(float3(0.25, 0.006, 0.077) - surfaceWorld)));
    float material = saturate(splat.materialConfidence.x);
    float3 fallbackColor = transparentField
        ? lerp(float3(0.18, 0.08, 0.32), float3(1.0, 0.44, 0.12), material)
        : lerp(float3(0.18, 0.58, 0.28), float3(1.15, 0.78, 0.28), material);
    float3 pbrColor = lerp(float3(0.06, 0.30, 0.14), saturate(pbr.baseColorRoughMetal.rgb), 0.72);
    float3 radiosityColor = saturate(radiosity.radianceDistance.rgb);
    float reservoirConfidence = min(
        sdfResident ? saturate(sdf.validation.x) : 0.0,
        min(
            pbrResident ? saturate(pbr.validation.x) : 0.0,
            radiosityResident ? saturate(radiosity.validation.x) : 0.0));
    float3 color = pbrResident ? pbrColor : fallbackColor;
    float roughness = pbrResident ? saturate(pbr.baseColorRoughMetal.w) : 0.6;
    float diffuse = transparentField ? 0.52 + 0.36 * viewFacing : 0.32 + 0.50 * starFacing + 0.18 * viewFacing;
    float fresnel = pow(saturate(1.0 - viewFacing), 2.2);
    float3 litColor = color * diffuse + color * (transparentField ? 0.18 : 0.72);
    litColor += radiosityResident ? radiosityColor * (0.24 + reservoirConfidence * 0.48) : 0.0;
    litColor += lerp(float3(0.015, 0.06, 0.05), transparentField ? float3(0.95, 0.28, 0.08) : float3(0.24, 0.30, 0.20), 1.0 - roughness) * fresnel * (transparentField ? 0.72 : 0.48);
    float opacity = transparentField
        ? saturate(edgeCoverage * edgeCoverage * (0.48 + reservoirConfidence * 0.42))
        : saturate(edgeCoverage * (0.92 + reservoirConfidence * 0.28));
    float reservoirUpdatedFrame = min(
        sdfResident ? sdf.validation.y : frameIndex,
        min(
            pbrResident ? pbr.validation.y : frameIndex,
            radiosityResident ? radiosity.validation.y : frameIndex));
    float reservoirSampleAge = max(frameIndex - reservoirUpdatedFrame, 0.0);
    float domainValidity = reservoirConfidence > 0.0 ? 1.0 : 0.0;

    SceneOut output;
    output.colorTravel = float4(litColor * opacity * (transparentField ? 1.0 : 4.0), min(input.travel - surfaceZ * input.worldRadius, farDistance + 1.0));
    output.metadata = float4(FIELD_ID_FRACTAL_SPLAT_BASE, normal);
    output.control = float4(opacity, reservoirConfidence, transparentField ? splat.materialConfidence.z / 10.0 : saturate((sdfResident ? sdf.centerRadius.w : splat.centerRadius.w) * 40.0), 0.0);
    output.reservoirGuide = float4(reservoirConfidence, reservoirSampleAge, domainValidity, 0.0);
    output.depth = saturate(input.travel / max(farDistance, 0.0001));
    return output;
}

SceneOut D3D12FractalSurfaceSplatPS(FractalSplatVertexOut input)
{
    return ResolveFractalSplat(input, false);
}

SceneOut D3D12FractalTransparentSplatPS(FractalSplatVertexOut input)
{
    return ResolveFractalSplat(input, true);
}

SceneOut D3D12FractalSplatPS(FractalSplatVertexOut input)
{
    return D3D12FractalSurfaceSplatPS(input);
}
