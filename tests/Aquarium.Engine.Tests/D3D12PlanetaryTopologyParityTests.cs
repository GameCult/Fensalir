using System.Numerics;
using Aquarium.Engine.Render;
using CultMath;

namespace Aquarium.Engine.Tests;

public sealed class D3D12PlanetaryTopologyParityTests
{
    [Fact]
    public void QscFaceDirectionsAndInverseCoordinatesMatchCultMathCpu()
    {
        var cases = new List<TopologyInput>();
        foreach (var face in Enum.GetValues<PlanetaryCubeFace>())
        foreach (var u in new[] { -0.9f, -0.25f, 0.0f, 0.43f, 0.9f })
        foreach (var v in new[] { -0.85f, -0.1f, 0.31f, 0.88f })
        {
            cases.Add(new(new Vector4(0, 0, 0, (int)face), new Vector4(u, v, 0, 0)));
            var direction = (Vector3)PlanetaryTopology.Direction(new(face, u, v));
            cases.Add(new(new Vector4(direction, 0), new Vector4(0, 0, 1, 0)));
        }

        var output = D3D12ComputeProbe.Run<TopologyInput, TopologyOutput>(ShaderPath(), "D3D12PlanetaryTopologyParityCS", cases.ToArray());
        for (var i = 0; i < cases.Count; i += 2)
        {
            var directionCase = cases[i];
            var face = (PlanetaryCubeFace)(int)directionCase.DirectionFace.W;
            var expectedDirection = (Vector3)PlanetaryTopology.Direction(new(face, directionCase.CoordinateMode.X, directionCase.CoordinateMode.Y));
            Assert.InRange(Vector3.Distance(expectedDirection, new(output[i].DirectionFace.X, output[i].DirectionFace.Y, output[i].DirectionFace.Z)), 0, 2.0e-6f);
            Assert.Equal((float)face, output[i].DirectionFace.W);

            var expectedCoordinate = PlanetaryTopology.FaceCoordinate((float3)new Vector3(
                cases[i + 1].DirectionFace.X, cases[i + 1].DirectionFace.Y, cases[i + 1].DirectionFace.Z));
            Assert.Equal((float)expectedCoordinate.Face, output[i + 1].DirectionFace.W);
            // atan is a hardware transcendental; identity is bounded rather
            // than bit exact across CPU and GPU implementations.
            Assert.InRange(MathF.Abs((float)expectedCoordinate.U - output[i + 1].CoordinateValid.X), 0, 2.0e-5f);
            Assert.InRange(MathF.Abs((float)expectedCoordinate.V - output[i + 1].CoordinateValid.Y), 0, 2.0e-5f);
        }
    }

    [Theory]
    [InlineData(PlanetaryProjectionKind.Equirectangular, 2, 3)]
    [InlineData(PlanetaryProjectionKind.EqualEarth, 4, 5)]
    public void MapProjectionForwardAndInverseMatchCultMathCpu(PlanetaryProjectionKind kind, int forwardMode, int inverseMode)
    {
        var projection = new PlanetaryProjectionParameters(kind);
        var inputs = new List<TopologyInput>();
        var expectedDirections = new List<Vector3>();
        foreach (var longitude in new[] { -2.4, -0.7, 0.0, 0.9, 2.5 })
        foreach (var latitude in new[] { -1.1, -0.35, 0.2, 0.95 })
        {
            var cos = Math.Cos(latitude);
            var direction = new Vector3((float)(cos * Math.Cos(longitude)), (float)(cos * Math.Sin(longitude)), (float)Math.Sin(latitude));
            Assert.True(PlanetaryProjection.TryForward((float3)direction, projection, out var coordinate));
            inputs.Add(new(new Vector4(direction, 0), new Vector4(0, 0, forwardMode, 0)));
            inputs.Add(new(Vector4.Zero, new Vector4((float)coordinate.x, (float)coordinate.y, inverseMode, 0)));
            expectedDirections.Add(direction);
        }
        var output = D3D12ComputeProbe.Run<TopologyInput, TopologyOutput>(ShaderPath(), "D3D12PlanetaryTopologyParityCS", inputs.ToArray());
        for (var sample = 0; sample < expectedDirections.Count; sample++)
        {
            var forward = output[sample * 2].CoordinateValid;
            Assert.True(PlanetaryProjection.TryForward((float3)expectedDirections[sample], projection, out var expectedCoordinate));
            // The legacy SM5 implementation uses float transcendental
            // approximations while the CPU projection uses double precision.
            // The measured normalized-map bound stays below 0.0002.
            Assert.InRange(Math.Abs(expectedCoordinate.x - forward.X), 0, 2.0e-4);
            Assert.InRange(Math.Abs(expectedCoordinate.y - forward.Y), 0, 2.0e-4);
            var inverse = output[sample * 2 + 1].DirectionFace;
            Assert.InRange(Vector3.Distance(expectedDirections[sample], new(inverse.X, inverse.Y, inverse.Z)), 0, 3.0e-4f);
        }
    }

    [Theory]
    [InlineData(PlanetaryProjectionKind.Equirectangular)]
    [InlineData(PlanetaryProjectionKind.WebMercator)]
    [InlineData(PlanetaryProjectionKind.EqualEarth)]
    [InlineData(PlanetaryProjectionKind.Orthographic)]
    [InlineData(PlanetaryProjectionKind.AzimuthalEquidistant)]
    [InlineData(PlanetaryProjectionKind.AzimuthalEqualArea)]
    [InlineData(PlanetaryProjectionKind.CubeAtlas)]
    [InlineData(PlanetaryProjectionKind.LocalTangent)]
    public void PortableProjectionDispatcherMatchesEveryCpuProjection(PlanetaryProjectionKind kind)
    {
        var projection = new PlanetaryProjectionParameters(kind);
        var inputs = new List<TopologyInput>();
        var expected = new List<(Vector3 Direction, Vector2 Coordinate)>();
        foreach (var longitude in new[] { -1.2, -0.3, 0.2, 0.9 })
        foreach (var latitude in new[] { -0.7, -0.1, 0.45 })
        {
            var cos = Math.Cos(latitude);
            var direction = new Vector3((float)(cos * Math.Cos(longitude)), (float)(cos * Math.Sin(longitude)), (float)Math.Sin(latitude));
            if (!PlanetaryProjection.TryForward((float3)direction, projection, out var coordinate)) continue;
            var map = new Vector2((float)coordinate.x, (float)coordinate.y);
            inputs.Add(new(new Vector4(direction, 0), new Vector4(0, 0, 6, (int)kind)));
            inputs.Add(new(Vector4.Zero, new Vector4(map, 7, (int)kind)));
            expected.Add((direction, map));
        }
        var output = D3D12ComputeProbe.Run<TopologyInput, TopologyOutput>(ShaderPath(), "D3D12PlanetaryTopologyParityCS", inputs.ToArray());
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(1, output[i * 2].CoordinateValid.W);
            Assert.InRange(Vector2.Distance(expected[i].Coordinate, new(output[i * 2].CoordinateValid.X, output[i * 2].CoordinateValid.Y)), 0, 3.0e-4f);
            Assert.Equal(1, output[i * 2 + 1].CoordinateValid.W);
            var inverse = output[i * 2 + 1].DirectionFace;
            Assert.InRange(Vector3.Distance(expected[i].Direction, new(inverse.X, inverse.Y, inverse.Z)), 0, 5.0e-4f);
        }
    }

    [Theory]
    [InlineData(PlanetaryProjectionKind.Equirectangular)]
    [InlineData(PlanetaryProjectionKind.WebMercator)]
    [InlineData(PlanetaryProjectionKind.EqualEarth)]
    [InlineData(PlanetaryProjectionKind.Orthographic)]
    [InlineData(PlanetaryProjectionKind.AzimuthalEquidistant)]
    [InlineData(PlanetaryProjectionKind.AzimuthalEqualArea)]
    [InlineData(PlanetaryProjectionKind.CubeAtlas)]
    [InlineData(PlanetaryProjectionKind.LocalTangent)]
    public void ProjectionDispatcherHonorsCenterAndScale(PlanetaryProjectionKind kind)
    {
        var projection = new PlanetaryProjectionParameters(kind, 0.31, -0.22, 1.4);
        var parameters = new Vector4((float)projection.CenterLongitude, (float)projection.CenterLatitude, (float)projection.Scale, 0);
        var direction = Vector3.Normalize(new Vector3(0.91f, 0.23f, 0.34f));
        if (!PlanetaryProjection.TryForward((float3)direction, projection, out var coordinate)) return;
        var map = new Vector2((float)coordinate.x, (float)coordinate.y);
        var inputs = new[]
        {
            new TopologyInput(new Vector4(direction,0),new Vector4(0,0,6,(int)kind),parameters),
            new TopologyInput(Vector4.Zero,new Vector4(map,7,(int)kind),parameters),
        };
        var output = D3D12ComputeProbe.Run<TopologyInput, TopologyOutput>(ShaderPath(), "D3D12PlanetaryTopologyParityCS", inputs);
        Assert.Equal(1, output[0].CoordinateValid.W);
        Assert.InRange(Vector2.Distance(map, new(output[0].CoordinateValid.X, output[0].CoordinateValid.Y)), 0, 4.0e-4f);
        Assert.Equal(1, output[1].CoordinateValid.W);
        Assert.InRange(Vector3.Distance(direction, new(output[1].DirectionFace.X, output[1].DirectionFace.Y, output[1].DirectionFace.Z)), 0, 6.0e-4f);
    }

    private static string ShaderPath() => Path.Combine(RepositoryRoot(), "src", "Aquarium.Engine", "Render", "Shaders", "D3D12PlanetaryTopologyParity.hlsl");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Fensalir.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate Fensalir repository root.");
    }

    private readonly record struct TopologyInput(Vector4 DirectionFace, Vector4 CoordinateMode, Vector4 ProjectionParameters)
    {
        public TopologyInput(Vector4 directionFace, Vector4 coordinateMode)
            : this(directionFace, coordinateMode, new Vector4(0, 0, 1, 0))
        {
        }
    }
    private readonly record struct TopologyOutput(Vector4 DirectionFace, Vector4 CoordinateValid);
}
