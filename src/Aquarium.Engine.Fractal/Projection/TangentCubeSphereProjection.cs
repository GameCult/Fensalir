using System.Numerics;
using CultMath;
using GameCult.Geometry;

namespace Aquarium.Engine.Fractal.Projection;

public sealed class TangentCubeSphereProjection : ICubeSphereProjection
{
    public string Name => "tangent";

    public Vector3 Project(PlanetaryFaceCoordinate position)
    {
        return (Vector3)PlanetaryTopology.Direction(position);
    }
}
