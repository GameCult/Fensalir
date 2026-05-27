using AquaSynth.Faust;

namespace Aquarium.Engine.Audio;

internal sealed class AquariumStreamingDspHost : IDisposable
{
    private readonly AquaSynthPatchCompiler compiler;
    private readonly Dictionary<string, StreamingProgramRuntime> programs = new(StringComparer.Ordinal);

    public AquariumStreamingDspHost(AquaSynthNativeOptions? options = null)
    {
        compiler = new AquaSynthPatchCompiler(options);
    }

    public IReadOnlyList<string> ProgramIds => programs.Keys.Order(StringComparer.Ordinal).ToArray();

    public string? LastError { get; private set; }

    public bool UpsertProgram(AquariumStreamingDspProgram program)
    {
        if (program.ProbeDurationSeconds <= 0.0f)
        {
            LastError = $"Streaming DSP program `{program.ProfileId}` has invalid probe duration.";
            return false;
        }

        if (programs.TryGetValue(program.ProfileId, out var existing) &&
            existing.Revision == program.Revision)
        {
            return true;
        }

        if (!compiler.TryCompileSource(
            new AquaSynthCompileIdentity(program.ProfileId, program.FaustName, program.FaustSource, program.Revision),
            program.FaustSource,
            program.ProbeDurationSeconds,
            out var patch,
            out var error))
        {
            LastError = error;
            return false;
        }

        var stream = patch!.CreateStreamingPatch();
        if (programs.Remove(program.ProfileId, out var old))
        {
            old.Dispose();
        }

        programs[program.ProfileId] = new StreamingProgramRuntime(program.Revision, patch, stream, program.OutputStems ?? []);
        LastError = null;
        return true;
    }

    public bool ApplyControls(AquariumAudioControlFrame frame)
    {
        if (!programs.TryGetValue(frame.ProfileId, out var runtime))
        {
            LastError = $"Streaming DSP program `{frame.ProfileId}` is not loaded.";
            return false;
        }

        foreach (var command in frame.Commands)
        {
            runtime.Stream.SetControls(command.Controls);
        }

        LastError = null;
        return true;
    }

    public bool ProcessBlock(string profileId, float[][] inputs, float[][] outputs, int frameCount, IReadOnlyDictionary<string, float>? controls = null)
    {
        if (!programs.TryGetValue(profileId, out var runtime))
        {
            LastError = $"Streaming DSP program `{profileId}` is not loaded.";
            return false;
        }

        runtime.Stream.ProcessBlock(inputs, outputs, frameCount, controls);
        LastError = null;
        return true;
    }

    public bool ProcessBlock(AquariumStreamingAudioBlock block, out AquariumAudioStemFrame stemFrame)
    {
        stemFrame = new AquariumAudioStemFrame(block.ProfileId, [], block.FrameCount, block.SampleRate, block.Sequence);
        if (!programs.TryGetValue(block.ProfileId, out var runtime))
        {
            LastError = $"Streaming DSP program `{block.ProfileId}` is not loaded.";
            return false;
        }

        var inputCount = runtime.Stream.InputCount;
        var outputCount = runtime.Stream.OutputCount;
        var inputs = new float[inputCount][];
        for (var channel = 0; channel < inputCount; channel++)
        {
            inputs[channel] = new float[block.FrameCount];
        }

        foreach (var channel in block.Channels)
        {
            if (channel.ChannelIndex < 0 || channel.ChannelIndex >= inputCount)
            {
                continue;
            }

            Array.Copy(channel.Samples, 0, inputs[channel.ChannelIndex], 0, Math.Min(block.FrameCount, channel.Samples.Length));
        }

        var outputs = new float[outputCount][];
        for (var channel = 0; channel < outputCount; channel++)
        {
            outputs[channel] = new float[block.FrameCount];
        }

        runtime.Stream.ProcessBlock(inputs, outputs, block.FrameCount);
        stemFrame = new AquariumAudioStemFrame(
            block.ProfileId,
            BuildOutputChannels(runtime.OutputStems, outputs, block.Channels),
            block.FrameCount,
            block.SampleRate,
            block.Sequence);
        LastError = null;
        return true;
    }

    private static IReadOnlyList<AquariumAudioStemChannel> BuildOutputChannels(
        IReadOnlyList<AquariumStreamingDspOutputStem> declaredStems,
        float[][] outputs,
        IReadOnlyList<AquariumStreamingAudioChannel> inputs)
    {
        var channels = new List<AquariumAudioStemChannel>(outputs.Length);
        for (var outputIndex = 0; outputIndex < outputs.Length; outputIndex++)
        {
            var declaration = declaredStems.FirstOrDefault(stem => stem.ChannelIndex == outputIndex);
            var inputSource = inputs.FirstOrDefault(input => input.ChannelIndex == outputIndex)?.SourceId ?? "";
            var stemId = string.IsNullOrWhiteSpace(declaration?.StemId)
                ? $"output{outputIndex}"
                : declaration!.StemId;
            var displayName = string.IsNullOrWhiteSpace(declaration?.DisplayName)
                ? stemId
                : declaration!.DisplayName;
            var sourceId = string.IsNullOrWhiteSpace(declaration?.SourceId)
                ? inputSource
                : declaration!.SourceId;

            channels.Add(new AquariumAudioStemChannel(outputIndex, stemId, displayName, sourceId, outputs[outputIndex]));
        }

        return channels;
    }

    public void Dispose()
    {
        foreach (var runtime in programs.Values)
        {
            runtime.Dispose();
        }

        programs.Clear();
        compiler.Dispose();
    }

    private sealed class StreamingProgramRuntime(
        int revision,
        AquaSynthCompiledPatch patch,
        AquaSynthStreamingPatch stream,
        IReadOnlyList<AquariumStreamingDspOutputStem> outputStems)
        : IDisposable
    {
        public int Revision { get; } = revision;

        public AquaSynthCompiledPatch Patch { get; } = patch;

        public AquaSynthStreamingPatch Stream { get; } = stream;

        public IReadOnlyList<AquariumStreamingDspOutputStem> OutputStems { get; } = outputStems;

        public void Dispose()
        {
            Stream.Dispose();
            Patch.Dispose();
        }
    }
}
