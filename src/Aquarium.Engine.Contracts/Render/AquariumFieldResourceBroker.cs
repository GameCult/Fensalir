namespace Aquarium.Engine.Render;

public interface IAquariumFieldResourceBroker
{
    AquariumFieldResourceLease LeaseTexture2D(AquariumTexture2DLeaseRequest request);

    bool CommitLeaseVersion(string resourceKey, ulong version, ulong producerFenceValue);
}

public readonly record struct AquariumTexture2DLeaseRequest(
    string ResourceKey,
    int Width,
    int Height,
    string Format,
    AquariumFieldShaderAccess ProducerAccess,
    ulong Version = 0,
    long ValidFromNs = 0,
    long ValidUntilNs = 0)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ResourceKey) &&
        Width > 0 &&
        Height > 0 &&
        !string.IsNullOrWhiteSpace(Format) &&
        ProducerAccess is AquariumFieldShaderAccess.ShaderResource or AquariumFieldShaderAccess.UnorderedAccess;
}

public readonly record struct AquariumFieldResourceLease(
    AquariumFieldResourceDeclaration Declaration,
    IntPtr NativeHandle,
    string NativeHandleKind,
    IntPtr ProducerFenceHandle,
    ulong Version,
    bool IsValid)
{
    public static AquariumFieldResourceLease Invalid { get; } = new(
        default,
        IntPtr.Zero,
        "",
        IntPtr.Zero,
        0,
        false);
}
