#ifndef D3D12_ZYPHOS_TERRAIN_HLSLI
#define D3D12_ZYPHOS_TERRAIN_HLSLI

#include "GameCult.Geometry/GameCult.Geometry.hlsl"

struct ZyPlanetPageOutput { float4 height_gradient; float4 masks; };
struct ZyPlanetPageMetadata { float4 address; float4 layout; float4 bounds; float4 state; };
struct ZyPlanetPageSummary { float4 bounds; float4 metadata; };
struct ZyPlanetPageSet { float4 state; };
StructuredBuffer<ZyPlanetPageOutput> zy_planet_page : register(t81);
StructuredBuffer<ZyPlanetPageMetadata> zy_planet_page_metadata : register(t82);
StructuredBuffer<ZyPlanetPageSummary> zy_planet_page_summary : register(t83);
StructuredBuffer<ZyPlanetPageSet> zy_planet_page_set : register(t84);

static const float ZY_TERRAIN_NON_EROSION_SLOPE_BOUND = 8.0;

bool zyTerrainPageLocal(float3 dir, ZyPlanetPageMetadata metadata, out float2 local)
{
    return gamecult_geometry_planetary_page_local(dir,metadata.address,metadata.state.x,local);
}

void zySampleErosionPage(int pageIndex, float2 local, out float4 heightGradient, out float2 masks)
{
    ZyPlanetPageMetadata metadata=zy_planet_page_metadata[pageIndex];
    float storage=metadata.layout.y, interior=metadata.layout.z, border=metadata.layout.w;
    float2 texel=local*(interior-1.0)+border; float2 base=floor(texel); float2 fraction=frac(texel);
    int2 p0=(int2)clamp(base,0.0,storage-1.0); int2 p1=min(p0+1,(int2)(storage-1.0));
    int offset=(int)metadata.layout.x;
    ZyPlanetPageOutput a=zy_planet_page[offset+p0.y*(int)storage+p0.x], b=zy_planet_page[offset+p0.y*(int)storage+p1.x];
    ZyPlanetPageOutput c=zy_planet_page[offset+p1.y*(int)storage+p0.x], d=zy_planet_page[offset+p1.y*(int)storage+p1.x];
    GameCultGeometryPlanetaryPageSample ca,cb,cc,cd;
    ca.height_gradient=a.height_gradient; ca.masks=a.masks.xy;
    cb.height_gradient=b.height_gradient; cb.masks=b.masks.xy;
    cc.height_gradient=c.height_gradient; cc.masks=c.masks.xy;
    cd.height_gradient=d.height_gradient; cd.masks=d.masks.xy;
    GameCultGeometryPlanetaryPageSample sample=gamecult_geometry_planetary_page_lerp(ca,cb,cc,cd,fraction);
    heightGradient=sample.height_gradient; masks=sample.masks;
}

bool zyTrySampleErosionPage(float3 dir, out float4 heightGradient, out float2 masks)
{
    heightGradient=0.0; masks=0.0; bool found=false;
    int pageCount=min((int)zy_planet_page_set[0].state.x,64);
    [loop] for(int pageIndex=0;pageIndex<pageCount;pageIndex++)
    {
        float2 local; ZyPlanetPageMetadata metadata=zy_planet_page_metadata[pageIndex];
        if(!zyTerrainPageLocal(dir,metadata,local))continue;
        float4 contribution; float2 pageMasks; zySampleErosionPage(pageIndex,local,contribution,pageMasks);
        float blend=saturate(metadata.state.y); heightGradient+=contribution*blend;
        masks=found?lerp(masks,pageMasks,blend):pageMasks; found=true;
    }
    return found;
}

bool zyTerrainDeepestPageEvidence(
    float3 dir,
    out ZyPlanetPageMetadata selectedMetadata,
    out ZyPlanetPageSummary selectedSummary,
    out float4 selectedHeightGradient,
    out float2 selectedMasks)
{
    selectedMetadata=(ZyPlanetPageMetadata)0;
    selectedSummary=(ZyPlanetPageSummary)0;
    selectedHeightGradient=0.0;
    selectedMasks=0.0;
    float selectedLevel=-1.0;
    int pageCount=min((int)zy_planet_page_set[0].state.x,64);
    [loop] for(int pageIndex=0;pageIndex<pageCount;pageIndex++)
    {
        float2 local; ZyPlanetPageMetadata metadata=zy_planet_page_metadata[pageIndex];
        ZyPlanetPageSummary summary=zy_planet_page_summary[pageIndex];
        if(metadata.address.y<selectedLevel||summary.metadata.w<=0.5||!zyTerrainPageLocal(dir,metadata,local))continue;
        selectedLevel=metadata.address.y;
        selectedMetadata=metadata;
        selectedSummary=summary;
        zySampleErosionPage(pageIndex,local,selectedHeightGradient,selectedMasks);
    }
    return selectedLevel>=0.0;
}

float zyTerrainConservativeDistanceScale(float3 dir)
{
    float erosionSlope=0.0; int pageCount=min((int)zy_planet_page_set[0].state.x,64);
    [loop] for(int pageIndex=0;pageIndex<pageCount;pageIndex++)
    {
        float2 local; ZyPlanetPageMetadata metadata=zy_planet_page_metadata[pageIndex];
        if(zyTerrainPageLocal(dir,metadata,local)&&zy_planet_page_summary[pageIndex].metadata.w>0.5)
            erosionSlope+=max(zy_planet_page_summary[pageIndex].bounds.z,0.0)*saturate(metadata.state.y);
    }
    float totalSlopeBound = ZY_TERRAIN_NON_EROSION_SLOPE_BOUND + max(erosionSlope, 0.0);
    return rsqrt(1.0 + totalSlopeBound * totalSlopeBound);
}

float zyTerrainPatchCaptureMargin(float3 dir)
{
    // The raster shell must contain unresolved relief between coarse vertices;
    // otherwise the pixel refinement has no fragment on which to recover it.
    float margin=0.03;
    int pageCount=min((int)zy_planet_page_set[0].state.x,64);
    [loop] for(int pageIndex=0;pageIndex<pageCount;pageIndex++)
    {
        float2 local; ZyPlanetPageMetadata metadata=zy_planet_page_metadata[pageIndex];
        ZyPlanetPageSummary summary=zy_planet_page_summary[pageIndex];
        if(!zyTerrainPageLocal(dir,metadata,local)||summary.metadata.w<=0.5)continue;
        float blend=saturate(metadata.state.y);
        margin+=max(abs(summary.bounds.x),abs(summary.bounds.y))*blend;
        margin+=max(summary.bounds.w,0.0);
    }
    // Four 0.08 world-unit pixel corrections define the capture interval.
    return min(margin,0.30);
}

float zySphericalField(float3 dir)
{
    float latitude = asin(saturate(abs(dir.z)) * 2.0 - 1.0);
    float longitude = atan2(dir.y, dir.x);
    float plates = sin(longitude * 2.0 + dir.z * 4.5) * 0.28 + sin(longitude * 3.0 - dir.z * 7.0 + 1.7) * 0.22
        + sin((dir.x + dir.y * 0.7 + dir.z * 0.4) * 8.0) * 0.16 + sin((dir.x * 1.3 - dir.y + dir.z * 1.9) * 13.0) * 0.08;
    return 0.50 + plates + cos(latitude) * 0.18;
}

float3 zySphericalFieldGradient(float3 dir)
{
    const float epsilon = 0.0015;
    float dx = zySphericalField(normalize(dir + float3(epsilon,0,0))) - zySphericalField(normalize(dir - float3(epsilon,0,0)));
    float dy = zySphericalField(normalize(dir + float3(0,epsilon,0))) - zySphericalField(normalize(dir - float3(0,epsilon,0)));
    float dz = zySphericalField(normalize(dir + float3(0,0,epsilon))) - zySphericalField(normalize(dir - float3(0,0,epsilon)));
    float3 gradient = float3(dx,dy,dz)/(2.0*epsilon); return gradient-dir*dot(gradient,dir);
}

GameCultGeometryAdvancedErosionParameters zyErosionParameters(float radius)
{
    GameCultGeometryAdvancedErosionParameters p;
    p.scale=radius*0.075; p.strength=0.12; p.gully_weight=0.58; p.detail=1.45;
    p.rounding=float4(0.1,0.015,0.1,2.0); p.onset=float4(1.25,1.25,2.8,1.5); p.assumed_slope=float2(0.7,0.85);
    p.cell_scale=0.7; p.normalization=0.5; p.octaves=7; p.lacunarity=2.0; p.gain=0.5; return p;
}

GameCultGeometryPlanetarySurfaceSample zyAdvancedErosionSurface(float3 dir, float field, float radius, float sampleSpacing)
{
    GameCultGeometryPlanetaryFieldDefinition definition;
    definition.radius=radius; definition.seed=0; definition.erosion=zyErosionParameters(radius);
    GameCultGeometryPlanetaryBaseFieldSample base_sample;
    base_sample.radial_displacement=0.0; base_sample.radial_gradient=0.0;
    base_sample.field_value=field; base_sample.field_gradient=zySphericalFieldGradient(dir)/radius;
    base_sample.fade_target=saturate(field*0.5+0.5)*2.0-1.0;
    return gamecult_geometry_planetary_field_sample(definition,dir,base_sample,sampleSpacing);
}

float4 zyAdvancedErosion(float3 dir, float field, float radius, float sampleSpacing)
{
    GameCultGeometryPlanetarySurfaceSample sample=zyAdvancedErosionSurface(dir,field,radius,sampleSpacing);
    return float4(sample.radial_displacement,sample.ridge,sample.gully,sample.unresolved_height_bound);
}

#endif
