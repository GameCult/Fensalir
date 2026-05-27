using Aquarium.Engine.Render;

namespace Aquarium.Engine;

public interface IAquariumRuntimeServicesReceiver
{
    void AttachServices(AquariumRuntimeServices services);
}

public sealed class AquariumRuntimeServices(IAquariumFieldResourceBroker fieldResources)
{
    public static AquariumRuntimeServices Empty { get; } = new(AquariumNullFieldResourceBroker.Instance);

    public IAquariumFieldResourceBroker FieldResources { get; } = fieldResources;
}

internal sealed class AquariumNullFieldResourceBroker : IAquariumFieldResourceBroker
{
    public static AquariumNullFieldResourceBroker Instance { get; } = new();

    private AquariumNullFieldResourceBroker()
    {
    }

    public AquariumFieldResourceLease LeaseTexture2D(AquariumTexture2DLeaseRequest request) => AquariumFieldResourceLease.Invalid;

    public bool CommitLeaseVersion(string resourceKey, ulong version, ulong producerFenceValue) => false;
}
