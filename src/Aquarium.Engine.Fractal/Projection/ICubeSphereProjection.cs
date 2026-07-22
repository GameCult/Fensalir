using System.Numerics;
using CultMath;
using GameCult.Geometry;

namespace Aquarium.Engine.Fractal.Projection;

public interface ICubeSphereProjection
{
    string Name { get; }

    Vector3 Project(PlanetaryFaceCoordinate position);
}
