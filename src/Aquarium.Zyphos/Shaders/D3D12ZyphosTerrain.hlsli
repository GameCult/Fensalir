#ifndef D3D12_ZYPHOS_TERRAIN_HLSLI
#define D3D12_ZYPHOS_TERRAIN_HLSLI

#include "CultMath/CultMath.hlsl"

struct ZyPlanetPageOutput { float4 height_gradient; float4 masks; };
struct ZyPlanetPageMetadata { float4 address; float4 layout; float4 bounds; float4 state; };
struct ZyPlanetPageSummary { float4 bounds; float4 metadata; };
struct ZyPlanetPageSet { float4 state; };
StructuredBuffer<ZyPlanetPageOutput> zy_planet_page : register(t81);
StructuredBuffer<ZyPlanetPageMetadata> zy_planet_page_metadata : register(t82);
StructuredBuffer<ZyPlanetPageSummary> zy_planet_page_summary : register(t83);
StructuredBuffer<ZyPlanetPageSet> zy_planet_page_set : register(t84);

static const float ZY_TERRAIN_NON_EROSION_SLOPE_BOUND = 8.0;

float2 zyTerrainCubeFaceUv(float3 dir, out float face)
{
    float3 a=abs(dir);
    if(a.x>=a.y&&a.x>=a.z){if(dir.x>=0){face=0;return float2(-dir.z,dir.y)/a.x;}face=1;return float2(dir.z,dir.y)/a.x;}
    if(a.y>=a.z){if(dir.y>=0){face=2;return float2(dir.x,-dir.z)/a.y;}face=3;return float2(dir.x,dir.z)/a.y;}
    if(dir.z>=0){face=4;return dir.xy/a.z;}face=5;return float2(-dir.x,dir.y)/a.z;
}

bool zyTerrainPageLocal(float3 dir, ZyPlanetPageMetadata metadata, out float2 local)
{
    float face; float2 uv=zyTerrainCubeFaceUv(dir,face); local=0.0;
    if(metadata.state.x<0.5||abs(face-metadata.address.x)>0.25)return false;
    float axisTiles=exp2(metadata.address.y); local=(uv*0.5+0.5)*axisTiles-metadata.address.zw;
    return all(local>=0.0)&&all(local<=1.0);
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
    heightGradient=lerp(lerp(a.height_gradient,b.height_gradient,fraction.x),lerp(c.height_gradient,d.height_gradient,fraction.x),fraction.y);
    masks=lerp(lerp(a.masks.xy,b.masks.xy,fraction.x),lerp(c.masks.xy,d.masks.xy,fraction.x),fraction.y);
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

CultMathAdvancedErosionParameters zyErosionParameters(float radius)
{
    CultMathAdvancedErosionParameters p;
    p.scale=radius*0.075; p.strength=0.12; p.gully_weight=0.58; p.detail=1.45;
    p.rounding=float4(0.1,0.015,0.1,2.0); p.onset=float4(1.25,1.25,2.8,1.5); p.assumed_slope=float2(0.7,0.85);
    p.cell_scale=0.7; p.normalization=0.5; p.octaves=7; p.lacunarity=2.0; p.gain=0.5; return p;
}

float4 zyAdvancedErosion(float3 dir, float field, float radius, float sampleSpacing)
{
    float3 world=dir*radius; float3 gradient=zySphericalFieldGradient(dir)/radius;
    float3 weights=pow(abs(dir),4.0); weights/=max(weights.x+weights.y+weights.z,1.0e-6);
    CultMathAdvancedErosionParameters p=zyErosionParameters(radius);
    CultMathErosionBandSelection band=cultmath_select_erosion_bands(p.scale*p.cell_scale,sampleSpacing,p.octaves,p.lacunarity,p.strength*p.scale,p.gain,2.0);
    float fade=saturate(field*0.5+0.5)*2.0-1.0;
    CultMathAdvancedErosionResult xy=cultmath_advanced_erosion_filter_banded(world.xy+float2(713,-291),float3(field,gradient.x,gradient.y),fade,p,band);
    CultMathAdvancedErosionResult yz=cultmath_advanced_erosion_filter_banded(world.yz+float2(-431,887),float3(field,gradient.y,gradient.z),fade,p,band);
    CultMathAdvancedErosionResult zx=cultmath_advanced_erosion_filter_banded(world.zx+float2(197,557),float3(field,gradient.z,gradient.x),fade,p,band);
    float height=xy.delta.x*weights.z+yz.delta.x*weights.x+zx.delta.x*weights.y;
    float ridge=xy.ridge_map*weights.z+yz.ridge_map*weights.x+zx.ridge_map*weights.y;
    return float4(height,saturate(ridge*0.5+0.5),saturate(0.5-ridge*0.5),band.unresolved_height_bound);
}

#endif
