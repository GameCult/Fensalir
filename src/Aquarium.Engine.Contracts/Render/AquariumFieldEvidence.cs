using System.Numerics;

namespace Aquarium.Engine.Render;

public enum AquariumFieldDomainKind
{
    Unknown = 0,
    Surface2D = 1,
    Object3D = 2,
    Volume3D = 3,
    SensorRig = 4,
    CameraSensor = 5,
    AudioPath = 6,
    RollingBuffer = 7,
    CubeTile = 8,
    TorusSurface = 9,
}

public enum AquariumFieldLayer
{
    Unknown = 0,
    Form = 1,
    Appearance = 2,
    Transport = 3,
}

public enum AquariumFieldEncoding
{
    Unknown = 0,
    Height = 1,
    Sdf2D = 2,
    Sdf3D = 3,
    Density = 4,
    Extinction = 5,
    Material = 6,
    Phase = 7,
    Emission = 8,
    Radiance = 9,
    Feature = 10,
    Confidence = 11,
    Tube = 12,
    Mesh = 13,
}

public enum AquariumFieldProposalKind
{
    Unknown = 0,
    DeterministicStructural = 1,
    StochasticSample = 2,
    SensorObservation = 3,
    CalibrationConstraint = 4,
    DebugIntent = 5,
}

public enum AquariumFieldBackendKind
{
    Unknown = 0,
    DirectSdf = 1,
    Mesh = 2,
    SurfaceSplat = 3,
    VolumeSplat = 4,
    SurfacePage = 5,
    TubeField = 6,
    DebugOverlay = 7,
}

public enum AquariumFieldInvalidationCode
{
    None = 0,
    Unknown = 1,
    DomainMismatch = 2,
    Disoccluded = 3,
    MaterialMismatch = 4,
    BoundsFailed = 5,
    CalibrationRejected = 6,
    Expired = 7,
}

public readonly record struct AquariumFieldDomain(
    string DomainKey,
    string ParentKey,
    AquariumFieldDomainKind Kind,
    Matrix4x4 LocalToParent,
    Matrix4x4 ParentToLocal,
    Vector3 BoundsMin,
    Vector3 BoundsMax,
    Vector3 Periodicity,
    string Owner)
{
    public bool HasIdentity => !string.IsNullOrWhiteSpace(DomainKey);

    public bool HasBounds =>
        BoundsMax.X >= BoundsMin.X &&
        BoundsMax.Y >= BoundsMin.Y &&
        BoundsMax.Z >= BoundsMin.Z;
}

public readonly record struct AquariumFieldSupport(
    Vector3 Center,
    Vector3 Radius,
    Matrix4x4 LocalFrame,
    float ConservativeRadius,
    float ProjectedError,
    float Curvature,
    float TemporalUncertainty)
{
    public bool HasSupport =>
        ConservativeRadius > 0.0f ||
        Radius.X > 0.0f ||
        Radius.Y > 0.0f ||
        Radius.Z > 0.0f;
}

public readonly record struct AquariumFieldProposalPolicy(
    AquariumFieldProposalKind Kind,
    float SourcePdf,
    float TargetContribution,
    int RepresentedCandidateCount,
    uint Seed)
{
    public bool IsValid =>
        Kind != AquariumFieldProposalKind.Unknown &&
        SourcePdf > 0.0f &&
        TargetContribution >= 0.0f &&
        RepresentedCandidateCount > 0;
}

public readonly record struct AquariumFieldGuide(
    float Confidence,
    float SampleAgeSeconds,
    float DomainValidity,
    AquariumFieldInvalidationCode InvalidationCode)
{
    public static AquariumFieldGuide Valid(float confidence, float sampleAgeSeconds = 0.0f) =>
        new(Math.Clamp(confidence, 0.0f, 1.0f), Math.Max(0.0f, sampleAgeSeconds), 1.0f, AquariumFieldInvalidationCode.None);

    public bool IsReusable =>
        Confidence > 0.0f &&
        DomainValidity > 0.0f &&
        InvalidationCode == AquariumFieldInvalidationCode.None;
}

public readonly record struct AquariumFieldClaim(
    string ClaimKey,
    string DomainKey,
    string ProducerKey,
    AquariumFieldLayer Layer,
    AquariumFieldEncoding Encoding,
    AquariumFieldSupport Support,
    AquariumFieldProposalPolicy Proposal,
    string PayloadHandle,
    long ObservedTimeNs,
    float Confidence)
{
    public bool HasIdentity =>
        !string.IsNullOrWhiteSpace(ClaimKey) &&
        !string.IsNullOrWhiteSpace(DomainKey);

    public bool HasEvidence =>
        HasIdentity &&
        Layer != AquariumFieldLayer.Unknown &&
        Encoding != AquariumFieldEncoding.Unknown &&
        Support.HasSupport &&
        Confidence > 0.0f;
}

public readonly record struct AquariumFieldCandidate(
    string CandidateKey,
    string ClaimKey,
    AquariumFieldLayer Layer,
    AquariumFieldEncoding Encoding,
    AquariumFieldProposalPolicy Proposal,
    AquariumFieldGuide Guide)
{
    public bool IsSelectable =>
        !string.IsNullOrWhiteSpace(CandidateKey) &&
        !string.IsNullOrWhiteSpace(ClaimKey) &&
        Proposal.IsValid &&
        Guide.IsReusable;
}

public readonly record struct AquariumFieldBackendPacket(
    string PacketKey,
    string ClaimKey,
    string DomainKey,
    AquariumFieldLayer Layer,
    AquariumFieldEncoding Encoding,
    AquariumFieldBackendKind Backend,
    AquariumFieldSupport Support,
    AquariumFieldGuide Guide,
    string PayloadHandle)
{
    public bool IsEvidenceWriter =>
        !string.IsNullOrWhiteSpace(PacketKey) &&
        !string.IsNullOrWhiteSpace(ClaimKey) &&
        !string.IsNullOrWhiteSpace(DomainKey) &&
        Layer != AquariumFieldLayer.Unknown &&
        Encoding != AquariumFieldEncoding.Unknown &&
        Backend != AquariumFieldBackendKind.Unknown &&
        Support.HasSupport;
}

public sealed class AquariumFieldEvidenceFrame
{
    public static AquariumFieldEvidenceFrame Empty { get; } = new();

    public IReadOnlyList<AquariumFieldDomain> Domains { get; init; } = [];

    public IReadOnlyList<AquariumFieldClaim> Claims { get; init; } = [];

    public IReadOnlyList<AquariumFieldCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<AquariumFieldBackendPacket> BackendPackets { get; init; } = [];

    public float AccumulationWindowSeconds { get; init; }

    public float PresentationDelaySeconds { get; init; }

    public bool HasInput =>
        Domains.Count > 0 ||
        Claims.Count > 0 ||
        Candidates.Count > 0 ||
        BackendPackets.Count > 0;
}
