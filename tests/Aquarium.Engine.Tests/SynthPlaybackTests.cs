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
    public void AquariumAudioDocumentDrainsStreamingAudioBlocks()
    {
        var document = new AquariumAudioDocument();
        document.EnqueueStreamingAudioBlock(new AquariumStreamingAudioBlock(
            "six-source-faust-fractional-delay",
            [
                new AquariumStreamingAudioChannel(0, "mic-0", [0.25f, 0.5f])
            ],
            FrameCount: 2,
            SampleRate: 48_000,
            Sequence: 3));

        var drained = document.DrainStreamingAudioBlocks();

        Assert.Single(drained);
        Assert.Equal(2, drained[0].FrameCount);
        Assert.Equal("mic-0", drained[0].Channels[0].SourceId);
        Assert.Empty(document.DrainStreamingAudioBlocks());
    }

    [Fact]
    public void AquariumAudioDocumentDrainsLoopbackCaptureRequests()
    {
        var document = new AquariumAudioDocument();
        document.EnqueueSystemLoopbackCapture(
            "perlines:system-loopback",
            "windows-default-render",
            "System mix",
            enabled: true,
            sequence: 7);

        var drained = document.DrainCaptureRequests();

        Assert.Single(drained);
        Assert.Equal(AquariumAudioCaptureKind.SystemLoopback, drained[0].Kind);
        Assert.Equal("perlines:system-loopback", drained[0].ProfileId);
        Assert.True(drained[0].Enabled);
        Assert.Empty(document.DrainCaptureRequests());
    }

    [Fact]
    public void StreamingDspHostProcessesControlDrivenInputBlocksWhenToolchainIsAvailable()
    {
        const string source = """
            import("stdfaust.lib");
            gain = hslider("source0/gain", 1.0, 0.0, 2.0, 0.001);
            process = _ * gain;
            """;
        using var host = new AquariumStreamingDspHost();
        var program = new AquariumStreamingDspProgram(
            "six-source-faust-fractional-delay",
            "mimir_alignment_smoke",
            source,
            Revision: 1);

        if (!host.UpsertProgram(program))
        {
            if (host.LastError?.Contains("Faust toolchain not found", StringComparison.OrdinalIgnoreCase) == true ||
                host.LastError?.Contains("Faust DLL not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            Assert.Fail($"Streaming DSP compile failed: {host.LastError}");
        }

        Assert.True(host.ApplyControls(new AquariumAudioControlFrame(
            program.ProfileId,
            "loopback-scarlett-speakers",
            0.0,
            [
                new AquariumAudioControlCommand(
                    "scarlett-host-mic",
                    0.0,
                    1.0,
                    1.0,
                    new Dictionary<string, float>
                    {
                        ["source0/gain"] = 0.5f
                    })
            ],
            TruncatedSourceCount: 0,
            Sequence: 1)));
        var input = new[] { Enumerable.Repeat(0.25f, 128).ToArray() };
        var output = new[] { new float[128] };

        Assert.True(host.ProcessBlock(program.ProfileId, input, output, 128));
        Assert.All(output[0], sample => Assert.InRange(sample, 0.124f, 0.126f));
    }

    [Fact]
    public void StreamingDspHostProcessesDeclaredAudioBlocksWhenToolchainIsAvailable()
    {
        const string source = """
            import("stdfaust.lib");
            gain = hslider("source0/gain", 1.0, 0.0, 2.0, 0.001);
            process = _ * gain;
            """;
        using var host = new AquariumStreamingDspHost();
        var program = new AquariumStreamingDspProgram(
            "six-source-faust-fractional-delay",
            "mimir_alignment_block_smoke",
            source,
            Revision: 1,
            OutputStems: [new AquariumStreamingDspOutputStem(0, "host_voice", "Host voice")]);

        if (!host.UpsertProgram(program))
        {
            if (host.LastError?.Contains("Faust toolchain not found", StringComparison.OrdinalIgnoreCase) == true ||
                host.LastError?.Contains("Faust DLL not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            Assert.Fail($"Streaming DSP compile failed: {host.LastError}");
        }

        Assert.True(host.ApplyControls(new AquariumAudioControlFrame(
            program.ProfileId,
            "loopback-scarlett-speakers",
            0.0,
            [
                new AquariumAudioControlCommand(
                    "scarlett-host-mic",
                    0.0,
                    1.0,
                    1.0,
                    new Dictionary<string, float>
                    {
                        ["source0/gain"] = 0.5f
                    })
            ],
            TruncatedSourceCount: 0,
            Sequence: 1)));

        Assert.True(host.ProcessBlock(new AquariumStreamingAudioBlock(
            program.ProfileId,
            [
                new AquariumStreamingAudioChannel(0, "scarlett-host-mic", Enumerable.Repeat(0.25f, 128).ToArray())
            ],
            FrameCount: 128,
            SampleRate: 48_000,
            Sequence: 2), out var stemFrame));
        Assert.Equal(program.ProfileId, stemFrame.ProfileId);
        Assert.Single(stemFrame.Channels);
        Assert.Equal("host_voice", stemFrame.Channels[0].StemId);
        Assert.Equal("Host voice", stemFrame.Channels[0].DisplayName);
        Assert.Equal("scarlett-host-mic", stemFrame.Channels[0].SourceId);
        Assert.All(stemFrame.Channels[0].Samples, sample => Assert.InRange(sample, 0.124f, 0.126f));
    }

    [Fact]
    public void AudioStemBusPublishesAndDrainsLatestFrames()
    {
        var bus = new AquariumAudioStemBus();
        var frame = new AquariumAudioStemFrame(
            "six-source-faust-fractional-delay",
            [new AquariumAudioStemChannel(0, "host_voice", "Host voice", "scarlett-host-mic", [0.1f, 0.2f])],
            FrameCount: 2,
            SampleRate: 48_000,
            Sequence: 4);

        bus.Publish(frame);

        Assert.Same(frame, bus.LatestFrame(frame.ProfileId));
        var drained = bus.DrainPublishedFrames();
        Assert.Single(drained);
        Assert.Equal("host_voice", drained[0].Channels[0].StemId);
        Assert.Empty(bus.DrainPublishedFrames());
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
