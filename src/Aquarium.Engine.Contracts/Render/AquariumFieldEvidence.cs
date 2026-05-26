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

public enum AquariumFieldEvidenceIssueSeverity
{
    Warning = 0,
    Error = 1,
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

public readonly record struct AquariumFieldEvidenceIssue(
    AquariumFieldEvidenceIssueSeverity Severity,
    string Key,
    string Message);

public readonly record struct AquariumFieldLoweringRequest(
    string RequestKey,
    string ClaimKey,
    string DomainKey,
    AquariumFieldLayer Layer,
    AquariumFieldEncoding Encoding,
    AquariumFieldSupport Support,
    AquariumFieldGuide Guide,
    string PayloadHandle)
{
    public bool IsPendingBackendSelection =>
        !string.IsNullOrWhiteSpace(RequestKey) &&
        !string.IsNullOrWhiteSpace(ClaimKey) &&
        !string.IsNullOrWhiteSpace(DomainKey) &&
        Layer != AquariumFieldLayer.Unknown &&
        Encoding != AquariumFieldEncoding.Unknown &&
        Support.HasSupport &&
        Guide.IsReusable;
}

public sealed class AquariumFieldEvidenceValidationReport
{
    public static AquariumFieldEvidenceValidationReport Empty { get; } = new([]);

    public AquariumFieldEvidenceValidationReport(IReadOnlyList<AquariumFieldEvidenceIssue> issues)
    {
        Issues = issues;
    }

    public IReadOnlyList<AquariumFieldEvidenceIssue> Issues { get; }

    public bool HasErrors => Issues.Any(static issue => issue.Severity == AquariumFieldEvidenceIssueSeverity.Error);
}

public static class AquariumFieldEvidenceValidator
{
    public static AquariumFieldEvidenceValidationReport Validate(AquariumFieldEvidenceFrame frame)
    {
        if (!frame.HasInput)
        {
            return AquariumFieldEvidenceValidationReport.Empty;
        }

        var issues = new List<AquariumFieldEvidenceIssue>();
        var domainKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var domain in frame.Domains)
        {
            if (!domain.HasIdentity)
            {
                issues.Add(Error("domain", "Field domain is missing a stable key."));
                continue;
            }

            if (!domain.HasBounds)
            {
                issues.Add(Error(domain.DomainKey, "Field domain bounds are inverted."));
            }

            domainKeys.Add(domain.DomainKey);
        }

        var claimKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in frame.Claims)
        {
            if (!claim.HasIdentity)
            {
                issues.Add(Error("claim", "Field claim is missing a stable claim/domain key."));
                continue;
            }

            if (!domainKeys.Contains(claim.DomainKey))
            {
                issues.Add(Error(claim.ClaimKey, $"Field claim references unknown domain '{claim.DomainKey}'."));
            }

            if (!claim.HasEvidence)
            {
                issues.Add(Error(claim.ClaimKey, "Field claim is missing layer, encoding, support, or confidence."));
            }

            if (!claim.Proposal.IsValid)
            {
                issues.Add(Error(claim.ClaimKey, "Field claim has an invalid proposal policy."));
            }

            claimKeys.Add(claim.ClaimKey);
        }

        foreach (var candidate in frame.Candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.CandidateKey))
            {
                issues.Add(Error("candidate", "Field candidate is missing a stable key."));
                continue;
            }

            if (!claimKeys.Contains(candidate.ClaimKey))
            {
                issues.Add(Error(candidate.CandidateKey, $"Field candidate references unknown claim '{candidate.ClaimKey}'."));
            }

            if (!candidate.IsSelectable)
            {
                issues.Add(Error(candidate.CandidateKey, "Field candidate is not selectable by proposal and guide."));
            }
        }

        foreach (var packet in frame.BackendPackets)
        {
            if (string.IsNullOrWhiteSpace(packet.PacketKey))
            {
                issues.Add(Error("packet", "Field backend packet is missing a stable key."));
                continue;
            }

            if (!claimKeys.Contains(packet.ClaimKey))
            {
                issues.Add(Error(packet.PacketKey, $"Field backend packet references unknown claim '{packet.ClaimKey}'."));
            }

            if (!domainKeys.Contains(packet.DomainKey))
            {
                issues.Add(Error(packet.PacketKey, $"Field backend packet references unknown domain '{packet.DomainKey}'."));
            }

            if (!packet.IsEvidenceWriter)
            {
                issues.Add(Error(packet.PacketKey, "Field backend packet cannot write reusable evidence."));
            }

            if (!packet.Guide.IsReusable)
            {
                issues.Add(Warning(packet.PacketKey, "Field backend packet guide marks the packet as non-reusable."));
            }
        }

        return issues.Count == 0
            ? AquariumFieldEvidenceValidationReport.Empty
            : new AquariumFieldEvidenceValidationReport(issues);
    }

    private static AquariumFieldEvidenceIssue Error(string key, string message) =>
        new(AquariumFieldEvidenceIssueSeverity.Error, key, message);

    private static AquariumFieldEvidenceIssue Warning(string key, string message) =>
        new(AquariumFieldEvidenceIssueSeverity.Warning, key, message);
}

public static class AquariumFieldEvidenceNormalizer
{
    public static IReadOnlyList<AquariumFieldLoweringRequest> BuildLoweringRequests(AquariumFieldEvidenceFrame frame)
    {
        var validation = AquariumFieldEvidenceValidator.Validate(frame);
        if (validation.HasErrors)
        {
            return [];
        }

        var claims = new Dictionary<string, AquariumFieldClaim>(StringComparer.Ordinal);
        foreach (var claim in frame.Claims)
        {
            claims[claim.ClaimKey] = claim;
        }

        var requests = new List<AquariumFieldLoweringRequest>();
        foreach (var candidate in frame.Candidates)
        {
            if (!candidate.IsSelectable || !claims.TryGetValue(candidate.ClaimKey, out var claim))
            {
                continue;
            }

            requests.Add(new AquariumFieldLoweringRequest(
                RequestKey: $"lower:{candidate.CandidateKey}",
                ClaimKey: claim.ClaimKey,
                DomainKey: claim.DomainKey,
                Layer: claim.Layer,
                Encoding: claim.Encoding,
                Support: claim.Support,
                Guide: candidate.Guide,
                PayloadHandle: claim.PayloadHandle));
        }

        return requests;
    }
}
