using System.Numerics;
using Aquarium.Engine.Render;

namespace Aquarium.Zyphos;

public static class ZyphosSceneBuilder
{
    public static AquariumSceneState Build(float timeSeconds, float previousTimeSeconds, ZyphosFractalRenderPlan fractalPlan)
        => Build(timeSeconds, previousTimeSeconds, ZyphosUmbrosSystem.ZyphosCenter + Vector3.UnitZ * 12.0f, fractalPlan);

    public static AquariumSceneState Build(float timeSeconds, float previousTimeSeconds, Vector3 cameraPosition, ZyphosFractalRenderPlan fractalPlan)
    {
        return new AquariumSceneState
        {
            TraceHeightFieldSurface = false,
            UseStarfieldBackground = true,
            HeightFieldBrushes = fractalPlan.HeightBrushes,
            FractalReservoirField = new AquariumFractalReservoirField
            {
                SplatCount = 2_000_000,
                SplatUpdatesPerFrame = 50_000,
                ReservoirUpdatesPerPass = 20_000,
                WorldCenterRadius = new Vector4(ZyphosUmbrosSystem.ZyphosCenter, ZyphosUmbrosSystem.ZyphosSurfaceRadius),
                PriorityFocus = fractalPlan.ReservoirPriorityFocus,
                ProgramTransforms = fractalPlan.GpuProgramTransforms,
            },
            PlanetarySurfacePages = Environment.GetEnvironmentVariable("AQUARIUM_ZYPHOS_DISABLE_PLANETARY_PAGES") == "1"
                ? AquariumPlanetarySurfacePageSet.Empty
                : ZyphosPlanetarySurfacePages.ForCamera(cameraPosition, timeSeconds),
            SdfObjects = BuildSdfObjects(timeSeconds, previousTimeSeconds),
            SdfLights = BuildSdfLights(timeSeconds),
        };
    }

    private static AquariumSdfObject[] BuildSdfObjects(float timeSeconds, float previousTimeSeconds)
    {
        var rotation = ZyphosUmbrosSystem.MutualPhase(timeSeconds);
        var previousRotation = ZyphosUmbrosSystem.MutualPhase(previousTimeSeconds);
        var umbrosCenter = ZyphosUmbrosSystem.UmbrosCenter(timeSeconds);
        var previousUmbrosCenter = ZyphosUmbrosSystem.UmbrosCenter(previousTimeSeconds);
        var starCenter = ZyphosUmbrosSystem.PrimaryStarCenter(timeSeconds);
        var previousStarCenter = ZyphosUmbrosSystem.PrimaryStarCenter(previousTimeSeconds);

        var objects = new AquariumSdfObject[ZyphosRenderPlan.SdfObjectCount];
        var planetBoundRadius = Environment.GetEnvironmentVariable("AQUARIUM_ZYPHOS_ENABLE_PLANET_SDF_ORACLE") == "1" ? ZyphosUmbrosSystem.ZyphosBoundRadius : 0.0f;
        objects[ZyphosRenderPlan.PlanetIndex] = new AquariumSdfObject(
            new Vector4(ZyphosUmbrosSystem.ZyphosCenter, planetBoundRadius),
            new Vector4(ZyphosUmbrosSystem.ZyphosCenter, previousRotation),
            new Vector4(ZyphosUmbrosSystem.ZyphosSurfaceRadius, rotation, ZyphosUmbrosSystem.SeaLevel, rotation));
        objects[ZyphosRenderPlan.UmbrosIndex] = new AquariumSdfObject(
            new Vector4(umbrosCenter, ZyphosUmbrosSystem.UmbrosBoundRadius),
            new Vector4(previousUmbrosCenter, 0.0f),
            new Vector4(ZyphosUmbrosSystem.UmbrosSurfaceRadius, rotation, 0.0f, 0.0f));
        objects[ZyphosRenderPlan.StarIndex] = new AquariumSdfObject(
            new Vector4(starCenter, ZyphosUmbrosSystem.PrimaryStarVisualRadius),
            new Vector4(previousStarCenter, 0.0f),
            new Vector4(ZyphosUmbrosSystem.PrimaryStarVisualRadius, rotation, 0.0f, 0.0f));

        return objects;
    }

    private static AquariumSdfLight[] BuildSdfLights(float timeSeconds)
    {
        var starCenter = ZyphosUmbrosSystem.PrimaryStarCenter(timeSeconds);
        return
        [
            new AquariumSdfLight(
                new Vector4(starCenter, ZyphosUmbrosSystem.PrimaryStarVisualRadius),
                new Vector4(3.2f, 2.5f, 1.8f, -100.0f)),
            new AquariumSdfLight(
                new Vector4(ZyphosUmbrosSystem.UmbrosCenter(timeSeconds), ZyphosUmbrosSystem.UmbrosSurfaceRadius),
                new Vector4(0.06f, 0.08f, 0.11f, -101.0f))
        ];
    }
}
