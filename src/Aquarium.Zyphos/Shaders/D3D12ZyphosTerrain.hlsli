#ifndef D3D12_ZYPHOS_TERRAIN_HLSLI
#define D3D12_ZYPHOS_TERRAIN_HLSLI

#include "CultMath/CultMath.hlsl"

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
