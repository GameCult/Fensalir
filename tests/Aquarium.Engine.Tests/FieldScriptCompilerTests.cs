using Aquarium.Engine.Render;

namespace Aquarium.Engine.Tests;

public sealed class FieldScriptCompilerTests
{
    [Fact]
    public void CompileTreatsSplineFieldAsIntentWithAutoLoweringByDefault()
    {
        var frame = AquariumFieldScriptCompiler.Compile(
            """
            texture id=spectrum
            surface id=amplitude op=texture.sample value=0,0,0,0
            splinefield id=field texture=spectrum frequencyAxis=x columns=4
            """,
            new Dictionary<string, AquariumTextureFieldBinding>(StringComparer.Ordinal)
            {
                ["spectrum"] = new("spectrum", 8, 4, 1, 0, AquariumRollingModuloMode.Rows, Enumerable.Repeat(0.5f, 32).ToArray()),
            });

        Assert.Equal(AquariumFieldLoweringMode.Auto, frame.LoweringPolicy.Mode);
        Assert.True(frame.Reservoir.HasInput);
        Assert.False(frame.UseReservoirLowering);
        Assert.Single(frame.TextureSplineFields);
    }

    [Fact]
    public void CompileParsesLoweringPolicyWithoutBackendCommands()
    {
        var frame = AquariumFieldScriptCompiler.Compile(
            """
            texture id=spectrum
            splinefield id=field texture=spectrum frequencyAxis=x columns=8
            lowering mode=reservoirSplats maxSplines=2 maxControlPoints=16 maxSplats=4096 lodBias=0.5
            """,
            new Dictionary<string, AquariumTextureFieldBinding>(StringComparer.Ordinal)
            {
                ["spectrum"] = new("spectrum", 8, 8, 1, 0, AquariumRollingModuloMode.Rows, Enumerable.Repeat(0.5f, 64).ToArray()),
            });

        Assert.Equal(AquariumFieldLoweringMode.ReservoirSplats, frame.LoweringPolicy.Mode);
        Assert.Equal(2, frame.LoweringPolicy.MaxDirectSplines);
        Assert.Equal(16, frame.LoweringPolicy.MaxDirectControlPoints);
        Assert.Equal(4096, frame.LoweringPolicy.MaxReservoirSplats);
        Assert.Equal(0.5f, frame.LoweringPolicy.LodBias);
        Assert.True(frame.UseReservoirLowering);
    }
}
