#include "CultMath/CultMath.hlsl"

struct PlanetaryTopologyInput
{
    float4 direction_face;
    float4 coordinate_mode;
    float4 projection_parameters;
};

struct PlanetaryTopologyOutput
{
    float4 direction_face;
    float4 coordinate_valid;
};

StructuredBuffer<PlanetaryTopologyInput> topology_inputs : register(t0);
RWStructuredBuffer<PlanetaryTopologyOutput> topology_outputs : register(u0);
cbuffer PlanetaryTopologyConstants : register(b0) { uint topology_input_count; }

[numthreads(64, 1, 1)]
void D3D12PlanetaryTopologyParityCS(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= topology_input_count) return;
    PlanetaryTopologyInput input = topology_inputs[id.x];
    PlanetaryTopologyOutput output = (PlanetaryTopologyOutput)0;
    if (input.coordinate_mode.z < 0.5)
    {
        int face = (int)input.direction_face.w;
        output.direction_face = float4(cultmath_planetary_face_direction(face, input.coordinate_mode.xy), (float)face);
        output.coordinate_valid = float4(input.coordinate_mode.xy, 0.0, 1.0);
    }
    else if (input.coordinate_mode.z < 1.5)
    {
        int face;
        float2 coordinate = cultmath_planetary_face_coordinate(input.direction_face.xyz, face);
        output.direction_face = float4(normalize(input.direction_face.xyz), (float)face);
        output.coordinate_valid = float4(coordinate, 0.0, 1.0);
    }
    else if (input.coordinate_mode.z < 2.5)
    {
        output.direction_face = float4(normalize(input.direction_face.xyz), 0.0);
        output.coordinate_valid = float4(cultmath_planetary_equirectangular_forward(input.direction_face.xyz), 0.0, 1.0);
    }
    else if (input.coordinate_mode.z < 3.5)
    {
        output.direction_face = float4(cultmath_planetary_equirectangular_inverse(input.coordinate_mode.xy), 0.0);
        output.coordinate_valid = float4(input.coordinate_mode.xy, 0.0, 1.0);
    }
    else if (input.coordinate_mode.z < 4.5)
    {
        output.direction_face = float4(normalize(input.direction_face.xyz), 0.0);
        output.coordinate_valid = float4(cultmath_planetary_equal_earth_forward(input.direction_face.xyz), 0.0, 1.0);
    }
    else if (input.coordinate_mode.z < 5.5)
    {
        output.direction_face = float4(cultmath_planetary_equal_earth_inverse(input.coordinate_mode.xy), 0.0);
        output.coordinate_valid = float4(input.coordinate_mode.xy, 0.0, 1.0);
    }
    else if (input.coordinate_mode.z < 6.5)
    {
        float2 coordinate;
        bool valid=cultmath_planetary_projection_forward(input.direction_face.xyz,(int)input.coordinate_mode.w,input.projection_parameters.xyz,coordinate);
        output.direction_face=float4(normalize(input.direction_face.xyz),0);
        output.coordinate_valid=float4(coordinate,0,valid?1.0:0.0);
    }
    else
    {
        float3 direction;
        bool valid=cultmath_planetary_projection_inverse(input.coordinate_mode.xy,(int)input.coordinate_mode.w,input.projection_parameters.xyz,direction);
        output.direction_face=float4(direction,0);
        output.coordinate_valid=float4(input.coordinate_mode.xy,0,valid?1.0:0.0);
    }
    topology_outputs[id.x] = output;
}
