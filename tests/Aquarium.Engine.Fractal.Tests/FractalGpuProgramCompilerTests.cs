using Aquarium.Engine.Fractal.Grammar;
using Aquarium.Engine.Fractal.Lod;

namespace Aquarium.Engine.Fractal.Tests;

public sealed class FractalGpuProgramCompilerTests
{
    [Fact]
    public void CompilesSelectedClaimsIntoPackedGpuTransforms()
    {
        const string source = """
            domain Planetary zyphos -
            tile PositiveZ 2 1 1 zyphos/equator zyphos
            height ridge 0.10 -0.20 0.30 0.15 0.70 4.0 0.85 1.25 17 basalt
            ifs grove 2 2 0.00 0.00 0.20 0.10 0.50 1.2 0.4 3.0 0.9 0.4 91 canopy
            """;
        var tree = FractalDslCompiler.Compile(source);
        var summaries = FractalSummaryBuilder.Build(tree);
        var selected = FractalSelectedCutBuilder.Build(summaries, _ => 8.0f, 64.0f);

        var transforms = FractalGpuProgramCompiler.CompileSelectedTree(tree, selected, maxTransformCount: 8);

        Assert.NotEmpty(transforms);
        Assert.All(transforms, transform =>
        {
            Assert.True(transform.OffsetScaleAmplitude.Z > 0.0f);
            Assert.True(transform.RadiiRotationFalloff.X > 0.0f);
            Assert.True(transform.RadiiRotationFalloff.Y > 0.0f);
            Assert.True(transform.RadiiRotationFalloff.W > 0.0f);
            Assert.InRange(transform.MaterialSeedShape.X, 0.0f, 1.0f);
        });
        Assert.Contains(transforms, transform =>
            transform.TileAddress.X == (float)CubeFace.PositiveZ
            && transform.TileAddress.Y == 2.0f
            && transform.TileAddress.Z == 1.0f
            && transform.TileAddress.W == 1.0f);
    }

    [Fact]
    public void CompilesFieldPayloadEncodingIntoGpuTransformRows()
    {
        const string source = """
            tile PositiveZ 0 0 0 zyphos/field
            height ridge 0 0 3 3 0 4 1 0.2 17 ridge
            density smoke 1 1 2 2 0 4 1 0.8 19 smoke
            extinction ash -1 -1 2 2 0 4 1 0.4 23 ash
            """;
        var tree = FractalDslCompiler.Compile(source);
        var selected = FractalSelectedCutBuilder.Build(FractalSummaryBuilder.Build(tree), _ => 8.0f, 64.0f);

        var transforms = FractalGpuProgramCompiler.CompileSelectedTree(tree, selected, maxTransformCount: 8);

        Assert.Contains(transforms, transform => transform.PostTranslation.Z == (float)AquariumFieldEncoding.Height);
        Assert.Contains(transforms, transform => transform.PostTranslation.Z == (float)AquariumFieldEncoding.Density);
        Assert.Contains(transforms, transform => transform.PostTranslation.Z == (float)AquariumFieldEncoding.Extinction);
    }

    [Fact]
    public void HonorsTransformBudget()
    {
        const string source = """
            tile PositiveZ 2 1 1 zyphos/equator
            ifs grove 4 3 0.00 0.00 0.20 0.10 0.50 1.2 0.4 3.0 0.9 0.4 91 canopy
            """;
        var tree = FractalDslCompiler.Compile(source);
        var summaries = FractalSummaryBuilder.Build(tree);
        var selected = FractalSelectedCutBuilder.Build(summaries, _ => 8.0f, 64.0f);

        var transforms = FractalGpuProgramCompiler.CompileSelectedTree(tree, selected, maxTransformCount: 5);

        Assert.Equal(5, transforms.Length);
    }

    [Fact]
    public void CompilesFlameTransformsIntoPackedGpuProgramRows()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "Apophysis", "linear-spherical-bubble.flame");
        var flame = FractalFlameFileParser.ParseFirst(File.ReadAllText(flamePath), seed: 77);

        var transforms = FractalGpuProgramCompiler.CompileFlame2D(flame, maxTransformCount: 2);

        Assert.Equal(2, transforms.Length);
        Assert.Equal(flame.Transforms[0].Matrix, transforms[0].OffsetScaleAmplitude);
        Assert.Equal(flame.Transforms[0].Translation.X, transforms[0].RadiiRotationFalloff.X);
        Assert.Equal(flame.Transforms[0].Translation.Y, transforms[0].RadiiRotationFalloff.Y);
        Assert.Equal(flame.Transforms[0].Variations.Linear, transforms[0].RadiiRotationFalloff.Z);
        Assert.Equal(flame.Transforms[1].Variations.Spherical, transforms[1].RadiiRotationFalloff.W);
        Assert.Equal(flame.Transforms[1].Weight, transforms[1].MaterialSeedShape.Y);
        Assert.Equal(flame.Transforms[1].Color, transforms[1].MaterialSeedShape.Z);
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
