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
    GpuSensorFusion = 8,
}

public enum AquariumFieldResourceKind
{
    Unknown = 0,
    StructuredBuffer = 1,
    Texture2D = 2,
    Texture2DArray = 3,
    Mesh = 4,
    SurfacePage = 5,
    VolumeTexture = 6,
    CurvePointBuffer = 7,
    RollingTexture = 8,
}

public enum AquariumFieldResourceResidency
{
    Unknown = 0,
    CpuVisible = 1,
    GpuResident = 2,
    SharedGpu = 3,
}

public enum AquariumFieldShaderAccess
{
    Unknown = 0,
    ShaderResource = 1,
    UnorderedAccess = 2,
    VertexBuffer = 3,
    IndexBuffer = 4,
    IndirectArguments = 5,
    AccelerationStructure = 6,
}

public enum AquariumFieldMeshTopology
{
    Unknown = 0,
    TriangleList = 1,
    TriangleStrip = 2,
    LineList = 3,
    LineStrip = 4,
    PointList = 5,
}

public enum AquariumFieldMeshIndexFormat
{
    Unknown = 0,
    UInt16 = 1,
    UInt32 = 2,
}

public enum AquariumFieldMeshLayout
{
    Unknown = 0,
    PositionNormalUvColor = 1,
    PipelinePrivate = 2,
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
        float.IsFinite(SourcePdf) &&
        float.IsFinite(TargetContribution) &&
        SourcePdf > 0.0f &&
        TargetContribution > 0.0f &&
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
    AquariumFieldProposalPolicy Proposal,
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
        Proposal.IsValid &&
        Support.HasSupport;
}

public readonly record struct AquariumFieldMeshBuffer(
    string BufferKey,
    int Count,
    int StrideBytes,
    IntPtr NativeHandle,
    string NativeHandleKind)
{
    public bool HasShape => Count > 0 && StrideBytes > 0;
}

public readonly record struct AquariumFieldMeshResource(
    AquariumFieldMeshBuffer Vertices,
    AquariumFieldMeshBuffer Indices,
    AquariumFieldMeshTopology Topology,
    AquariumFieldMeshIndexFormat IndexFormat,
    AquariumFieldMeshLayout Layout,
    Vector3 BoundsMin,
    Vector3 BoundsMax,
    int SubmeshCount)
{
    public const int PositionNormalUvColorStrideBytes = 48;

    public bool IsValid =>
        Vertices.HasShape &&
        Indices.HasShape &&
        Topology != AquariumFieldMeshTopology.Unknown &&
        IndexFormat != AquariumFieldMeshIndexFormat.Unknown &&
        Layout != AquariumFieldMeshLayout.Unknown &&
        SubmeshCount > 0 &&
        BoundsMax.X >= BoundsMin.X &&
        BoundsMax.Y >= BoundsMin.Y &&
        BoundsMax.Z >= BoundsMin.Z;

    public bool IsStandardImportedLayout =>
        Layout == AquariumFieldMeshLayout.PositionNormalUvColor &&
        Vertices.StrideBytes == PositionNormalUvColorStrideBytes;

    public bool IsPipelinePrivate => Layout == AquariumFieldMeshLayout.PipelinePrivate;
}

public sealed class AquariumFieldResourceUpload
{
    public string ResourceKey { get; init; } = "";

    public ulong Version { get; init; }

    public int ElementOffset { get; init; }

    public IReadOnlyList<float> Float32Data { get; init; } = [];

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ResourceKey) &&
        ElementOffset >= 0 &&
        Float32Data.Count > 0;
}

public readonly record struct AquariumFieldTubeSplineLowering(
    string LoweringKey,
    string ClaimKey,
    string ResourceKey,
    int Width,
    int Height,
    int StrideBytes,
    int FirstColumn,
    int ColumnCount,
    int ColumnStride,
    int RollingModulo,
    int RollingOffset,
    Vector3 Origin,
    Vector3 AxisStep,
    Vector3 ColumnStep,
    float AmplitudePower,
    float AmplitudeScale,
    float NormalizeMin,
    float NormalizeMax,
    float BaseRadius,
    float RadiusScale,
    float Alpha,
    float Feather,
    string RampTexturePath,
    string RampResourceKey,
    float EmissionScale,
    int CatmullRomSubdivisions)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(LoweringKey) &&
        !string.IsNullOrWhiteSpace(ClaimKey) &&
        !string.IsNullOrWhiteSpace(ResourceKey) &&
        Width > 1 &&
        Height > 0 &&
        StrideBytes > 0 &&
        ColumnCount > 0 &&
        ColumnStride > 0 &&
        NormalizeMax > NormalizeMin &&
        BaseRadius > 0.0f &&
        RadiusScale >= 0.0f &&
        EmissionScale >= 0.0f &&
        CatmullRomSubdivisions > 0;

    public AquariumFieldTubeSplineLowering Normalized() => this with
    {
        Width = Math.Max(2, Width),
        Height = Math.Max(1, Height),
        StrideBytes = Math.Max(4, StrideBytes),
        FirstColumn = Math.Max(0, FirstColumn),
        ColumnCount = Math.Max(1, ColumnCount),
        ColumnStride = Math.Max(1, ColumnStride),
        RollingModulo = Math.Max(0, RollingModulo),
        AmplitudePower = MathF.Max(0.0001f, AmplitudePower),
        NormalizeMax = NormalizeMax <= NormalizeMin ? NormalizeMin + 1.0f : NormalizeMax,
        BaseRadius = MathF.Max(0.0001f, BaseRadius),
        RadiusScale = MathF.Max(0.0f, RadiusScale),
        Alpha = Math.Clamp(Alpha, 0.0f, 1.0f),
        Feather = MathF.Max(0.0001f, Feather),
        EmissionScale = MathF.Max(0.0f, EmissionScale),
        CatmullRomSubdivisions = Math.Clamp(CatmullRomSubdivisions, 1, 16),
    };
}

public readonly record struct AquariumFieldStereoDepthLowering(
    string LoweringKey,
    string ClaimKey,
    string ProfileKey,
    string CalibrationKey,
    string CameraPairKey,
    string LeftResourceKey,
    string RightResourceKey,
    string DisparityResourceKey,
    string ConfidenceResourceKey,
    int Width,
    int Height,
    int MinDisparity,
    int DisparityLevels,
    int AggregationPathCount,
    int CensusRadius,
    float SmoothnessPenaltySmall,
    float SmoothnessPenaltyLarge,
    float MinDepthMeters,
    float MaxDepthMeters)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(LoweringKey) &&
        !string.IsNullOrWhiteSpace(ClaimKey) &&
        !string.IsNullOrWhiteSpace(ProfileKey) &&
        !string.IsNullOrWhiteSpace(CalibrationKey) &&
        !string.IsNullOrWhiteSpace(CameraPairKey) &&
        !string.IsNullOrWhiteSpace(LeftResourceKey) &&
        !string.IsNullOrWhiteSpace(RightResourceKey) &&
        !string.IsNullOrWhiteSpace(DisparityResourceKey) &&
        Width > 0 &&
        Height > 0 &&
        MinDisparity >= 0 &&
        DisparityLevels > 0 &&
        AggregationPathCount > 0 &&
        CensusRadius > 0 &&
        SmoothnessPenaltySmall > 0.0f &&
        SmoothnessPenaltyLarge >= SmoothnessPenaltySmall &&
        MaxDepthMeters > MinDepthMeters;

    public AquariumFieldStereoDepthLowering Normalized() => this with
    {
        Width = Math.Max(1, Width),
        Height = Math.Max(1, Height),
        MinDisparity = Math.Max(0, MinDisparity),
        DisparityLevels = Math.Max(1, DisparityLevels),
        AggregationPathCount = Math.Max(1, AggregationPathCount),
        CensusRadius = Math.Clamp(CensusRadius, 1, 8),
        SmoothnessPenaltySmall = MathF.Max(0.0001f, SmoothnessPenaltySmall),
        SmoothnessPenaltyLarge = MathF.Max(MathF.Max(0.0001f, SmoothnessPenaltySmall), SmoothnessPenaltyLarge),
        MaxDepthMeters = MaxDepthMeters <= MinDepthMeters ? MinDepthMeters + 0.001f : MaxDepthMeters,
    };
}

public readonly record struct AquariumFieldResourceDeclaration(
    string ResourceKey,
    AquariumFieldResourceKind Kind,
    AquariumFieldResourceResidency Residency,
    AquariumFieldShaderAccess Access,
    string Format,
    int Width,
    int Height,
    int DepthOrCount,
    int StrideBytes,
    long ValidFromNs,
    long ValidUntilNs,
    ulong Version,
    IntPtr NativeHandle,
    string NativeHandleKind,
    string SourceUri = "",
    AquariumFieldMeshResource Mesh = default,
    IntPtr ProducerFenceHandle = default,
    ulong ProducerFenceValue = 0)
{
    public bool HasIdentity => !string.IsNullOrWhiteSpace(ResourceKey);

    public bool HasShape =>
        Kind != AquariumFieldResourceKind.Unknown &&
        Residency != AquariumFieldResourceResidency.Unknown &&
        Access != AquariumFieldShaderAccess.Unknown &&
        (Width > 0 || DepthOrCount > 0 || StrideBytes > 0 || HasSourceAsset || Mesh.IsValid);

    public bool HasSourceAsset => !string.IsNullOrWhiteSpace(SourceUri);

    public bool IsGpuVisible =>
        Residency is AquariumFieldResourceResidency.GpuResident or AquariumFieldResourceResidency.SharedGpu;

    public bool IsLiveAt(long timestampNs) =>
        timestampNs <= 0 ||
        ((ValidFromNs <= 0 || timestampNs >= ValidFromNs) &&
         (ValidUntilNs <= 0 || timestampNs <= ValidUntilNs));

    public static AquariumFieldResourceDeclaration LocalTexture2D(
        string resourceKey,
        string sourceUri,
        string format = "Rgba8Unorm",
        ulong version = 0,
        int width = 0,
        int height = 0,
        long validFromNs = 0,
        long validUntilNs = 0) =>
        new(
            ResourceKey: resourceKey,
            Kind: AquariumFieldResourceKind.Texture2D,
            Residency: AquariumFieldResourceResidency.GpuResident,
            Access: AquariumFieldShaderAccess.ShaderResource,
            Format: format,
            Width: width,
            Height: height,
            DepthOrCount: 1,
            StrideBytes: 4,
            ValidFromNs: validFromNs,
            ValidUntilNs: validUntilNs,
            Version: version,
            NativeHandle: IntPtr.Zero,
            NativeHandleKind: "local-asset",
            SourceUri: sourceUri);

    public static AquariumFieldResourceDeclaration SurfacePage(
        string resourceKey,
        int width,
        int height,
        string format = "R16Float",
        ulong version = 0,
        string sourceUri = "",
        long validFromNs = 0,
        long validUntilNs = 0) =>
        new(
            ResourceKey: resourceKey,
            Kind: AquariumFieldResourceKind.SurfacePage,
            Residency: AquariumFieldResourceResidency.GpuResident,
            Access: AquariumFieldShaderAccess.ShaderResource,
            Format: format,
            Width: Math.Max(1, width),
            Height: Math.Max(1, height),
            DepthOrCount: 1,
            StrideBytes: FormatStrideBytes(format),
            ValidFromNs: validFromNs,
            ValidUntilNs: validUntilNs,
            Version: version,
            NativeHandle: IntPtr.Zero,
            NativeHandleKind: string.IsNullOrWhiteSpace(sourceUri) ? "fensalir-surface-page" : "local-asset",
            SourceUri: sourceUri);

    public static AquariumFieldResourceDeclaration VolumeTexture(
        string resourceKey,
        int width,
        int height,
        int depth,
        string format = "R16Float",
        ulong version = 0,
        long validFromNs = 0,
        long validUntilNs = 0) =>
        new(
            ResourceKey: resourceKey,
            Kind: AquariumFieldResourceKind.VolumeTexture,
            Residency: AquariumFieldResourceResidency.GpuResident,
            Access: AquariumFieldShaderAccess.ShaderResource,
            Format: format,
            Width: Math.Max(1, width),
            Height: Math.Max(1, height),
            DepthOrCount: Math.Max(1, depth),
            StrideBytes: FormatStrideBytes(format),
            ValidFromNs: validFromNs,
            ValidUntilNs: validUntilNs,
            Version: version,
            NativeHandle: IntPtr.Zero,
            NativeHandleKind: "fensalir-volume-texture",
            SourceUri: "");

    public static AquariumFieldResourceDeclaration MeshPackage(
        string resourceKey,
        AquariumFieldMeshResource mesh,
        ulong version = 0,
        long validFromNs = 0,
        long validUntilNs = 0) =>
        new(
            ResourceKey: resourceKey,
            Kind: AquariumFieldResourceKind.Mesh,
            Residency: AquariumFieldResourceResidency.GpuResident,
            Access: AquariumFieldShaderAccess.ShaderResource,
            Format: "FieldMesh",
            Width: Math.Max(1, mesh.Vertices.Count),
            Height: Math.Max(1, mesh.SubmeshCount),
            DepthOrCount: Math.Max(1, mesh.Indices.Count),
            StrideBytes: Math.Max(1, mesh.Vertices.StrideBytes),
            ValidFromNs: validFromNs,
            ValidUntilNs: validUntilNs,
            Version: version,
            NativeHandle: IntPtr.Zero,
            NativeHandleKind: "fensalir-mesh-package",
            SourceUri: "",
            Mesh: mesh);

    public static int FormatStrideBytes(string format) =>
        format switch
        {
            "R8Unorm" or "R8_UNorm" or "R8_UNORM" => 1,
            "R16Float" or "R16_Float" or "R16_FLOAT" => 2,
            "R32Float" or "R32_Float" or "R32_FLOAT" or "Float32" => 4,
            "Rg8" or "Rg8Unorm" or "R8G8_UNorm" or "R8G8_UNORM" or "Yuy2" or "YUY2" or "LeapStereoIr" or "LeapPackedMap" => 2,
            "Bayer8" or "Gray8" or "R8" => 1,
            "Bgra8" or "Bgra8Unorm" or "B8G8R8A8_UNorm" or "B8G8R8A8_UNORM" or "Rgba8Unorm" or "R8G8B8A8_UNorm" or "R8G8B8A8_UNORM" => 4,
            "Rgba16Float" or "R16G16B16A16_Float" or "R16G16B16A16_FLOAT" => 8,
            "Nv12" or "NV12" => 1,
            _ => 4,
        };
}

public sealed class AquariumFieldEvidenceFrame
{
    public static AquariumFieldEvidenceFrame Empty { get; } = new();

    public IReadOnlyList<AquariumFieldDomain> Domains { get; init; } = [];

    public IReadOnlyList<AquariumFieldClaim> Claims { get; init; } = [];

    public IReadOnlyList<AquariumFieldCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<AquariumFieldBackendPacket> BackendPackets { get; init; } = [];

    public IReadOnlyList<AquariumFieldResourceDeclaration> Resources { get; init; } = [];

    public IReadOnlyList<AquariumFieldResourceUpload> ResourceUploads { get; init; } = [];

    public IReadOnlyList<AquariumFieldTubeSplineLowering> TubeSplineLowerings { get; init; } = [];

    public IReadOnlyList<AquariumFieldStereoDepthLowering> StereoDepthLowerings { get; init; } = [];

    public float AccumulationWindowSeconds { get; init; }

    public float PresentationDelaySeconds { get; init; }

    public bool HasInput =>
        Domains.Count > 0 ||
        Claims.Count > 0 ||
        Candidates.Count > 0 ||
        BackendPackets.Count > 0 ||
        Resources.Count > 0 ||
        ResourceUploads.Count > 0 ||
        TubeSplineLowerings.Count > 0 ||
        StereoDepthLowerings.Count > 0;
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
    AquariumFieldProposalPolicy Proposal,
    AquariumFieldSupport Support,
    AquariumFieldGuide Guide,
    string PayloadHandle)
{
    public AquariumFieldProposalKind ProposalKind => Proposal.Kind;

    public bool IsPendingBackendSelection =>
        !string.IsNullOrWhiteSpace(RequestKey) &&
        !string.IsNullOrWhiteSpace(ClaimKey) &&
        !string.IsNullOrWhiteSpace(DomainKey) &&
        Layer != AquariumFieldLayer.Unknown &&
        Encoding != AquariumFieldEncoding.Unknown &&
        Proposal.IsValid &&
        Support.HasSupport &&
        Guide.IsReusable;
}

public sealed class AquariumFieldLoweringPlan
{
    public static AquariumFieldLoweringPlan Empty { get; } = new([], []);

    public AquariumFieldLoweringPlan(
        IReadOnlyList<AquariumFieldBackendPacket> packets,
        IReadOnlyList<AquariumFieldLoweringRequest> deferredRequests)
    {
        Packets = packets;
        DeferredRequests = deferredRequests;
    }

    public IReadOnlyList<AquariumFieldBackendPacket> Packets { get; }

    public IReadOnlyList<AquariumFieldLoweringRequest> DeferredRequests { get; }

    public bool HasPackets => Packets.Count > 0;
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
        var claimsByKey = new Dictionary<string, AquariumFieldClaim>(StringComparer.Ordinal);
        var resourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var resourcesByKey = new Dictionary<string, AquariumFieldResourceDeclaration>(StringComparer.Ordinal);
        foreach (var resource in frame.Resources)
        {
            if (!resource.HasIdentity)
            {
                issues.Add(Error("resource", "Field resource declaration is missing a stable key."));
                continue;
            }

            if (!resource.HasShape)
            {
                issues.Add(Error(resource.ResourceKey, "Field resource declaration is missing kind, residency, access, or shape."));
            }

            if (resource.Kind == AquariumFieldResourceKind.Mesh && !resource.Mesh.IsValid)
            {
                issues.Add(Error(resource.ResourceKey, "Mesh field resource is missing vertex buffer, index buffer, topology, index format, bounds, or submesh count."));
            }

            if (!resource.IsGpuVisible)
            {
                issues.Add(Warning(resource.ResourceKey, "Field resource is not GPU-visible; shader lowering must import or upload before use."));
            }

            if (!resourceKeys.Add(resource.ResourceKey))
            {
                issues.Add(Error(resource.ResourceKey, "Field resource declarations must have unique keys."));
            }
            else
            {
                resourcesByKey[resource.ResourceKey] = resource;
            }
        }

        foreach (var upload in frame.ResourceUploads)
        {
            if (!upload.IsValid)
            {
                issues.Add(Error("resource-upload", "Field resource upload is missing a resource key or data."));
                continue;
            }

            if (!resourcesByKey.TryGetValue(upload.ResourceKey, out var resource))
            {
                issues.Add(Error(upload.ResourceKey, $"Field resource upload references unknown resource '{upload.ResourceKey}'."));
            }
            else
            {
                if (resource.Kind is not (AquariumFieldResourceKind.StructuredBuffer or AquariumFieldResourceKind.CurvePointBuffer) ||
                    resource.StrideBytes != sizeof(float) ||
                    !string.Equals(resource.Format, "Float32", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Error(upload.ResourceKey, "Float32 field resource uploads require a Float32 structured/curve buffer resource."));
                }

                var capacity = Math.Max(1, resource.DepthOrCount > 0 ? resource.DepthOrCount : resource.Width);
                if (upload.ElementOffset >= capacity ||
                    upload.Float32Data.Count > capacity - upload.ElementOffset)
                {
                    issues.Add(Error(upload.ResourceKey, $"Field resource upload writes {upload.Float32Data.Count} Float32 values at offset {upload.ElementOffset}, exceeding resource capacity {capacity}."));
                }
            }
        }

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

            if (LooksLikeResourceKey(claim.PayloadHandle) && !resourceKeys.Contains(claim.PayloadHandle))
            {
                issues.Add(Error(claim.ClaimKey, $"Field claim references unknown resource '{claim.PayloadHandle}'."));
            }

            claimKeys.Add(claim.ClaimKey);
            claimsByKey[claim.ClaimKey] = claim;
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

            if (LooksLikeResourceKey(packet.PayloadHandle) && !resourceKeys.Contains(packet.PayloadHandle))
            {
                issues.Add(Error(packet.PacketKey, $"Field backend packet references unknown resource '{packet.PayloadHandle}'."));
            }
        }

        foreach (var lowering in frame.TubeSplineLowerings)
        {
            if (!lowering.IsValid)
            {
                issues.Add(Error(string.IsNullOrWhiteSpace(lowering.LoweringKey) ? "tube-spline-lowering" : lowering.LoweringKey, "Tube spline lowering is missing identity, shape, normalization, or style."));
                continue;
            }

            if (!claimsByKey.TryGetValue(lowering.ClaimKey, out var claim))
            {
                issues.Add(Error(lowering.LoweringKey, $"Tube spline lowering references unknown claim '{lowering.ClaimKey}'."));
            }
            else
            {
                if (claim.Encoding != AquariumFieldEncoding.Tube)
                {
                    issues.Add(Error(lowering.LoweringKey, $"Tube spline lowering claim '{lowering.ClaimKey}' is encoded as {claim.Encoding}, not Tube."));
                }

                if (!string.Equals(claim.PayloadHandle, lowering.ResourceKey, StringComparison.Ordinal))
                {
                    issues.Add(Error(lowering.LoweringKey, $"Tube spline lowering resource '{lowering.ResourceKey}' does not match claim payload '{claim.PayloadHandle}'."));
                }
            }

            if (!resourceKeys.Contains(lowering.ResourceKey))
            {
                issues.Add(Error(lowering.LoweringKey, $"Tube spline lowering references unknown resource '{lowering.ResourceKey}'."));
            }

            if (!string.IsNullOrWhiteSpace(lowering.RampResourceKey) &&
                !resourceKeys.Contains(lowering.RampResourceKey))
            {
                issues.Add(Error(lowering.LoweringKey, $"Tube spline lowering references unknown ramp resource '{lowering.RampResourceKey}'."));
            }
        }

        foreach (var lowering in frame.StereoDepthLowerings)
        {
            if (!lowering.IsValid)
            {
                issues.Add(Error(string.IsNullOrWhiteSpace(lowering.LoweringKey) ? "stereo-depth-lowering" : lowering.LoweringKey, "Stereo depth lowering is missing identity, inputs, output, shape, profile, calibration, or depth range."));
                continue;
            }

            if (!claimsByKey.TryGetValue(lowering.ClaimKey, out var claim))
            {
                issues.Add(Error(lowering.LoweringKey, $"Stereo depth lowering references unknown claim '{lowering.ClaimKey}'."));
            }
            else
            {
                if (claim.Encoding != AquariumFieldEncoding.Height)
                {
                    issues.Add(Error(lowering.LoweringKey, $"Stereo depth lowering claim '{lowering.ClaimKey}' is encoded as {claim.Encoding}, not Height."));
                }

                if (!string.Equals(claim.PayloadHandle, lowering.DisparityResourceKey, StringComparison.Ordinal))
                {
                    issues.Add(Error(lowering.LoweringKey, $"Stereo depth disparity resource '{lowering.DisparityResourceKey}' does not match claim payload '{claim.PayloadHandle}'."));
                }
            }

            ValidateStereoDepthInputResource(issues, resourcesByKey, lowering, lowering.LeftResourceKey, "left");
            ValidateStereoDepthInputResource(issues, resourcesByKey, lowering, lowering.RightResourceKey, "right");

            if (!resourcesByKey.TryGetValue(lowering.DisparityResourceKey, out var disparity))
            {
                issues.Add(Error(lowering.LoweringKey, $"Stereo depth lowering references unknown disparity resource '{lowering.DisparityResourceKey}'."));
            }
            else
            {
                if (disparity.Kind != AquariumFieldResourceKind.SurfacePage)
                {
                    issues.Add(Error(lowering.LoweringKey, $"Stereo depth disparity resource '{lowering.DisparityResourceKey}' is {disparity.Kind}, not SurfacePage."));
                }

                if (disparity.Access != AquariumFieldShaderAccess.UnorderedAccess)
                {
                    issues.Add(Error(lowering.LoweringKey, $"Stereo depth disparity resource '{lowering.DisparityResourceKey}' must be compute-writable UnorderedAccess."));
                }
            }

            if (!string.IsNullOrWhiteSpace(lowering.ConfidenceResourceKey) &&
                !resourcesByKey.TryGetValue(lowering.ConfidenceResourceKey, out _))
            {
                issues.Add(Error(lowering.LoweringKey, $"Stereo depth lowering references unknown confidence resource '{lowering.ConfidenceResourceKey}'."));
            }
        }

        return issues.Count == 0
            ? AquariumFieldEvidenceValidationReport.Empty
            : new AquariumFieldEvidenceValidationReport(issues);
    }

    private static void ValidateStereoDepthInputResource(
        ICollection<AquariumFieldEvidenceIssue> issues,
        IReadOnlyDictionary<string, AquariumFieldResourceDeclaration> resourcesByKey,
        AquariumFieldStereoDepthLowering lowering,
        string resourceKey,
        string role)
    {
        if (!resourcesByKey.TryGetValue(resourceKey, out var resource))
        {
            issues.Add(Error(lowering.LoweringKey, $"Stereo depth lowering references unknown {role} input resource '{resourceKey}'."));
            return;
        }

        if (resource.Kind is not (AquariumFieldResourceKind.Texture2D or AquariumFieldResourceKind.RollingTexture))
        {
            issues.Add(Error(lowering.LoweringKey, $"Stereo depth {role} input resource '{resourceKey}' is {resource.Kind}, not Texture2D or RollingTexture."));
        }

        if (resource.Access != AquariumFieldShaderAccess.ShaderResource)
        {
            issues.Add(Error(lowering.LoweringKey, $"Stereo depth {role} input resource '{resourceKey}' must be shader-readable."));
        }
    }

    private static AquariumFieldEvidenceIssue Error(string key, string message) =>
        new(AquariumFieldEvidenceIssueSeverity.Error, key, message);

    private static AquariumFieldEvidenceIssue Warning(string key, string message) =>
        new(AquariumFieldEvidenceIssueSeverity.Warning, key, message);

    private static bool LooksLikeResourceKey(string payloadHandle) =>
        payloadHandle.StartsWith("resource:", StringComparison.Ordinal) ||
        payloadHandle.StartsWith("mimir:resource:", StringComparison.Ordinal) ||
        payloadHandle.StartsWith("aquarium:resource:", StringComparison.Ordinal);
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
                Proposal: claim.Proposal,
                Support: claim.Support,
                Guide: candidate.Guide,
                PayloadHandle: claim.PayloadHandle));
        }

        return requests;
    }
}

public static class AquariumFieldLoweringPlanner
{
    public static AquariumFieldLoweringPlan Plan(AquariumFieldEvidenceFrame frame)
    {
        var resources = frame.Resources.ToDictionary(static resource => resource.ResourceKey, StringComparer.Ordinal);
        return Plan(frame, resources);
    }

    public static AquariumFieldLoweringPlan Plan(
        AquariumFieldEvidenceFrame frame,
        IReadOnlyDictionary<string, AquariumFieldResourceDeclaration> resources)
    {
        var requests = AquariumFieldEvidenceNormalizer.BuildLoweringRequests(frame);
        if (requests.Count == 0)
        {
            return AquariumFieldLoweringPlan.Empty;
        }

        var packets = new List<AquariumFieldBackendPacket>(requests.Count);
        var deferred = new List<AquariumFieldLoweringRequest>();
        var domains = frame.Domains.ToDictionary(static domain => domain.DomainKey, StringComparer.Ordinal);
        foreach (var request in requests)
        {
            if (RequiresResource(request.Encoding) && !LooksLikeResourceKey(request.PayloadHandle))
            {
                deferred.Add(request);
                continue;
            }

            if (LooksLikeResourceKey(request.PayloadHandle) &&
                (!resources.TryGetValue(request.PayloadHandle, out var resource) ||
                 !IsResourceCompatible(request, resource)))
            {
                deferred.Add(request);
                continue;
            }

            var domainKind = domains.TryGetValue(request.DomainKey, out var domain)
                ? domain.Kind
                : AquariumFieldDomainKind.Unknown;
            if (!TrySelectBackend(request, domainKind, out var backend))
            {
                deferred.Add(request);
                continue;
            }

            packets.Add(new AquariumFieldBackendPacket(
                PacketKey: $"packet:{request.RequestKey}",
                ClaimKey: request.ClaimKey,
                DomainKey: request.DomainKey,
                Layer: request.Layer,
                Encoding: request.Encoding,
                Backend: backend,
                Support: request.Support,
                Proposal: request.Proposal,
                Guide: request.Guide,
                PayloadHandle: request.PayloadHandle));
        }

        return new AquariumFieldLoweringPlan(packets, deferred);
    }

    public static bool TrySelectBackend(AquariumFieldLoweringRequest request, out AquariumFieldBackendKind backend)
    {
        return TrySelectBackend(request, AquariumFieldDomainKind.Unknown, out backend);
    }

    private static bool TrySelectBackend(
        AquariumFieldLoweringRequest request,
        AquariumFieldDomainKind domainKind,
        out AquariumFieldBackendKind backend)
    {
        backend = request.Encoding switch
        {
            AquariumFieldEncoding.Height or AquariumFieldEncoding.Sdf2D => AquariumFieldBackendKind.SurfacePage,
            AquariumFieldEncoding.Sdf3D => AquariumFieldBackendKind.DirectSdf,
            AquariumFieldEncoding.Density or AquariumFieldEncoding.Extinction => AquariumFieldBackendKind.VolumeSplat,
            AquariumFieldEncoding.Tube => AquariumFieldBackendKind.TubeField,
            AquariumFieldEncoding.Mesh => AquariumFieldBackendKind.Mesh,
            AquariumFieldEncoding.Phase or AquariumFieldEncoding.Confidence
                when domainKind == AquariumFieldDomainKind.AudioPath => AquariumFieldBackendKind.DebugOverlay,
            AquariumFieldEncoding.Feature
                when domainKind == AquariumFieldDomainKind.CameraSensor &&
                     request.ProposalKind == AquariumFieldProposalKind.DeterministicStructural => AquariumFieldBackendKind.DebugOverlay,
            AquariumFieldEncoding.Feature
                when domainKind == AquariumFieldDomainKind.CameraSensor &&
                     request.ProposalKind == AquariumFieldProposalKind.SensorObservation => AquariumFieldBackendKind.GpuSensorFusion,
            _ => AquariumFieldBackendKind.Unknown,
        };

        return backend != AquariumFieldBackendKind.Unknown;
    }

    private static bool IsResourceCompatible(
        AquariumFieldLoweringRequest request,
        AquariumFieldResourceDeclaration resource)
    {
        if (!resource.HasShape || !resource.IsGpuVisible)
        {
            return false;
        }

        return request.Encoding switch
        {
            AquariumFieldEncoding.Tube => resource.Kind is AquariumFieldResourceKind.CurvePointBuffer or AquariumFieldResourceKind.StructuredBuffer,
            AquariumFieldEncoding.Mesh => resource.Kind == AquariumFieldResourceKind.Mesh,
            AquariumFieldEncoding.Height or AquariumFieldEncoding.Sdf2D or AquariumFieldEncoding.Material => resource.Kind is AquariumFieldResourceKind.SurfacePage or AquariumFieldResourceKind.Texture2D or AquariumFieldResourceKind.RollingTexture,
            AquariumFieldEncoding.Density or AquariumFieldEncoding.Extinction or AquariumFieldEncoding.Sdf3D => resource.Kind == AquariumFieldResourceKind.VolumeTexture,
            AquariumFieldEncoding.Feature or AquariumFieldEncoding.Confidence or AquariumFieldEncoding.Phase => true,
            _ => false,
        };
    }

    private static bool RequiresResource(AquariumFieldEncoding encoding) =>
        encoding is
            AquariumFieldEncoding.Height or
            AquariumFieldEncoding.Sdf2D or
            AquariumFieldEncoding.Sdf3D or
            AquariumFieldEncoding.Density or
            AquariumFieldEncoding.Extinction or
            AquariumFieldEncoding.Material or
            AquariumFieldEncoding.Tube or
            AquariumFieldEncoding.Mesh;

    private static bool LooksLikeResourceKey(string payloadHandle) =>
        payloadHandle.StartsWith("resource:", StringComparison.Ordinal) ||
        payloadHandle.StartsWith("mimir:resource:", StringComparison.Ordinal) ||
        payloadHandle.StartsWith("aquarium:resource:", StringComparison.Ordinal);
}
