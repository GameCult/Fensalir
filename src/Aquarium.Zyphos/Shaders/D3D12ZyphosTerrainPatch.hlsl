#include "D3D12ZyphosPlanet.hlsl"

struct ZyPatchVertexOut
{
    float4 position : SV_Position;
    float3 worldPosition : TEXCOORD0;
};

struct ZyPatchSceneOut
{
    float4 colorTravel:SV_Target0; float4 metadata:SV_Target1; float4 control:SV_Target2;
    float4 reservoirGuide:SV_Target3; float overdraw:SV_Target4;
    float depth:SV_Depth;
};

float3 zyPatchDirection(uint face,float2 local)
{
    return gamecult_geometry_planetary_face_direction((int)face,local*2.0-1.0);
}

ZyPatchVertexOut D3D12ZyphosTerrainPatchVS(uint vertexId:SV_VertexID,uint instanceId:SV_InstanceID)
{
    uint patchCells=(uint)max(zy_planet_page_set[0].state.y,1.0);
    uint cell=vertexId/6u,corner=vertexId%6u; uint x=cell%patchCells,y=cell/patchCells;
    uint2 offsets[6]={uint2(0,0),uint2(1,0),uint2(1,1),uint2(0,0),uint2(1,1),uint2(0,1)};
    float2 local=(float2(x,y)+offsets[corner])/(float)patchCells;
    ZyPlanetPageMetadata root=zy_planet_page_metadata[instanceId]; float3 dir=zyPatchDirection((uint)root.address.x,local);
    SdfObject planet=sdfObjects[0]; float height=zyTerrainOffset(dir,planet); if(!isfinite(height))height=0.0;
    float captureMargin=zyTerrainPatchCaptureMargin(dir);
    float3 worldPosition=planet.centerRadius.xyz+zyRotateZ(dir,planet.state.y)*(planet.state.x+height+captureMargin);
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
        float3 local=p-planet.centerRadius.xyz; float3 dir=zyPlanetDir(local,planet);
        float targetRadius=planet.state.x+zyTerrainOffset(dir,planet);
        p=gamecult_geometry_planetary_radial_refinement_step(p,ray,planet.centerRadius.xyz,targetRadius,0.2,0.08);
    }
    if(!all(isfinite(p)))p=coarsePosition;
    float travel=length(p-cameraPosition); if(!isfinite(travel)||travel<=0.0||travel>farDistance)discard;
    float3 radial=normalize(p-planet.centerRadius.xyz);
    float3 normal=cross(ddy(p),ddx(p));
    if(!all(isfinite(normal))||dot(normal,normal)<1.0e-10)normal=radial; else normal=normalize(normal);
    if(dot(normal,cameraPosition-p)<0.0)normal=-normal;
    float3 terrainDir=zyPlanetDir(p-planet.centerRadius.xyz,planet);
    ZyPlanetPageMetadata debugMetadata; ZyPlanetPageSummary debugSummary; float4 debugHeightGradient; float2 debugMasks;
    bool hasDebugPage=zyTerrainDeepestPageEvidence(terrainDir,debugMetadata,debugSummary,debugHeightGradient,debugMasks);
    float3 debugColor=0.0;
    if(renderDebugMode>=21.5&&renderDebugMode<22.5)
        debugColor=hasDebugPage?float3(debugMetadata.address.x/5.0,debugMetadata.address.y/5.0,saturate(debugMetadata.state.y)):float3(1,0,1);
    else if(renderDebugMode>=22.5&&renderDebugMode<23.5)
        debugColor=hasDebugPage?float3(saturate(0.5+debugHeightGradient.x*2.0),saturate(debugSummary.bounds.z*0.1),saturate(debugSummary.bounds.w*10.0)):float3(1,0,1);
    else if(renderDebugMode>=23.5&&renderDebugMode<24.5)
        debugColor=abs(normal);
    else if(renderDebugMode>=24.5&&renderDebugMode<25.5)
        debugColor=hasDebugPage?float3(saturate(debugMasks),saturate(debugMetadata.state.y)):float3(1,0,1);
    else if(renderDebugMode>=25.5&&renderDebugMode<26.5)
    {
        float3 absoluteDirection=abs(terrainDir);
        float dominant=max(absoluteDirection.x,max(absoluteDirection.y,absoluteDirection.z));
        float second=absoluteDirection.x+absoluteDirection.y+absoluteDirection.z-dominant-min(absoluteDirection.x,min(absoluteDirection.y,absoluteDirection.z));
        float3 dominantAxis=absoluteDirection.x>=absoluteDirection.y&&absoluteDirection.x>=absoluteDirection.z?float3(sign(terrainDir.x),0,0):absoluteDirection.y>=absoluteDirection.z?float3(0,sign(terrainDir.y),0):float3(0,0,sign(terrainDir.z));
        float3 secondaryAxis=absoluteDirection.x<dominant&&absoluteDirection.x>=min(absoluteDirection.y,absoluteDirection.z)?float3(sign(terrainDir.x),0,0):absoluteDirection.y<dominant&&absoluteDirection.y>=min(absoluteDirection.x,absoluteDirection.z)?float3(0,sign(terrainDir.y),0):float3(0,0,sign(terrainDir.z));
        float3 crossDirection=normalize(secondaryAxis-dominantAxis*dot(secondaryAxis,terrainDir));
        float4 sideA,sideB; float2 sideMasks; bool hasA=zyTrySampleErosionPage(normalize(terrainDir-crossDirection*0.001),sideA,sideMasks); bool hasB=zyTrySampleErosionPage(normalize(terrainDir+crossDirection*0.001),sideB,sideMasks);
        float edgeProximity=1.0-saturate((dominant-second)*100.0);
        debugColor=float3(saturate(abs(sideA.x-sideB.x)*100.0)*edgeProximity,edgeProximity,(hasA&&hasB)?0.0:1.0);
    }
    else if(renderDebugMode>=26.5&&renderDebugMode<27.5)
    {
        float4 pageField; float2 pageMasks; bool hasPage=zyTrySampleErosionPage(terrainDir,pageField,pageMasks);
        float directHeight=hasDebugPage?zyAdvancedErosion(terrainDir,zySphericalField(terrainDir),planet.state.x,max(debugMetadata.state.w,1.0e-5)).x:0.0;
        float error=abs(directHeight-pageField.x);
        float bound=hasDebugPage?max(debugSummary.bounds.w,1.0e-5):1.0;
        debugColor=hasPage?float3(saturate(error/bound),saturate(error*100.0),error<=bound?0.0:1.0):float3(1,0,1);
    }
    else if(renderDebugMode>=27.5&&renderDebugMode<28.5)
    {
        float spacing=hasDebugPage?max(debugMetadata.state.w,1.0e-6):1.0;
        float wavelengthRatio=hasDebugPage?saturate(log2(max(planet.state.x,0.001)/spacing)/16.0):0.0;
        debugColor=float3(frac(zy_planet_page_set[0].state.z),wavelengthRatio,hasDebugPage?saturate(debugMetadata.address.y/5.0):0.0);
    }
    SdfSurface surface=sdfSurface(p,0);
    float3 shaded=(renderDebugMode>=21.5&&renderDebugMode<28.5)?debugColor:shadeSdf(0.0,travel,p,normal,0,surface);
    ZyPatchSceneOut output; output.colorTravel=float4(shaded,min(travel,farDistance+1.0));
    output.metadata=float4(FIELD_ID_SDF_OBJECT_BASE,normal); output.control=float4(1,4.0/384.0,saturate(surface.temporalDetail),saturate(surface.reservoirConfidence));
    float3 forward,right,up; cameraBasis(cameraPosition,cameraTarget,forward,right,up);
    output.reservoirGuide=float4(saturate(surface.reservoirConfidence),0,1,0); output.overdraw=1;
    output.depth=saturate(dot(p-cameraPosition,forward)/max(farDistance,0.0001)); return output;
}
