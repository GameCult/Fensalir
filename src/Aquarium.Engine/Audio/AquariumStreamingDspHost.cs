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

        programs[program.ProfileId] = new StreamingProgramRuntime(program.Revision, patch, stream);
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
        AquaSynthStreamingPatch stream)
        : IDisposable
    {
        public int Revision { get; } = revision;

        public AquaSynthCompiledPatch Patch { get; } = patch;

        public AquaSynthStreamingPatch Stream { get; } = stream;

        public void Dispose()
        {
            Stream.Dispose();
            Patch.Dispose();
        }
    }
}
