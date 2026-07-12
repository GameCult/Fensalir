using System.Numerics;

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
    private const double QuarterPi = Math.PI * 0.25;

    public static Vector3 Direction(PlanetarySurfacePageRequest request, int storageX, int storageY)
    {
        request.Validate();
        if ((uint)storageX >= request.StorageSize || (uint)storageY >= request.StorageSize) throw new ArgumentOutOfRangeException();
        var localU = (storageX - request.BorderSize) / (double)(request.InteriorSize - 1);
        var localV = (storageY - request.BorderSize) / (double)(request.InteriorSize - 1);
        return DirectionAtLocal(request, localU, localV);
    }

    public static Vector3 DirectionAtLocal(PlanetarySurfacePageRequest request, double localU, double localV)
    {
        request.Validate();
        if (!double.IsFinite(localU) || !double.IsFinite(localV)) throw new ArgumentOutOfRangeException();
        var count = request.Tile.AxisTileCount;
        var faceU = -1.0 + 2.0 * (request.Tile.X + localU) / count;
        var faceV = -1.0 + 2.0 * (request.Tile.Y + localV) / count;
        var u = Math.Tan(faceU * QuarterPi);
        var v = Math.Tan(faceV * QuarterPi);
        var cube = request.Tile.Face switch
        {
            CubeFace.PositiveX => new Vector3(1, (float)v, (float)-u), CubeFace.NegativeX => new Vector3(-1, (float)v, (float)u),
            CubeFace.PositiveY => new Vector3((float)u, 1, (float)-v), CubeFace.NegativeY => new Vector3((float)u, -1, (float)v),
            CubeFace.PositiveZ => new Vector3((float)u, (float)v, 1), CubeFace.NegativeZ => new Vector3((float)-u, (float)v, -1),
            _ => throw new ArgumentOutOfRangeException(),
        };
        return Vector3.Normalize(cube);
    }

    public static float AngularTexelSize(PlanetarySurfacePageRequest request)
    {
        var center = request.BorderSize + (request.InteriorSize - 1) / 2;
        var a = Direction(request, center, center);
        var b = Direction(request, Math.Min(center + 1, request.StorageSize - 1), center);
        return MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1.0f, 1.0f));
    }

    public static float NominalAngularTexelSize(PlanetarySurfacePageRequest request)
    {
        request.Validate();
        return (MathF.PI * 0.5f) / (request.Tile.AxisTileCount * (request.InteriorSize - 1));
    }
}

public static class PlanetarySurfaceDifferential
{
    public static Vector3 SurfaceNormal(Vector3 unitDirection, Vector3 worldDistanceTangentGradient)
    {
        if (!float.IsFinite(unitDirection.X) || !float.IsFinite(unitDirection.Y) || !float.IsFinite(unitDirection.Z) || unitDirection.LengthSquared() < 1.0e-12f)
            throw new ArgumentOutOfRangeException(nameof(unitDirection));
        if (!float.IsFinite(worldDistanceTangentGradient.X) || !float.IsFinite(worldDistanceTangentGradient.Y) || !float.IsFinite(worldDistanceTangentGradient.Z))
            throw new ArgumentOutOfRangeException(nameof(worldDistanceTangentGradient));
        var direction=Vector3.Normalize(unitDirection);
        var tangentGradient=worldDistanceTangentGradient-direction*Vector3.Dot(worldDistanceTangentGradient,direction);
        return Vector3.Normalize(direction-tangentGradient);
    }
}
