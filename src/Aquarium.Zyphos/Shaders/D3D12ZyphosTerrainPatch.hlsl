#include "D3D12ZyphosPlanet.hlsl"

static const uint ZY_PATCH_CELLS = 16u;
static const uint ZY_PATCH_VERTICES = ZY_PATCH_CELLS * ZY_PATCH_CELLS * 6u;

struct ZyPatchVertexOut
{
    float4 position : SV_Position;
    float3 worldPosition : TEXCOORD0;
};

struct ZyPatchSceneOut
{
    float4 colorTravel:SV_Target0; float4 metadata:SV_Target1; float4 control:SV_Target2;
    float4 reservoirGuide:SV_Target3; float overdraw:SV_Target4;
};

float3 zyPatchDirection(uint face,float2 local)
{
    float2 faceCoordinate=local*2.0-1.0;
    float u=tan(faceCoordinate.x*PI*0.25),v=tan(faceCoordinate.y*PI*0.25);
    float3 cube=face==0u?float3(1,v,-u):face==1u?float3(-1,v,u):face==2u?float3(u,1,-v):face==3u?float3(u,-1,v):face==4u?float3(u,v,1):float3(-u,v,-1);
    return normalize(cube);
}

ZyPatchVertexOut D3D12ZyphosTerrainPatchVS(uint vertexId:SV_VertexID,uint instanceId:SV_InstanceID)
{
    uint cell=vertexId/6u,corner=vertexId%6u; uint x=cell%ZY_PATCH_CELLS,y=cell/ZY_PATCH_CELLS;
    uint2 offsets[6]={uint2(0,0),uint2(1,0),uint2(1,1),uint2(0,0),uint2(1,1),uint2(0,1)};
    float2 local=(float2(x,y)+offsets[corner])/(float)ZY_PATCH_CELLS;
    ZyPlanetPageMetadata root=zy_planet_page_metadata[instanceId]; float3 dir=zyPatchDirection((uint)root.address.x,local);
    SdfObject planet=sdfObjects[0]; float height=zyTerrainOffset(dir,planet); if(!isfinite(height))height=0.0;
    float3 worldPosition=planet.centerRadius.xyz+zyRotateZ(dir,planet.state.y)*(planet.state.x+height);
    float3 forward,right,up; cameraBasis(cameraPosition,cameraTarget,forward,right,up);
    float3 delta=worldPosition-cameraPosition; float z=max(dot(delta,forward),0.0001);
    float2 frustumMin=float2(cameraFrustumXy.x,cameraFrustumXy.z),frustumMax=float2(cameraFrustumXy.y,cameraFrustumXy.w);
    float2 slope=float2(dot(delta,right),dot(delta,up))/z; float2 uv=(slope-frustumMin)/max(frustumMax-frustumMin,0.0001);
    float2 clip=uv*float2(2.0,-2.0)+float2(-1.0,1.0);
    ZyPatchVertexOut output; output.position=float4(clip,saturate(z/max(farDistance,0.0001)),1); output.worldPosition=worldPosition; return output;
}

ZyPatchSceneOut D3D12ZyphosTerrainPatchPS(ZyPatchVertexOut input)
{
    SdfObject planet=sdfObjects[0]; float3 coarsePosition=input.worldPosition; float3 ray=normalize(coarsePosition-cameraPosition); float3 p=coarsePosition;
    [unroll] for(int step=0;step<4;step++)
    {
        float3 local=p-planet.centerRadius.xyz; float radius=length(local); float3 radial=local/max(radius,0.0001); float3 dir=zyPlanetDir(local,planet);
        float error=radius-(planet.state.x+zyTerrainOffset(dir,planet)); float derivative=dot(ray,radial);
        float correction=clamp(-error/(abs(derivative)>0.2?derivative:(derivative<0?-0.2:0.2)),-0.08,0.08); p+=ray*correction;
    }
    if(!all(isfinite(p)))p=coarsePosition;
    float travel=length(p-cameraPosition); if(!isfinite(travel)||travel<=0.0||travel>farDistance)discard;
    float3 radial=normalize(p-planet.centerRadius.xyz); float3 normal=zyPlanetSurfaceNormal(p,0); if(!all(isfinite(normal))||dot(normal,normal)<0.25)normal=radial;
    SdfSurface surface=sdfSurface(p,0);
    ZyPatchSceneOut output; output.colorTravel=float4(shadeSdf(0.0,travel,p,normal,0,surface),min(travel,farDistance+1.0));
    output.metadata=float4(FIELD_ID_SDF_OBJECT_BASE,normal); output.control=float4(1,4.0/384.0,saturate(surface.temporalDetail),saturate(surface.reservoirConfidence));
    output.reservoirGuide=float4(saturate(surface.reservoirConfidence),0,1,0); output.overdraw=1; return output;
}
