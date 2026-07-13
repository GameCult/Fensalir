using System.Numerics;
using CultMath;

namespace Aquarium.Engine.Fractal.Lod;

public readonly record struct PlanetarySurfacePageRequest(CubeTileKey Tile, int InteriorSize, int BorderSize)
{
    public int StorageSize => checked(InteriorSize + BorderSize * 2);

    public PlanetarySurfacePageRequest Validate()
    {
        if (InteriorSize < 2) throw new ArgumentOutOfRangeException(nameof(InteriorSize));
        if (BorderSize < 0 || BorderSize >= InteriorSize) throw new ArgumentOutOfRangeException(nameof(BorderSize));
        return this;
    }
}

public readonly record struct PlanetarySurfacePageSummary(
    float MinimumHeight,
    float MaximumHeight,
    float MaximumSlope,
    float UnresolvedHeightBound,
    float AngularTexelSize);

public static class PlanetarySurfacePageSampling
{
    public static Vector3 Direction(PlanetarySurfacePageRequest request, int storageX, int storageY)
    {
        return (Vector3)PlanetaryPageSampling.Direction(ToCultMath(request), storageX, storageY);
    }

    public static Vector3 DirectionAtLocal(PlanetarySurfacePageRequest request, double localU, double localV)
    {
        return (Vector3)PlanetaryPageSampling.DirectionAtLocal(ToCultMath(request), localU, localV);
    }

    public static float AngularTexelSize(PlanetarySurfacePageRequest request)
    {
        return PlanetaryPageSampling.AngularTexelSize(ToCultMath(request));
    }

    public static float NominalAngularTexelSize(PlanetarySurfacePageRequest request)
    {
        return PlanetaryPageSampling.NominalAngularTexelSize(ToCultMath(request));
    }

    private static PlanetaryPageLayout ToCultMath(PlanetarySurfacePageRequest request)
        => new(request.Tile.ToCultMath(), request.InteriorSize, request.BorderSize);
}

public static class PlanetarySurfaceDifferential
{
    public static Vector3 SurfaceNormal(Vector3 unitDirection, Vector3 worldDistanceTangentGradient)
    {
        return (Vector3)PlanetaryTopology.SurfaceNormal((float3)unitDirection, (float3)worldDistanceTangentGradient);
    }
}
