using System.Numerics;
using System.Runtime.InteropServices;
using Aquarium.Engine.Render;
using CultMath;
using GameCult.Geometry;

namespace Aquarium.Engine.Tests;

public sealed class D3D12AdvancedErosionParityTests
{
    [Fact]
    public void AdvancedErosionMatchesGameCultGeometryCpuAcrossDeterministicCorpus()
    {
        if (!OperatingSystem.IsWindows()) return;
        var parameters = AdvancedErosionParameters.Default;
        var fixedCases = new[]
        {
            Case.Full(new float2(0.42f, 0.31f), new float3(0.5f, 0.1f, -0.2f), 0.0f, parameters),
            Case.Full(new float2(1.7f, -3.2f), new float3(-0.2f, 0.0f, 0.0f), -0.7f, parameters with { Normalization = 0.0f, Octaves = 1 }),
            Case.Full(new float2(50.5f, 77.25f), new float3(0.8f, -0.3f, 0.4f), 0.9f, parameters with { Normalization = 1.0f, Octaves = 8 }),
            Case.Full(new float2(-8.125f, 3.75f), float3.zero, 0.0f, parameters with { Rounding = float4.zero }),
        };
        var random = new System.Random(713);
        var cases = fixedCases.Concat(Enumerable.Range(0, 2048).Select(index =>
        {
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var p = parameters with { Octaves = 1 + index % 8, Normalization = index % 3 * 0.5f };
            return new Case(
                new float2(Next(-10, 10), Next(-10, 10)),
                new float3(Next(-1, 1), Next(-0.8f, 0.8f), Next(-0.8f, 0.8f)),
                Next(-1, 1),
                p,
                new ErosionBandSelection(p.Octaves, index % 2 == 0 ? 1.0f : 0.37f, 0.0f, 0.0f));
        })).ToArray();
        var gpuInputs = cases.Select(ParityInput.From).ToArray();
        var shader = Path.Combine(RepositoryRoot(), "src", "Aquarium.Engine", "Render", "Shaders", "D3D12AdvancedErosionParity.hlsl");
        var gpu = D3D12ComputeProbe.Run<ParityInput, ParityOutput>(shader, "D3D12AdvancedErosionParityCS", gpuInputs);

        for (var index = 0; index < cases.Length; index++)
        {
            var cpu = AdvancedErosionFilter.Sample(cases[index].Position, cases[index].Base, cases[index].Fade, cases[index].Parameters, cases[index].Band);
            AssertClose(cpu.Delta.x, gpu[index].DeltaMagnitude.X, index, "height", 2.0e-5f);
            AssertClose(cpu.Delta.y, gpu[index].DeltaMagnitude.Y, index, "dx", 2.0e-4f);
            AssertClose(cpu.Delta.z, gpu[index].DeltaMagnitude.Z, index, "dy", 2.0e-4f);
            AssertClose(cpu.Magnitude, gpu[index].DeltaMagnitude.W, index, "magnitude", 2.0e-6f);
            AssertClose(cpu.RidgeMap, gpu[index].RidgeFade.X, index, "ridge", 5.0e-4f);
            AssertClose(cpu.FadeTarget, gpu[index].RidgeFade.Y, index, "fade", 5.0e-4f);
        }
    }

    private static void AssertClose(float expected, float actual, int sample, string channel, float tolerance)
        => Assert.True(MathF.Abs(expected - actual) <= tolerance, $"sample {sample} {channel}: CPU={expected:R} GPU={actual:R} delta={MathF.Abs(expected-actual):R}");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fensalir.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Fensalir root not found.");
    }

    private readonly record struct Case(float2 Position, float3 Base, float Fade, AdvancedErosionParameters Parameters, ErosionBandSelection Band)
    {
        public static Case Full(float2 position, float3 @base, float fade, AdvancedErosionParameters p) => new(position, @base, fade, p, new ErosionBandSelection(p.Octaves, 1.0f, 0.0f, 0.0f));
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ParityInput(Vector4 PositionBase, Vector4 SlopeFade, Vector4 Scalar0, Vector4 Rounding, Vector4 Onset, Vector4 Scalar1, Vector4 Scalar2, Vector4 Scalar3)
    {
        public static ParityInput From(Case c) => new(
            new(c.Position.x, c.Position.y, c.Base.x, 0), new(c.Base.y, c.Base.z, c.Fade, 0),
            new(c.Parameters.Scale, c.Parameters.Strength, c.Parameters.GullyWeight, c.Parameters.Detail),
            (Vector4)c.Parameters.Rounding, (Vector4)c.Parameters.Onset,
            new(c.Parameters.AssumedSlope.x, c.Parameters.AssumedSlope.y, c.Parameters.CellScale, c.Parameters.Normalization),
            new(c.Parameters.Octaves, c.Parameters.Lacunarity, c.Parameters.Gain, 0),
            new(c.Band.ActiveOctaves, c.Band.FinalOctaveWeight, c.Band.FinestIncludedWavelength, c.Band.UnresolvedHeightBound));
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ParityOutput(Vector4 DeltaMagnitude, Vector4 RidgeFade);
}
