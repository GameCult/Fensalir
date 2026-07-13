static const int SDF_INDEX = 0;
static const int ZYPHOS_GEOMETRY_BRUSH_LIMIT = 4;

#include "D3D12SdfCommon.hlsli"
#include "D3D12SdfMath.hlsli"
#include "CultMath/CultMath.hlsl"
#include "D3D12ZyphosTerrain.hlsli"

float3 zyRotateZ(float3 p, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float3(c * p.x - s * p.y, s * p.x + c * p.y, p.z);
}

float3 zyPlanetDir(float3 local, SdfObject sdfObject)
{
    return normalize(zyRotateZ(local, -sdfObject.state.y));
}

float3 zyPrimaryStarDirectionLocal(SdfObject sdfObject)
{
    return normalize(float3(cos(sdfObject.state.w), sin(sdfObject.state.w), 0.18));
}

float zyUmbrosEclipse(float3 starDirectionLocal)
{
    const float umbrosAngularRadius = 0.1127;
    float alignment = dot(normalize(starDirectionLocal), float3(1.0, 0.0, 0.0));
    return smoothstep(cos(umbrosAngularRadius * 1.45), cos(umbrosAngularRadius * 0.45), alignment);
}

float zyHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float zyCompactBrush(float2 delta, float2 radii, float rotation, float falloff, float shapePower)
{
    float c = cos(rotation);
    float s = sin(rotation);
    float2 local = float2(delta.x * c + delta.y * s, -delta.x * s + delta.y * c);
    float2 normalized = local / max(radii, float2(0.001, 0.001));
    float r2 = dot(normalized, normalized);
    if (r2 >= 1.0)
    {
        return 0.0;
    }

    float edgeValue = exp(-falloff);
    float gaussianValue = exp(-falloff * r2);
    float compactValue = (gaussianValue - edgeValue) / max(1.0 - edgeValue, 0.000001);
    return pow(saturate(compactValue), shapePower);
}

float2 zyCubeFaceUv(float3 dir, out float face)
{
    // Authored brush packets retain their legacy cube-ratio chart. Planetary
    // page addressing and patch geometry use CultMath's QSC topology.
    float3 a = abs(dir);
    if (a.x >= a.y && a.x >= a.z)
    {
        if (dir.x >= 0.0) { face = 0.0; return float2(-dir.z, dir.y) / max(a.x, 0.0001); }
        face = 1.0; return float2(dir.z, dir.y) / max(a.x, 0.0001);
    }
    if (a.y >= a.z)
    {
        if (dir.y >= 0.0) { face = 2.0; return float2(dir.x, -dir.z) / max(a.y, 0.0001); }
        face = 3.0; return float2(dir.x, dir.z) / max(a.y, 0.0001);
    }
    if (dir.z >= 0.0) { face = 4.0; return dir.xy / max(a.z, 0.0001); }
    face = 5.0; return float2(-dir.x, dir.y) / max(a.z, 0.0001);
}

float2 zyTileBrushPlane(float2 faceUv, float level, float tileX, float tileY, out float inTile)
{
    float axisTiles = exp2(level);
    float2 local01 = (faceUv * 0.5 + 0.5) * axisTiles - float2(tileX, tileY);
    float2 inside = step(float2(0.0, 0.0), local01) * step(local01, float2(1.0, 1.0));
    inTile = inside.x * inside.y;
    return (local01 * 2.0 - 1.0) * 30.0;
}

float zyAuthoredBrushTerrainLimited(float3 dir, int brushLimit, out float materialMask)
{
    float face;
    float2 faceUv = zyCubeFaceUv(dir, face);
    float height = 0.0;
    materialMask = 0.0;

    [loop]
    for (int index = 0; index < brushLimit; index++)
    {
        float4 centerRadius = brushCenterRadius[index];
        float4 shape = brushShape[index];
        float4 domain = brushDomain[index];
        if (centerRadius.z <= 0.0 || abs(domain.x - face) > 0.25)
        {
            continue;
        }

        float inTile;
        float2 plane = zyTileBrushPlane(faceUv, domain.y, domain.z, domain.w, inTile);
        if (inTile <= 0.0)
        {
            continue;
        }

        float2 radii = float2(centerRadius.z, centerRadius.w > 0.0 ? centerRadius.w : centerRadius.z);
        float weight = zyCompactBrush(plane - centerRadius.xy, radii, shape.z, max(shape.w, 0.001), max(shape.x, 0.001));
        height += shape.y * weight;
        materialMask = max(materialMask, abs(shape.y) * weight);
    }

    return height;
}

float zyAuthoredBrushTerrain(float3 dir, out float materialMask)
{
    return zyAuthoredBrushTerrainLimited(dir, 64, materialMask);
}

float3 zyFaceDebugColor(float face)
{
    if (face < 0.5) return float3(0.95, 0.18, 0.14);
    if (face < 1.5) return float3(0.12, 0.56, 1.00);
    if (face < 2.5) return float3(0.20, 0.88, 0.34);
    if (face < 3.5) return float3(1.00, 0.78, 0.18);
    if (face < 4.5) return float3(0.78, 0.36, 1.00);
    return float3(0.10, 0.90, 0.86);
}

float3 zyFractalDomainDebug(float3 dir)
{
    float face;
    float2 faceUv = zyCubeFaceUv(dir, face);
    float materialMask;
    float authoredHeight = zyAuthoredBrushTerrain(dir, materialMask);
    float2 grid = abs(frac((faceUv * 0.5 + 0.5) * 8.0) - 0.5);
    float gridLine = 1.0 - smoothstep(0.015, 0.055, min(grid.x, grid.y));
    float3 faceColor = zyFaceDebugColor(face);
    float signedHeight = authoredHeight >= 0.0 ? 1.0 : 0.0;
    float3 brushColor = lerp(float3(0.12, 0.28, 1.0), float3(1.0, 0.42, 0.12), signedHeight) * saturate(materialMask * 5.0);
    return saturate(faceColor * 0.28 + gridLine.xxx * 0.34 + brushColor);
}

float zyTileRelief(float2 uv, float level, float amplitude, float ridgeBias)
{
    float scale = exp2(level);
    float2 cell = floor(uv * scale);
    float2 local = frac(uv * scale) - 0.5;
    float seed = zyHash21(cell + level * 17.0);
    float angle = seed * 6.2831853;
    float2 axis = float2(cos(angle), sin(angle));
    float ridge = 1.0 - smoothstep(0.035, 0.18, abs(dot(local, axis) + (seed - 0.5) * 0.16));
    float pit = 1.0 - smoothstep(0.06, 0.32, length(local - axis * 0.18));
    return (ridge * ridgeBias - pit * (1.0 - ridgeBias)) * amplitude;
}

float zyQuadtreeSdfRelief(float3 dir)
{
    float2 equatorial = dir.xy * 0.5 + 0.5;
    float2 polar = float2(atan2(dir.y, dir.x) * 0.15915494 + 0.5, abs(dir.z));
    float polarBlend = smoothstep(0.58, 0.92, abs(dir.z));
    float2 uv = lerp(equatorial, polar, polarBlend);
    float relief = 0.0;
    relief += zyTileRelief(uv + 0.07, 2.0, 0.030, 0.68);
    relief += zyTileRelief(uv * 1.37 + 0.31, 3.0, 0.019, 0.55);
    relief += zyTileRelief(uv * 2.13 - 0.19, 4.0, 0.010, 0.45);
    return relief;
}

float zyDomainMask(float3 dir, float3 center, float width)
{
    return smoothstep(cos(width), cos(width * 0.35), dot(dir, normalize(center)));
}

float zyLeafCluster(float3 dir)
{
    float forest = zyDomainMask(dir, float3(0.32, 0.68, 0.16), 0.42);
    float leafCells =
        sin(dir.x * 91.0 + dir.y * 37.0) *
        sin(dir.y * 113.0 - dir.z * 53.0) *
        sin((dir.x + dir.z) * 157.0);
    float leaf = smoothstep(0.36, 0.92, leafCells * 0.5 + 0.5);
    return forest * leaf;
}

float zyPebbleCluster(float3 dir)
{
    float coast = zyDomainMask(dir, float3(0.57, -0.28, 0.10), 0.24);
    float grains = sin(dir.x * 173.0 - dir.y * 71.0) * sin(dir.z * 127.0 + dir.y * 59.0);
    return coast * smoothstep(0.48, 0.94, grains * 0.5 + 0.5);
}

float zyTerrainBaseOffset(float3 dir, SdfObject sdfObject, out float land)
{
    float field = zySphericalField(dir);
    float seaLevel = sdfObject.state.z;
    land = smoothstep(seaLevel - 0.04, seaLevel + 0.08, field);
    float mountain = pow(saturate(field - seaLevel), 1.65);
    float polarCap = pow(abs(dir.z), 8.0) * 0.035;
    float authoredMaterial;
    float authoredGeometry = zyAuthoredBrushTerrainLimited(dir, ZYPHOS_GEOMETRY_BRUSH_LIMIT, authoredMaterial);
    float tileRelief = zyQuadtreeSdfRelief(dir) * land + zyLeafCluster(dir) * 0.018 + zyPebbleCluster(dir) * 0.010;
    return (field - seaLevel) * 0.10 + mountain * 0.13 + polarCap * land + tileRelief + authoredGeometry * 0.52;
}

float zyTerrainOffset(float3 dir, SdfObject sdfObject)
{
    float land; float baseOffset=zyTerrainBaseOffset(dir,sdfObject,land);
    float4 pageHeightGradient; float2 pageMasks;
    float erosionHeight=zyTrySampleErosionPage(dir,pageHeightGradient,pageMasks)?pageHeightGradient.x:0.0;
    return baseOffset+erosionHeight*land;
}

float sdfDistance(float3 p, int sdfIndex)
{
    SdfObject sdfObject = sdfObjects[sdfIndex];
    float planetRadius = max(sdfObject.state.x, 0.001);
    float3 local = p - sdfObject.centerRadius.xyz;
    float3 dir = zyPlanetDir(local, sdfObject);
    float radialDistance = length(local) - (planetRadius + zyTerrainOffset(dir, sdfObject));
    return radialDistance * zyTerrainConservativeDistanceScale(dir);
}

float zyPlanetTraceBoundRadius(SdfObject sdfObject)
{
    float legacyBound = max(sdfObject.centerRadius.w * 1.42, 0.001);
    float erosionExtent=0.0; int pageCount=min((int)zy_planet_page_set[0].state.x,64);
    [loop] for(int pageIndex=0;pageIndex<pageCount;pageIndex++)
    {
        ZyPlanetPageSummary summary=zy_planet_page_summary[pageIndex]; ZyPlanetPageMetadata metadata=zy_planet_page_metadata[pageIndex];
        if(metadata.state.x<0.5||summary.metadata.w<0.5)continue;
        erosionExtent+=max(abs(summary.bounds.x),abs(summary.bounds.y))*saturate(metadata.state.y)+max(summary.bounds.w,0.0);
    }
    return max(legacyBound, max(sdfObject.state.x, 0.001) + erosionExtent);
}

float3 zyPlanetSurfaceNormal(float3 p, int sdfIndex)
{
    SdfObject sdfObject=sdfObjects[sdfIndex]; float radius=max(sdfObject.state.x,0.001);
    float3 local=p-sdfObject.centerRadius.xyz; float3 dir=zyPlanetDir(local,sdfObject);
    float3 reference=abs(dir.z)<0.8?float3(0,0,1):float3(0,1,0);
    float3 tangentX=normalize(cross(reference,dir)); float3 tangentY=normalize(cross(dir,tangentX));
    float spacing=max(radius/8192.0,0.0005); float angular=spacing/radius;
    float landCenter; zyTerrainBaseOffset(dir,sdfObject,landCenter);
    float landXp,landXm,landYp,landYm;
    float baseXp=zyTerrainBaseOffset(normalize(dir+tangentX*angular),sdfObject,landXp);
    float baseXm=zyTerrainBaseOffset(normalize(dir-tangentX*angular),sdfObject,landXm);
    float baseYp=zyTerrainBaseOffset(normalize(dir+tangentY*angular),sdfObject,landYp);
    float baseYm=zyTerrainBaseOffset(normalize(dir-tangentY*angular),sdfObject,landYm);
    float3 baseGradient=tangentX*((baseXp-baseXm)/(2.0*spacing))+tangentY*((baseYp-baseYm)/(2.0*spacing));
    float3 landGradient=tangentX*((landXp-landXm)/(2.0*spacing))+tangentY*((landYp-landYm)/(2.0*spacing));
    float4 erosion; float2 masks; erosion=0.0;
    zyTrySampleErosionPage(dir,erosion,masks);
    float3 erosionGradient=erosion.yzw;
    float3 planetNormal=normalize(dir-(baseGradient+landCenter*erosionGradient+erosion.x*landGradient));
    return normalize(zyRotateZ(planetNormal,sdfObject.state.y));
}

SdfSurface sdfSurface(float3 p, int sdfIndex)
{
    SdfObject sdfObject = sdfObjects[sdfIndex];
    float3 local = p - sdfObject.centerRadius.xyz;
    float3 dir = zyPlanetDir(local, sdfObject);
    float field = zySphericalField(dir);
    float seaLevel = sdfObject.state.z;
    float land = smoothstep(seaLevel - 0.03, seaLevel + 0.06, field);
    float mountain = smoothstep(seaLevel + 0.12, seaLevel + 0.28, field);
    float tileRelief = zyQuadtreeSdfRelief(dir);
    float authoredMaterial;
    float authoredHeight = zyAuthoredBrushTerrain(dir, authoredMaterial);
    float leafCluster = zyLeafCluster(dir);
    float pebbleCluster = zyPebbleCluster(dir);
    float polar = smoothstep(0.72, 0.92, abs(dir.z));
    float4 erosionDifferential; float2 erosionMasks;
    zyTrySampleErosionPage(dir,erosionDifferential,erosionMasks);
    float ridge=erosionMasks.x, gully=erosionMasks.y;
    float cloud = smoothstep(0.73, 0.91, sin(dir.x * 18.0 + dir.y * 13.0 + dir.z * 9.0 + timeSeconds * 0.19) * 0.5 + 0.5);

    float3 ocean = lerp(float3(0.004, 0.070, 0.130), float3(0.010, 0.220, 0.330), saturate(field));
    float3 lowland = float3(0.070, 0.300, 0.185);
    float3 highland = float3(0.540, 0.405, 0.220);
    float3 snow = float3(0.800, 0.865, 0.830);
    float3 landColor = lerp(lowland, highland, mountain);
    landColor = lerp(landColor, snow, saturate(polar + mountain * 0.38));
    landColor = lerp(landColor, float3(0.62, 0.54, 0.36), saturate(tileRelief * 14.0));
    landColor = lerp(landColor, float3(0.78, 0.56, 0.30), saturate(authoredMaterial * 2.6 + authoredHeight * 5.0));
    landColor = lerp(landColor, float3(0.025, 0.24, 0.08), leafCluster * 0.82);
    landColor = lerp(landColor, float3(0.42, 0.39, 0.34), pebbleCluster * 0.65);
    landColor = lerp(landColor, float3(0.49,0.44,0.36), ridge*0.34);
    landColor = lerp(landColor, float3(0.035,0.16,0.11), gully*0.28);

    SdfSurface surface;
    surface.baseColor = lerp(ocean, landColor, land);
    surface.baseColor = lerp(surface.baseColor, float3(0.78, 0.84, 0.80), cloud * 0.16);
    surface.metallic = 0.0;
    surface.roughness = saturate(lerp(0.34,0.84,land)+ridge*0.09-gully*0.12);
    surface.emission = 0.0;
    surface.temporalDetail = saturate(authoredMaterial * 3.0);
    surface.reservoirConfidence = saturate(0.42 + authoredMaterial * 2.2 + leafCluster * 0.25 + pebbleCluster * 0.20);
    return surface;
}

bool zyTracePlanet(float3 origin,float3 direction,int sdfIndex,out float travel,out float3 normal,out SdfSurface surface,out float stepCount)
{
    SdfObject sdfObject=sdfObjects[sdfIndex]; float boundRadius=zyPlanetTraceBoundRadius(sdfObject);
    float3 oc=origin-sdfObject.centerRadius.xyz; float b=dot(oc,direction); float c=dot(oc,oc)-boundRadius*boundRadius;
    float discriminant=b*b-c; normal=0.0; stepCount=0.0;
    surface.baseColor=0.0; surface.metallic=0.0; surface.roughness=0.0; surface.emission=0.0; surface.temporalDetail=0.0; surface.reservoirConfidence=1.0;
    if(discriminant<0.0){travel=farDistance+1.0;return false;}
    float extent=sqrt(discriminant); float entry=max(-b-extent,0.0); float closest=max(-b,entry); float exit=min(-b+extent,farDistance);
    if(entry>exit){travel=farDistance+1.0;return false;}
    float low=entry; float lowDistance=sdfDistance(origin+direction*low,sdfIndex);
    if(lowDistance<=0.0){travel=low;float3 p=origin+direction*travel;normal=zyPlanetSurfaceNormal(p,sdfIndex);surface=sdfSurface(p,sdfIndex);return true;}
    float high=low; bool bracketed=false;
    [unroll] for(int coarse=1;coarse<=24;coarse++)
    {
        high=lerp(entry,closest,(float)coarse/24.0); float highDistance=sdfDistance(origin+direction*high,sdfIndex); stepCount=(float)coarse;
        if(highDistance<=0.0){bracketed=true;break;} low=high; lowDistance=highDistance;
    }
    if(!bracketed){travel=farDistance+1.0;return false;}
    [unroll] for(int refine=0;refine<10;refine++)
    {
        float mid=(low+high)*0.5; float value=sdfDistance(origin+direction*mid,sdfIndex);
        if(value<=0.0)high=mid;else low=mid;
    }
    travel=high; float3 p=origin+direction*travel; normal=zyPlanetSurfaceNormal(p,sdfIndex); surface=sdfSurface(p,sdfIndex); stepCount+=10.0; return true;
}

float3 shadeSdf(float2 uv, float travel, float3 p, float3 normal, int sdfIndex, SdfSurface surface)
{
    SdfObject sdfObject = sdfObjects[sdfIndex];
    float3 dir = zyPlanetDir(p - sdfObject.centerRadius.xyz, sdfObject);
    if (renderDebugMode >= 10.5 && renderDebugMode < 11.5)
    {
        return zyFractalDomainDebug(dir);
    }

    float3 viewDirection = normalize(cameraPosition - p);
    float3 starDirectionLocal = zyPrimaryStarDirectionLocal(sdfObject);
    float eclipse = zyUmbrosEclipse(starDirectionLocal);
    float daylight = saturate(dot(dir, starDirectionLocal) * 0.74 + 0.24) * (1.0 - eclipse * 0.86);
    float night = saturate(-dot(dir, starDirectionLocal) * 1.7 - 0.20 + eclipse * 0.55);
    float citySeed = sin(dir.x * 43.0 + sin(dir.y * 19.0) * 4.0 + dir.z * 31.0);
    float cityWeb = smoothstep(0.82, 0.96, citySeed * 0.5 + 0.5);
    float coast = 1.0 - smoothstep(0.020, 0.070, abs(zySphericalField(dir) - sdfObject.state.z));
    float fresnel = pow(1.0 - saturate(dot(normal, viewDirection)), 3.2);
    float3 city = float3(1.0, 0.54, 0.14) * cityWeb * coast * night * 1.25;
    float3 eclipseTint = float3(0.15, 0.19, 0.26) * eclipse * saturate(dot(dir, starDirectionLocal) * 0.5 + 0.5);
    float3 atmosphere = float3(0.07, 0.38, 0.78) * (fresnel * 1.35 + pow(fresnel, 6.0) * 2.6);

    return shadeSdfPbr(p, normal, surface) * daylight + city + atmosphere + eclipseTint;
}

#define SDF_TRACE_BOUND_RADIUS(sdfObject) zyPlanetTraceBoundRadius(sdfObject)
#define SDF_SURFACE_NORMAL(p,sdfIndex) zyPlanetSurfaceNormal(p,sdfIndex)
#define SDF_TRACE_FUNCTION zyTracePlanet
#include "D3D12SdfProxy.hlsli"
