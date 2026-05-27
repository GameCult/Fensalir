using Aquarium.Engine.Render;
using Aquarium.Engine.Audio;

namespace Aquarium.Engine;

public interface IAquariumRuntimeServicesReceiver
{
    void AttachServices(AquariumRuntimeServices services);
}

public sealed class AquariumRuntimeServices(
    IAquariumFieldResourceBroker fieldResources,
    IAquariumAudioStemBus? audioStems = null)
{
    public static AquariumRuntimeServices Empty { get; } = new(
        AquariumNullFieldResourceBroker.Instance,
        AquariumNullAudioStemBus.Instance);

    public IAquariumFieldResourceBroker FieldResources { get; } = fieldResources;

    public IAquariumAudioStemBus AudioStems { get; } = audioStems ?? AquariumNullAudioStemBus.Instance;
}

internal sealed class AquariumNullFieldResourceBroker : IAquariumFieldResourceBroker
{
    public static AquariumNullFieldResourceBroker Instance { get; } = new();

    private AquariumNullFieldResourceBroker()
    {
    }

    public AquariumFieldResourceLease LeaseTexture2D(AquariumTexture2DLeaseRequest request) => AquariumFieldResourceLease.Invalid;

    public bool CommitLeaseVersion(string resourceKey, ulong version, ulong producerFenceValue) => false;

    public bool UploadTexture2D(AquariumTexture2DUpload upload) => false;
}

internal sealed class AquariumNullAudioStemBus : IAquariumAudioStemBus
{
    public static AquariumNullAudioStemBus Instance { get; } = new();

    private AquariumNullAudioStemBus()
    {
    }

    public void Publish(AquariumAudioStemFrame frame)
    {
    }

    public IReadOnlyList<AquariumAudioStemFrame> DrainPublishedFrames(int maxFrames = 64) => [];

    public AquariumAudioStemFrame? LatestFrame(string profileId) => null;
}
