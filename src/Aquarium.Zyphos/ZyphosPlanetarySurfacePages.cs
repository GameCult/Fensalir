using System.Numerics;
using Aquarium.Engine.Render;
using CultMath;

namespace Aquarium.Zyphos;

internal static class ZyphosPlanetarySurfacePages
{
    private const int InteriorSize = 17;
    private const int BorderSize = 2;
    private const int MaximumLevel = 5;
    private const float ArrivalSeconds = 0.35f;
    private static readonly Dictionary<ulong, AquariumPlanetarySurfacePageInput[]> Content = [];
    private static readonly PlanetaryResidualResidency Residency = new();

    internal static void Reset()
    {
        Residency.Reset();
    }

    internal static void PrimeForCapture(Vector3 cameraPosition, float pageAgeSeconds)
    {
        Reset();
        _ = ForCamera(cameraPosition, -Math.Max(pageAgeSeconds, 0.0f));
    }

    internal static void PrimeEvictionForCapture(Vector3 cameraPosition, float pageAgeSeconds)
    {
        var center=ZyphosUmbrosSystem.ZyphosCenter;
        var relative=cameraPosition-center;
        var oldDirection=Vector3.Normalize(new Vector3(relative.Z,relative.Y,-relative.X));
        var oldCamera=center+oldDirection*Math.Max(relative.Length(),0.001f);
        Reset();
        _=ForCamera(oldCamera,-1.0f);
        _=ForCamera(oldCamera,-0.65f);
        _=ForCamera(cameraPosition,-Math.Max(pageAgeSeconds,0.0f));
    }

    public static AquariumPlanetarySurfacePageSet ForCamera(Vector3 cameraPosition, float timeSeconds)
    {
        var relative = cameraPosition - ZyphosUmbrosSystem.ZyphosCenter;
        var distance = Math.Max(relative.Length(), 0.001f);
        var direction = relative / distance;
        var field = FieldDefinition(ZyphosUmbrosSystem.ZyphosSurfaceRadius);
        var lod = new PlanetaryLodParameters(MaximumLevel, InteriorSize, BorderSize, 720, field.Radius / 65536);
        var desired = PlanetaryLodSelector.SelectAncestorChain(field, (float3)direction, distance, lod);
        var snapshot = Residency.Update(desired, timeSeconds, ArrivalSeconds);
        var pages = snapshot.Tiles.Select(resident => BuildPage(resident.Tile, resident.Blend)).ToArray();
        return new AquariumPlanetarySurfacePageSet
        {
            GeneratorEntryPoint = "D3D12ZyphosTerrainPageCS",
            ContentVersion = unchecked((long)snapshot.ContentVersion),
            PresentationVersion = unchecked((long)snapshot.PresentationVersion),
            Pages = pages,
            RenderCoarsePatches = true,
            PatchCells = 64,
        };
    }

    private static AquariumPlanetarySurfacePage BuildPage(PlanetaryTileAddress tile, float blend)
    {
        var request = new PlanetaryPageLayout(tile, InteriorSize, BorderSize);
        var key = tile.StableKey;
        var radius = ZyphosUmbrosSystem.ZyphosSurfaceRadius;
        var spacing = PlanetaryPageSampling.NominalAngularTexelSize(request) * radius;
        var parentSpacing = tile.Level == 0
            ? 0.0f
            : PlanetaryPageSampling.NominalAngularTexelSize(new PlanetaryPageLayout(tile.Parent(), InteriorSize, BorderSize)) * radius;
        if (!Content.TryGetValue(key, out var samples))
        {
            samples = new AquariumPlanetarySurfacePageInput[request.StorageSize * request.StorageSize];
            for (var y = 0; y < request.StorageSize; y++) for (var x = 0; x < request.StorageSize; x++)
            {
                var sampleDirection = (Vector3)PlanetaryPageSampling.Direction(request, x, y);
                samples[y * request.StorageSize + x] = new AquariumPlanetarySurfacePageInput(
                    new Vector4(sampleDirection, radius), new Vector4(spacing, parentSpacing, 0, 0));
            }
            Content[key] = samples;
        }

        return new AquariumPlanetarySurfacePage
        {
            ContentKey = unchecked((long)key),
            Samples = samples,
            Metadata = new AquariumPlanetarySurfacePageMetadata(
                new Vector4((int)tile.Face, tile.Level, tile.X, tile.Y),
                new Vector4(0, request.StorageSize, request.InteriorSize, request.BorderSize),
                Vector4.Zero,
                new Vector4(1, blend, PlanetaryPageSampling.NominalAngularTexelSize(request), spacing)),
        };
    }

    private static PlanetaryFieldDefinition FieldDefinition(float radius)
    {
        var erosion = new AdvancedErosionParameters(
            radius * 0.075f, 0.12f, 0.58f, 1.45f,
            new float4(0.1f, 0.015f, 0.1f, 2.0f),
            new float4(1.25f, 1.25f, 2.8f, 1.5f),
            new float2(0.7f, 0.85f), 0.7f, 0.5f, 7, 2.0f, 0.5f);
        return new(1, radius, 0, erosion);
    }
}
