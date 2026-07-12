using System.Numerics;
using Aquarium.Engine.Fractal;
using Aquarium.Engine.Fractal.Lod;
using Aquarium.Engine.Render;

namespace Aquarium.Zyphos;

internal static class ZyphosPlanetarySurfacePages
{
    private static readonly Dictionary<CubeFace, AquariumPlanetarySurfacePageSet> Pages = [];

    public static AquariumPlanetarySurfacePageSet ForCamera(Vector3 cameraPosition)
    {
        var direction = Vector3.Normalize(cameraPosition - ZyphosUmbrosSystem.ZyphosCenter);
        var face = DominantFace(direction);
        if (Pages.TryGetValue(face, out var cached)) return cached;
        var request = new PlanetarySurfacePageRequest(new CubeTileKey(face, 0, 0, 0), 17, 2);
        var radius = ZyphosUmbrosSystem.ZyphosSurfaceRadius;
        var spacing = PlanetarySurfacePageSampling.NominalAngularTexelSize(request) * radius;
        var samples = new AquariumPlanetarySurfacePageInput[request.StorageSize * request.StorageSize];
        for (var y = 0; y < request.StorageSize; y++) for (var x = 0; x < request.StorageSize; x++)
        {
            var sampleDirection = PlanetarySurfacePageSampling.Direction(request, x, y);
            samples[y * request.StorageSize + x] = new AquariumPlanetarySurfacePageInput(new Vector4(sampleDirection, radius), new Vector4(spacing, 0, 0, 0));
        }
        var metadata = new AquariumPlanetarySurfacePageMetadata(
            new Vector4((int)face, 0, 0, 0),
            new Vector4(0, request.StorageSize, request.InteriorSize, request.BorderSize),
            Vector4.Zero,
            new Vector4(1, (int)face + 1, PlanetarySurfacePageSampling.AngularTexelSize(request), spacing));
        return Pages[face] = new AquariumPlanetarySurfacePageSet
        {
            GeneratorEntryPoint = "D3D12ZyphosTerrainPageCS",
            Version = (int)face + 1, Samples = samples, Metadata = metadata,
        };
    }

    private static CubeFace DominantFace(Vector3 d)
    {
        var a = Vector3.Abs(d);
        if (a.X >= a.Y && a.X >= a.Z) return d.X >= 0 ? CubeFace.PositiveX : CubeFace.NegativeX;
        if (a.Y >= a.Z) return d.Y >= 0 ? CubeFace.PositiveY : CubeFace.NegativeY;
        return d.Z >= 0 ? CubeFace.PositiveZ : CubeFace.NegativeZ;
    }
}
