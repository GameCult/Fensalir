using System.Numerics;
using Aquarium.Engine.Fractal.Grammar;

namespace Aquarium.Engine.Fractal.Tests;

public sealed class FractalFlameIterationStateTests
{
    [Fact]
    public void PersistentIterationStateMatchesOneShotAdvance()
    {
        var flame = LoadJWildfireMinimalFlame();
        var initial = FractalFlameIterationState.Create(0xFACEu, sampleIndex: 17);

        var oneShot = FractalFlameIterationStepper.Advance(flame, initial, iterations: 64);
        var chunked = initial;
        for (var chunk = 0; chunk < 8; chunk++)
        {
            chunked = FractalFlameIterationStepper.Advance(flame, chunked, iterations: 8);
        }

        Assert.Equal(64, oneShot.Step);
        Assert.Equal(oneShot.Step, chunked.Step);
        Assert.Equal(oneShot.RandomState, chunked.RandomState);
        Assert.True(Vector2.Distance(oneShot.Point, chunked.Point) <= 0.000001f, $"Expected {oneShot.Point}, got {chunked.Point}.");
    }

    [Fact]
    public void PersistentIterationStateCarriesSampleIdentity()
    {
        var flame = LoadJWildfireMinimalFlame();
        var a = FractalFlameIterationStepper.Advance(flame, FractalFlameIterationState.Create(0xFACEu, sampleIndex: 17), iterations: 32);
        var b = FractalFlameIterationStepper.Advance(flame, FractalFlameIterationState.Create(0xFACEu, sampleIndex: 18), iterations: 32);

        Assert.NotEqual(a.RandomState, b.RandomState);
        Assert.True(Vector2.Distance(a.Point, b.Point) > 0.0001f);
    }

    private static FractalFlameDefinition LoadJWildfireMinimalFlame()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "JWildfire", "julian-disc-minimal.flame");
        return FractalFlameFileParser.ParseFirst(File.ReadAllText(flamePath), seed: 77);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fensalir.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find Aquarium repo root.");
    }
}
