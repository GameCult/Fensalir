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

    [Fact]
    public void CompileEvidenceParsesTubeSplineLoweringForRollingFloatBuffers()
    {
        var resource = new AquariumFieldResourceDeclaration(
            "mimir:resource:log-mel",
            AquariumFieldResourceKind.StructuredBuffer,
            AquariumFieldResourceResidency.SharedGpu,
            AquariumFieldShaderAccess.ShaderResource,
            "Float32",
            Width: 64,
            Height: 32,
            DepthOrCount: 2048,
            StrideBytes: 4,
            ValidFromNs: 0,
            ValidUntilNs: 10_000,
            Version: 7,
            NativeHandle: IntPtr.Zero,
            NativeHandleKind: "fensalir-buffer");

        var frame = AquariumFieldScriptCompiler.CompileEvidence(
            """
            resource id=mel key=mimir:resource:log-mel
            tubespline id=mel-field resource=mel domain=mimir:log-mel width=64 height=32 stride=4 firstColumn=3 columns=8 columnStride=1 rollingModulo=32 rollingOffset=12 amplitudePower=2 normalizeMin=0 normalizeMax=1 radius=0.01 radiusScale=0.03 ramp=D:\WIP4\Projects\Aetheria\Assets\Resources\Ramps\blackbody.png emissionScale=10 subdivisions=4
            """,
            new Dictionary<string, AquariumFieldResourceDeclaration>(StringComparer.Ordinal)
            {
                [resource.ResourceKey] = resource,
            });

        var lowering = Assert.Single(frame.TubeSplineLowerings);
        Assert.Equal("dsl:tube:mel-field", lowering.ClaimKey);
        Assert.Equal(resource.ResourceKey, lowering.ResourceKey);
        Assert.Equal(64, lowering.Width);
        Assert.Equal(32, lowering.Height);
        Assert.Equal(12, lowering.RollingOffset);
        Assert.Equal(2.0f, lowering.AmplitudePower);
        Assert.Equal(10.0f, lowering.EmissionScale);
        Assert.Equal(@"D:\WIP4\Projects\Aetheria\Assets\Resources\Ramps\blackbody.png", lowering.RampTexturePath);
    }
}
