using System.Collections.Concurrent;

namespace Aquarium.Engine.Audio;

public sealed class AquariumAudioDocument
{
    private readonly ConcurrentQueue<AquariumPcmAudioChunk> pcmChunks = new();
    private readonly ConcurrentQueue<AquariumAudioControlFrame> controlFrames = new();
    private readonly ConcurrentQueue<AquariumStreamingDspProgram> streamingDspPrograms = new();

    public static AquariumAudioDocument Empty { get; } = new();

    public void EnqueuePcm16Base64(string base64Data, int sampleRate, int channels, float gain = 1.0f, float pan = 0.0f)
    {
        if (string.IsNullOrWhiteSpace(base64Data) || sampleRate <= 0 || channels <= 0)
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return;
        }

        var frameCount = bytes.Length / Math.Max(1, channels) / 2;
        if (frameCount <= 0)
        {
            return;
        }

        var mono = new float[frameCount];
        var byteIndex = 0;
        var safeGain = Math.Clamp(gain, 0.0f, 4.0f);
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0.0f;
            for (var channel = 0; channel < channels && byteIndex + 1 < bytes.Length; channel++)
            {
                var sample = (short)(bytes[byteIndex] | (bytes[byteIndex + 1] << 8));
                sum += sample / 32768.0f;
                byteIndex += 2;
            }

            mono[frame] = Math.Clamp(sum / channels, -1.0f, 1.0f);
        }

        var safePan = Math.Clamp(pan, -1.0f, 1.0f);
        var leftGain = safeGain * MathF.Sqrt((1.0f - safePan) * 0.5f);
        var rightGain = safeGain * MathF.Sqrt((1.0f + safePan) * 0.5f);
        pcmChunks.Enqueue(new AquariumPcmAudioChunk(mono, sampleRate, leftGain, rightGain));
    }

    public IReadOnlyList<AquariumPcmAudioChunk> DrainPcmChunks(int maxChunks = 64)
    {
        var drained = new List<AquariumPcmAudioChunk>();
        while (drained.Count < maxChunks && pcmChunks.TryDequeue(out var chunk))
        {
            drained.Add(chunk);
        }

        return drained;
    }

    public void EnqueueControlFrame(AquariumAudioControlFrame frame)
    {
        if (string.IsNullOrWhiteSpace(frame.ProfileId) || frame.Commands.Count == 0)
        {
            return;
        }

        controlFrames.Enqueue(frame);
    }

    public IReadOnlyList<AquariumAudioControlFrame> DrainControlFrames(int maxFrames = 16)
    {
        var drained = new List<AquariumAudioControlFrame>();
        while (drained.Count < maxFrames && controlFrames.TryDequeue(out var frame))
        {
            drained.Add(frame);
        }

        return drained;
    }

    public void EnqueueStreamingDspProgram(AquariumStreamingDspProgram program)
    {
        if (string.IsNullOrWhiteSpace(program.ProfileId) ||
            string.IsNullOrWhiteSpace(program.FaustName) ||
            string.IsNullOrWhiteSpace(program.FaustSource))
        {
            return;
        }

        streamingDspPrograms.Enqueue(program);
    }

    public IReadOnlyList<AquariumStreamingDspProgram> DrainStreamingDspPrograms(int maxPrograms = 4)
    {
        var drained = new List<AquariumStreamingDspProgram>();
        while (drained.Count < maxPrograms && streamingDspPrograms.TryDequeue(out var program))
        {
            drained.Add(program);
        }

        return drained;
    }
}

public sealed record AquariumPcmAudioChunk(float[] MonoSamples, int SampleRate, float LeftGain = 1.0f, float RightGain = 1.0f);

public sealed record AquariumAudioControlFrame(
    string ProfileId,
    string ReferenceSourceId,
    double ReferenceHoldbackSamples,
    IReadOnlyList<AquariumAudioControlCommand> Commands,
    int TruncatedSourceCount,
    long Sequence);

public sealed record AquariumAudioControlCommand(
    string SourceId,
    double TargetDelaySamples,
    double ResampleRatio,
    double Confidence,
    IReadOnlyDictionary<string, float> Controls);

public sealed record AquariumStreamingDspProgram(
    string ProfileId,
    string FaustName,
    string FaustSource,
    int Revision,
    float ProbeDurationSeconds = 0.05f);
