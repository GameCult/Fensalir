using System.Numerics;
using CultMath;

namespace Aquarium.Engine.Fractal.Projection;

public sealed class NormalizeCubeSphereProjection : ICubeSphereProjection
{
    public string Name => "normalize";

    public Vector3 Project(PlanetaryFaceCoordinate position)
    {
        return (Vector3)PlanetaryTopology.NormalizedCubeDirection(position);
    }
}
