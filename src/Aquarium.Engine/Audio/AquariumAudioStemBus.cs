using System.Collections.Concurrent;

namespace Aquarium.Engine.Audio;

internal sealed class AquariumAudioStemBus : IAquariumAudioStemBus
{
    private readonly ConcurrentQueue<AquariumAudioStemFrame> publishedFrames = new();
    private readonly ConcurrentDictionary<string, AquariumAudioStemFrame> latestFrames = new(StringComparer.Ordinal);

    public void Publish(AquariumAudioStemFrame frame)
    {
        if (string.IsNullOrWhiteSpace(frame.ProfileId) ||
            frame.FrameCount <= 0 ||
            frame.SampleRate <= 0 ||
            frame.Channels.Count == 0)
        {
            return;
        }

        publishedFrames.Enqueue(frame);
        latestFrames[frame.ProfileId] = frame;
    }

    public IReadOnlyList<AquariumAudioStemFrame> DrainPublishedFrames(int maxFrames = 64)
    {
        var drained = new List<AquariumAudioStemFrame>();
        var count = Math.Max(0, maxFrames);
        while (drained.Count < count && publishedFrames.TryDequeue(out var frame))
        {
            drained.Add(frame);
        }

        return drained;
    }

    public AquariumAudioStemFrame? LatestFrame(string profileId)
    {
        return latestFrames.TryGetValue(profileId, out var frame) ? frame : null;
    }
}
