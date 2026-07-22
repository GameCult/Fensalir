using System.Numerics;
using Aquarium.Engine.Render;
using CultMath;
using GameCult.Geometry;

namespace Aquarium.Zyphos;

internal static class ZyphosPlanetarySurfacePages
{
    private const int InteriorSize = 17;
    private const int BorderSize = 2;
    private const int MaximumLevel = 5;
    private const float ArrivalSeconds = 0.35f;
    private static readonly Dictionary<ulong, CachedPageContent> Content = [];
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
        var layout = new PlanetaryPageLayout(tile, InteriorSize, BorderSize);
        var key = tile.StableKey;
        var radius = ZyphosUmbrosSystem.ZyphosSurfaceRadius;
        if (!Content.TryGetValue(key, out var cached))
        {
            var common = PlanetaryGpuPageBuilder.BuildContent(layout, radius);
            var samples = common.Inputs.Select(input => new AquariumPlanetarySurfacePageInput(
                ToNumerics(input.DirectionRadius), ToNumerics(input.Sampling))).ToArray();
            cached = new(common, samples);
            Content[key] = cached;
        }

        var metadata = PlanetaryGpuPageBuilder.Metadata(cached.Common, 0, blend);

        return new AquariumPlanetarySurfacePage
        {
            ContentKey = unchecked((long)key),
            Samples = cached.Samples,
            Metadata = new AquariumPlanetarySurfacePageMetadata(
                ToNumerics(metadata.Address),
                ToNumerics(metadata.Layout),
                ToNumerics(metadata.Bounds),
                ToNumerics(metadata.State)),
        };
    }

    private static Vector4 ToNumerics(float4 value) => new(value.x, value.y, value.z, value.w);

    private sealed record CachedPageContent(
        PlanetaryGpuPageContent Common,
        AquariumPlanetarySurfacePageInput[] Samples);

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
