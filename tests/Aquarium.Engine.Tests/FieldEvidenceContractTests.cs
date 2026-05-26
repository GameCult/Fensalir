using System.Numerics;
using Aquarium.Engine.Render;

namespace Aquarium.Engine.Tests;

public sealed class FieldEvidenceContractTests
{
    [Fact]
    public void SceneStateCarriesFieldEvidenceWithoutChoosingBackendAuthority()
    {
        var support = new AquariumFieldSupport(
            Center: new Vector3(0.0f, 1.0f, 2.0f),
            Radius: new Vector3(4.0f, 0.25f, 0.25f),
            LocalFrame: Matrix4x4.Identity,
            ConservativeRadius: 4.0f,
            ProjectedError: 0.5f,
            Curvature: 0.1f,
            TemporalUncertainty: 0.02f);

        var proposal = new AquariumFieldProposalPolicy(
            AquariumFieldProposalKind.SensorObservation,
            SourcePdf: 0.25f,
            TargetContribution: 12.0f,
            RepresentedCandidateCount: 4,
            Seed: 123u);

        var frame = new AquariumFieldEvidenceFrame
        {
            AccumulationWindowSeconds = 5.0f,
            PresentationDelaySeconds = 0.25f,
            Domains =
            [
                new AquariumFieldDomain(
                    "mimir:spectrum:asio-ch0",
                    "mimir:room",
                    AquariumFieldDomainKind.RollingBuffer,
                    Matrix4x4.Identity,
                    Matrix4x4.Identity,
                    new Vector3(-1.0f, 0.0f, -5.0f),
                    new Vector3(1.0f, 2.0f, 0.0f),
                    Vector3.Zero,
                    "Mimir.Runtime")
            ],
            Claims =
            [
                new AquariumFieldClaim(
                    "claim:mimir:spectrum:asio-ch0:42",
                    "mimir:spectrum:asio-ch0",
                    "mimir:asio",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Tube,
                    support,
                    proposal,
                    "rolling-window:asio-ch0:spectrum",
                    ObservedTimeNs: 10_000_000,
                    Confidence: 0.91f)
            ],
            Candidates =
            [
                new AquariumFieldCandidate(
                    "candidate:mimir:spectrum:asio-ch0:42",
                    "claim:mimir:spectrum:asio-ch0:42",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Tube,
                    proposal,
                    AquariumFieldGuide.Valid(0.91f, sampleAgeSeconds: 0.016f))
            ],
            BackendPackets =
            [
                new AquariumFieldBackendPacket(
                    "packet:mimir:spectrum:asio-ch0:42:tube",
                    "claim:mimir:spectrum:asio-ch0:42",
                    "mimir:spectrum:asio-ch0",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Tube,
                    AquariumFieldBackendKind.TubeField,
                    support,
                    AquariumFieldGuide.Valid(0.91f, sampleAgeSeconds: 0.016f),
                    "rolling-window:asio-ch0:spectrum")
            ],
        };

        var scene = new AquariumSceneState { FieldEvidenceFrame = frame };

        Assert.True(scene.FieldEvidenceFrame.HasInput);
        Assert.True(scene.FieldEvidenceFrame.Domains[0].HasBounds);
        Assert.True(scene.FieldEvidenceFrame.Claims[0].HasEvidence);
        Assert.True(scene.FieldEvidenceFrame.Candidates[0].IsSelectable);
        Assert.True(scene.FieldEvidenceFrame.BackendPackets[0].IsEvidenceWriter);
        Assert.Equal(AquariumFieldBackendKind.TubeField, scene.FieldEvidenceFrame.BackendPackets[0].Backend);
    }

    [Fact]
    public void InvalidBackendPacketCannotPretendToWriteReusableEvidence()
    {
        var packet = new AquariumFieldBackendPacket(
            PacketKey: "debug:pixel-only",
            ClaimKey: "",
            DomainKey: "debug",
            Layer: AquariumFieldLayer.Unknown,
            Encoding: AquariumFieldEncoding.Unknown,
            Backend: AquariumFieldBackendKind.DebugOverlay,
            Support: default,
            Guide: default,
            PayloadHandle: "");

        Assert.False(packet.IsEvidenceWriter);
    }

    [Fact]
    public void ValidatorAcceptsCoherentFieldEvidenceFrame()
    {
        var frame = BuildValidTubeEvidenceFrame();

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.False(report.HasErrors);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void ValidatorRejectsSplitAuthorityPackets()
    {
        var frame = new AquariumFieldEvidenceFrame
        {
            Domains =
            [
                new AquariumFieldDomain(
                    "debug",
                    "",
                    AquariumFieldDomainKind.Surface2D,
                    Matrix4x4.Identity,
                    Matrix4x4.Identity,
                    Vector3.Zero,
                    Vector3.One,
                    Vector3.Zero,
                    "test")
            ],
            Claims =
            [
                new AquariumFieldClaim(
                    "claim:debug",
                    "debug",
                    "test",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Tube,
                    default,
                    default,
                    "",
                    0,
                    0.0f)
            ],
            BackendPackets =
            [
                new AquariumFieldBackendPacket(
                    "packet:debug",
                    "claim:missing",
                    "debug",
                    AquariumFieldLayer.Unknown,
                    AquariumFieldEncoding.Unknown,
                    AquariumFieldBackendKind.Unknown,
                    default,
                    default,
                    "")
            ],
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "claim:debug");
        Assert.Contains(report.Issues, issue => issue.Key == "packet:debug");
    }

    [Fact]
    public void NormalizerBuildsPendingLoweringRequestsWithoutChoosingBackend()
    {
        var frame = BuildValidTubeEvidenceFrame();

        var requests = AquariumFieldEvidenceNormalizer.BuildLoweringRequests(frame);

        Assert.Single(requests);
        Assert.True(requests[0].IsPendingBackendSelection);
        Assert.Equal("claim:mimir:spectrum:asio-ch0:42", requests[0].ClaimKey);
        Assert.Equal(AquariumFieldEncoding.Tube, requests[0].Encoding);
    }

    [Fact]
    public void NormalizerRefusesInvalidEvidence()
    {
        var frame = new AquariumFieldEvidenceFrame
        {
            Candidates =
            [
                new AquariumFieldCandidate(
                    "candidate:orphan",
                    "claim:missing",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Feature,
                    new AquariumFieldProposalPolicy(AquariumFieldProposalKind.SensorObservation, 1.0f, 1.0f, 1, 1u),
                    AquariumFieldGuide.Valid(1.0f))
            ],
        };

        var requests = AquariumFieldEvidenceNormalizer.BuildLoweringRequests(frame);

        Assert.Empty(requests);
    }

    [Fact]
    public void LoweringPlannerSelectsOnlyObviousBackends()
    {
        var frame = BuildValidTubeEvidenceFrame();

        var plan = AquariumFieldLoweringPlanner.Plan(frame);

        Assert.True(plan.HasPackets);
        Assert.Empty(plan.DeferredRequests);
        Assert.Single(plan.Packets);
        Assert.Equal(AquariumFieldBackendKind.TubeField, plan.Packets[0].Backend);
        Assert.True(plan.Packets[0].IsEvidenceWriter);
    }

    [Fact]
    public void LoweringPlannerDefersAmbiguousFeatureEvidence()
    {
        var support = new AquariumFieldSupport(
            Vector3.Zero,
            Vector3.One,
            Matrix4x4.Identity,
            1.0f,
            0.0f,
            0.0f,
            0.0f);
        var proposal = new AquariumFieldProposalPolicy(
            AquariumFieldProposalKind.SensorObservation,
            1.0f,
            1.0f,
            1,
            1u);
        var frame = new AquariumFieldEvidenceFrame
        {
            Domains =
            [
                new AquariumFieldDomain(
                    "mimir:observation:feature",
                    "",
                    AquariumFieldDomainKind.CameraSensor,
                    Matrix4x4.Identity,
                    Matrix4x4.Identity,
                    Vector3.Zero,
                    Vector3.One,
                    Vector3.Zero,
                    "Mimir.Runtime")
            ],
            Claims =
            [
                new AquariumFieldClaim(
                    "claim:feature",
                    "mimir:observation:feature",
                    "mimir",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Feature,
                    support,
                    proposal,
                    "",
                    0,
                    1.0f)
            ],
            Candidates =
            [
                new AquariumFieldCandidate(
                    "candidate:feature",
                    "claim:feature",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Feature,
                    proposal,
                    AquariumFieldGuide.Valid(1.0f))
            ],
        };

        var plan = AquariumFieldLoweringPlanner.Plan(frame);

        Assert.False(plan.HasPackets);
        Assert.Empty(plan.Packets);
        Assert.Single(plan.DeferredRequests);
        Assert.Equal("claim:feature", plan.DeferredRequests[0].ClaimKey);
    }

    [Fact]
    public void FieldDslBindsDeclaredResourcesBeforePlanningTubePackets()
    {
        var resource = new AquariumFieldResourceDeclaration(
            "mimir:resource:native-ring:asio-ch0",
            AquariumFieldResourceKind.StructuredBuffer,
            AquariumFieldResourceResidency.SharedGpu,
            AquariumFieldShaderAccess.ShaderResource,
            "Float32",
            Width: 192,
            Height: 1,
            DepthOrCount: 192,
            StrideBytes: 4,
            ValidFromNs: 0,
            ValidUntilNs: 10_000_000,
            Version: 42,
            NativeHandle: new IntPtr(0x1234),
            NativeHandleKind: "native-ring");
        const string source = """
resource id=spectrum key=mimir:resource:native-ring:asio-ch0
domain id=mimir:spectrum:asio-ch0 kind=RollingBuffer min=-1,0,-5 max=1,2,0 owner=Mimir.Runtime
tubeclaim id=spectrum-trail resource=spectrum domain=mimir:spectrum:asio-ch0 confidence=0.91 radius=0.02
""";

        var frame = AquariumFieldScriptCompiler.CompileEvidence(
            source,
            new Dictionary<string, AquariumFieldResourceDeclaration>(StringComparer.Ordinal)
            {
                [resource.ResourceKey] = resource,
            });
        var plan = AquariumFieldLoweringPlanner.Plan(frame);

        Assert.Single(frame.Resources);
        Assert.Single(frame.Claims);
        Assert.Single(plan.Packets);
        Assert.Empty(plan.DeferredRequests);
        Assert.Equal(AquariumFieldBackendKind.TubeField, plan.Packets[0].Backend);
        Assert.Equal(resource.ResourceKey, plan.Packets[0].PayloadHandle);
    }

    private static AquariumFieldEvidenceFrame BuildValidTubeEvidenceFrame()
    {
        var support = new AquariumFieldSupport(
            Center: new Vector3(0.0f, 1.0f, 2.0f),
            Radius: new Vector3(4.0f, 0.25f, 0.25f),
            LocalFrame: Matrix4x4.Identity,
            ConservativeRadius: 4.0f,
            ProjectedError: 0.5f,
            Curvature: 0.1f,
            TemporalUncertainty: 0.02f);

        var proposal = new AquariumFieldProposalPolicy(
            AquariumFieldProposalKind.SensorObservation,
            SourcePdf: 0.25f,
            TargetContribution: 12.0f,
            RepresentedCandidateCount: 4,
            Seed: 123u);

        return new AquariumFieldEvidenceFrame
        {
            Resources =
            [
                new AquariumFieldResourceDeclaration(
                    "mimir:resource:native-ring:asio-ch0",
                    AquariumFieldResourceKind.StructuredBuffer,
                    AquariumFieldResourceResidency.SharedGpu,
                    AquariumFieldShaderAccess.ShaderResource,
                    "Float32",
                    Width: 192,
                    Height: 1,
                    DepthOrCount: 192,
                    StrideBytes: 4,
                    ValidFromNs: 0,
                    ValidUntilNs: 10_000_000,
                    Version: 42,
                    NativeHandle: new IntPtr(0x1234),
                    NativeHandleKind: "native-ring")
            ],
            Domains =
            [
                new AquariumFieldDomain(
                    "mimir:spectrum:asio-ch0",
                    "mimir:room",
                    AquariumFieldDomainKind.RollingBuffer,
                    Matrix4x4.Identity,
                    Matrix4x4.Identity,
                    new Vector3(-1.0f, 0.0f, -5.0f),
                    new Vector3(1.0f, 2.0f, 0.0f),
                    Vector3.Zero,
                    "Mimir.Runtime")
            ],
            Claims =
            [
                new AquariumFieldClaim(
                    "claim:mimir:spectrum:asio-ch0:42",
                    "mimir:spectrum:asio-ch0",
                    "mimir:asio",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Tube,
                    support,
                    proposal,
                    "mimir:resource:native-ring:asio-ch0",
                    ObservedTimeNs: 10_000_000,
                    Confidence: 0.91f)
            ],
            Candidates =
            [
                new AquariumFieldCandidate(
                    "candidate:mimir:spectrum:asio-ch0:42",
                    "claim:mimir:spectrum:asio-ch0:42",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Tube,
                    proposal,
                    AquariumFieldGuide.Valid(0.91f, sampleAgeSeconds: 0.016f))
            ],
            BackendPackets =
            [
                new AquariumFieldBackendPacket(
                    "packet:mimir:spectrum:asio-ch0:42:tube",
                    "claim:mimir:spectrum:asio-ch0:42",
                    "mimir:spectrum:asio-ch0",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Tube,
                    AquariumFieldBackendKind.TubeField,
                    support,
                    AquariumFieldGuide.Valid(0.91f, sampleAgeSeconds: 0.016f),
                    "mimir:resource:native-ring:asio-ch0")
            ],
        };
    }
}
