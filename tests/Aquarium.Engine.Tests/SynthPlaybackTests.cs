using Aquarium.Engine.Audio;
using AquaSynth.Faust;

namespace Aquarium.Engine.Tests;

public sealed class SynthPlaybackTests
{
    [Fact]
    public void AquariumAudioDocumentDrainsAudioControlFrames()
    {
        var document = new AquariumAudioDocument();
        var frame = new AquariumAudioControlFrame(
            "six-source-faust-fractional-delay",
            "loopback-scarlett-speakers",
            120.0,
            [
                new AquariumAudioControlCommand(
                    "scarlett-host-mic",
                    7.5,
                    0.9999982,
                    0.82,
                    new Dictionary<string, float>
                    {
                        ["source0/delay_samples"] = 7.5f,
                        ["source0/gain"] = 1.0f
                    })
            ],
            TruncatedSourceCount: 0,
            Sequence: 12);

        document.EnqueueControlFrame(frame);
        var drained = document.DrainControlFrames();

        Assert.Single(drained);
        Assert.Equal("six-source-faust-fractional-delay", drained[0].ProfileId);
        Assert.Equal(120.0, drained[0].ReferenceHoldbackSamples);
        Assert.Equal("scarlett-host-mic", drained[0].Commands[0].SourceId);
        Assert.Empty(document.DrainControlFrames());
    }

    [Fact]
    public void AquaSynthPatchCompilerCanRenderAudiblePatchForEnginePlayback()
    {
        const string script = """
            voice
                wave=sine
                freq=440
                gain=0.2
                attack=0.001
                sustain=0.06
                decay=0.12
            """;

        using var compiler = new AquaSynthPatchCompiler();
        if (!compiler.TryCompileScript(new AquaSynthCompileIdentity("engine_synth_smoke", "engine_synth_smoke", script), out var patch, out var error))
        {
            if (error?.Contains("Faust toolchain not found", StringComparison.OrdinalIgnoreCase) == true ||
                error?.Contains("Faust DLL not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            Assert.Fail($"AquaSynth patch compiler failed to render a tiny patch: {error}");
        }

        using (patch)
        {
            var samples = patch!.Render(1.0f);
            Assert.True(samples.Length > 2048, $"Rendered too few samples: {samples.Length}.");
            Assert.Contains(samples, sample => MathF.Abs(sample) > 0.001f);
            Assert.InRange(samples.Max(sample => MathF.Abs(sample)), 0.001f, 1.0f);
        }
    }
}
