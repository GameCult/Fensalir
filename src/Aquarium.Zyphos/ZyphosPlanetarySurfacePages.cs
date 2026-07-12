using System.Numerics;
using Aquarium.Engine.Fractal;
using Aquarium.Engine.Fractal.Lod;
using Aquarium.Engine.Render;
using CultMath;

namespace Aquarium.Zyphos;

internal static class ZyphosPlanetarySurfacePages
{
    private const int InteriorSize = 17;
    private const int BorderSize = 2;
    private const int MaximumLevel = 5;
    private const float ArrivalSeconds = 0.35f;
    private static readonly Dictionary<long, AquariumPlanetarySurfacePageInput[]> Content = [];
    private static readonly Dictionary<long, float> ArrivalTimes = [];
    private static readonly Dictionary<long, float> DepartureTimes = [];
    private static readonly Dictionary<long, CubeTileKey> ResidentTiles = [];
    private static float lastTimeSeconds = float.NegativeInfinity;

    internal static void Reset()
    {
        ArrivalTimes.Clear(); DepartureTimes.Clear(); ResidentTiles.Clear();
        lastTimeSeconds=float.NegativeInfinity;
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
        if (timeSeconds < lastTimeSeconds)
        {
            ArrivalTimes.Clear(); DepartureTimes.Clear(); ResidentTiles.Clear();
        }
        lastTimeSeconds = timeSeconds;
        var relative = cameraPosition - ZyphosUmbrosSystem.ZyphosCenter;
        var distance = Math.Max(relative.Length(), 0.001f);
        var direction = relative / distance;
        var desired = DesiredTiles(direction, distance).ToDictionary(ContentKey);
        foreach (var (key, tile) in desired)
        {
            ResidentTiles[key] = tile;
            if (!ArrivalTimes.TryGetValue(key, out var arrival)) ArrivalTimes[key] = arrival = timeSeconds;
            DepartureTimes.Remove(key);
        }
        foreach (var (key, tile) in ResidentTiles.ToArray())
        {
            if (tile.Level == 0 || desired.ContainsKey(key)) continue;
            if (!DepartureTimes.ContainsKey(key)) DepartureTimes[key] = timeSeconds;
            if (timeSeconds - DepartureTimes[key] < ArrivalSeconds) continue;
            ResidentTiles.Remove(key); ArrivalTimes.Remove(key); DepartureTimes.Remove(key);
        }

        var residents = ResidentTiles.OrderBy(pair => pair.Value.Level).ThenBy(pair => pair.Key).ToArray();
        var pages = new AquariumPlanetarySurfacePage[residents.Length];
        for (var index = 0; index < residents.Length; index++)
        {
            var (key, tile) = residents[index];
            var blend = tile.Level == 0 ? 1.0f : DepartureTimes.TryGetValue(key, out var departure)
                ? Math.Clamp(1.0f - (timeSeconds - departure) / ArrivalSeconds, 0.0f, 1.0f)
                : Math.Clamp((timeSeconds - ArrivalTimes[key]) / ArrivalSeconds, 0.0f, 1.0f);
            pages[index] = BuildPage(tile, key, blend);
        }

        var contentVersion = Version(residents.Select(pair => pair.Key));
        var presentationVersion = Version(pages.Select(page => (long)HashCode.Combine(page.ContentKey, BitConverter.SingleToInt32Bits(page.Metadata.State.Y))));
        return new AquariumPlanetarySurfacePageSet
        {
            GeneratorEntryPoint = "D3D12ZyphosTerrainPageCS",
            ContentVersion = contentVersion,
            PresentationVersion = presentationVersion,
            Pages = pages,
            RenderCoarsePatches = true,
            PatchCells = 64,
        };
    }

    private static AquariumPlanetarySurfacePage BuildPage(CubeTileKey tile, long key, float blend)
    {
        var request = new PlanetarySurfacePageRequest(tile, InteriorSize, BorderSize);
        var radius = ZyphosUmbrosSystem.ZyphosSurfaceRadius;
        var spacing = PlanetarySurfacePageSampling.NominalAngularTexelSize(request) * radius;
        var parentSpacing = tile.Level == 0
            ? 0.0f
            : PlanetarySurfacePageSampling.NominalAngularTexelSize(new PlanetarySurfacePageRequest(tile.Parent(), InteriorSize, BorderSize)) * radius;
        if (!Content.TryGetValue(key, out var samples))
        {
            samples = new AquariumPlanetarySurfacePageInput[request.StorageSize * request.StorageSize];
            for (var y = 0; y < request.StorageSize; y++) for (var x = 0; x < request.StorageSize; x++)
            {
                var sampleDirection = PlanetarySurfacePageSampling.Direction(request, x, y);
                samples[y * request.StorageSize + x] = new AquariumPlanetarySurfacePageInput(
                    new Vector4(sampleDirection, radius), new Vector4(spacing, parentSpacing, 0, 0));
            }
            Content[key] = samples;
        }

        return new AquariumPlanetarySurfacePage
        {
            ContentKey = key,
            Samples = samples,
            Metadata = new AquariumPlanetarySurfacePageMetadata(
                new Vector4((int)tile.Face, tile.Level, tile.X, tile.Y),
                new Vector4(0, request.StorageSize, request.InteriorSize, request.BorderSize),
                Vector4.Zero,
                new Vector4(1, blend, PlanetarySurfacePageSampling.NominalAngularTexelSize(request), spacing)),
        };
    }

    private static IEnumerable<CubeTileKey> DesiredTiles(Vector3 direction, float distance)
    {
        foreach (var rootFace in Enum.GetValues<CubeFace>()) yield return new CubeTileKey(rootFace, 0, 0, 0);
        var altitude = Math.Max(distance - ZyphosUmbrosSystem.ZyphosSurfaceRadius, 0.001f);
        var footprint = Math.Max(altitude / 720.0f * 2.0f, ZyphosUmbrosSystem.ZyphosSurfaceRadius / 65536.0f);
        var radius=ZyphosUmbrosSystem.ZyphosSurfaceRadius;
        var baseWavelength=radius*0.075f*0.7f;
        var baseAmplitude=0.12f*radius*0.075f;
        var targetLevel=MaximumLevel;
        for(var level=0;level<=MaximumLevel;level++)
        {
            var request=new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveX,level,0,0),InteriorSize,BorderSize);
            var spacing=PlanetarySurfacePageSampling.NominalAngularTexelSize(request)*radius;
            var unresolved=ErosionFrequencyBands.Select(baseWavelength,spacing,7,2.0f,baseAmplitude,0.5f).UnresolvedHeightBound;
            if(unresolved<=footprint){targetLevel=level;break;}
        }
        var (face, uv) = FaceUv(direction);
        for (var level = 1; level <= targetLevel; level++)
        {
            var count = 1 << level;
            var x = Math.Clamp((int)((uv.X * 0.5f + 0.5f) * count), 0, count - 1);
            var y = Math.Clamp((int)((uv.Y * 0.5f + 0.5f) * count), 0, count - 1);
            yield return new CubeTileKey(face, level, x, y);
        }
    }

    private static (CubeFace Face, Vector2 Uv) FaceUv(Vector3 d)
    {
        var a = Vector3.Abs(d);
        if (a.X >= a.Y && a.X >= a.Z) return d.X >= 0 ? (CubeFace.PositiveX, new(-d.Z / a.X, d.Y / a.X)) : (CubeFace.NegativeX, new(d.Z / a.X, d.Y / a.X));
        if (a.Y >= a.Z) return d.Y >= 0 ? (CubeFace.PositiveY, new(d.X / a.Y, -d.Z / a.Y)) : (CubeFace.NegativeY, new(d.X / a.Y, d.Z / a.Y));
        return d.Z >= 0 ? (CubeFace.PositiveZ, new(d.X / a.Z, d.Y / a.Z)) : (CubeFace.NegativeZ, new(-d.X / a.Z, d.Y / a.Z));
    }

    private static long ContentKey(CubeTileKey tile) => 1L + (long)tile.Face + ((long)tile.Level << 3) + ((long)tile.X << 9) + ((long)tile.Y << 29);

    private static long Version(IEnumerable<long> values)
    {
        const long offset = 1469598103934665603;
        const long prime = 1099511628211;
        var hash = offset;
        foreach (var value in values) hash = unchecked((hash ^ value) * prime);
        return hash == 0 ? 1 : hash;
    }
}
