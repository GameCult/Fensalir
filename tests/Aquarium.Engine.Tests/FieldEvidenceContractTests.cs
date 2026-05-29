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
                    proposal,
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
            Proposal: default,
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
    public void ValidatorRejectsNonFiniteOrZeroReservoirProposalPolicy()
    {
        var frame = BuildValidTubeEvidenceFrame();
        var badProposal = frame.Claims[0].Proposal with
        {
            SourcePdf = float.NaN,
            TargetContribution = 0.0f,
        };
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = frame.Resources,
            Domains = frame.Domains,
            Claims = [frame.Claims[0] with { Proposal = badProposal }],
            Candidates = [frame.Candidates[0] with { Proposal = badProposal }],
            TubeSplineLowerings = frame.TubeSplineLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "claim:mimir:spectrum:asio-ch0:42");
        Assert.Contains(report.Issues, issue => issue.Key == "candidate:mimir:spectrum:asio-ch0:42");
        Assert.Empty(AquariumFieldEvidenceNormalizer.BuildLoweringRequests(frame));
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
    public void ValidatorRejectsDuplicateFieldResourceKeys()
    {
        var frame = BuildValidTubeEvidenceFrame();
        var duplicate = frame.Resources[0] with { Version = 43 };
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = [frame.Resources[0], duplicate],
            Domains = frame.Domains,
            Claims = frame.Claims,
            Candidates = frame.Candidates,
            BackendPackets = frame.BackendPackets,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "mimir:resource:native-ring:asio-ch0");
    }

    [Fact]
    public void ValidatorAcceptsKnownFieldResourceUploads()
    {
        var frame = BuildValidTubeEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = frame.Resources,
            ResourceUploads =
            [
                new AquariumFieldResourceUpload
                {
                    ResourceKey = frame.Resources[0].ResourceKey,
                    Version = frame.Resources[0].Version,
                    ElementOffset = 1,
                    Float32Data = [0.0f, 0.25f, 0.5f, 1.0f],
                },
            ],
            Domains = frame.Domains,
            Claims = frame.Claims,
            Candidates = frame.Candidates,
            BackendPackets = frame.BackendPackets,
            TubeSplineLowerings = frame.TubeSplineLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.False(report.HasErrors);
    }

    [Fact]
    public void ValidatorRejectsOutOfRangeFieldResourceUploadOffsets()
    {
        var frame = BuildValidTubeEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = [frame.Resources[0] with { DepthOrCount = 4, Width = 4, Format = "Float32", StrideBytes = 4 }],
            ResourceUploads =
            [
                new AquariumFieldResourceUpload
                {
                    ResourceKey = frame.Resources[0].ResourceKey,
                    ElementOffset = 3,
                    Float32Data = [0.0f, 0.5f],
                },
            ],
            Domains = frame.Domains,
            Claims = frame.Claims,
            Candidates = frame.Candidates,
            BackendPackets = frame.BackendPackets,
            TubeSplineLowerings = frame.TubeSplineLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "mimir:resource:native-ring:asio-ch0");
    }

    [Fact]
    public void ValidatorRejectsUnknownFieldResourceUploads()
    {
        var frame = BuildValidTubeEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = frame.Resources,
            ResourceUploads =
            [
                new AquariumFieldResourceUpload
                {
                    ResourceKey = "mimir:resource:missing",
                    Float32Data = [1.0f],
                },
            ],
            Domains = frame.Domains,
            Claims = frame.Claims,
            Candidates = frame.Candidates,
            BackendPackets = frame.BackendPackets,
            TubeSplineLowerings = frame.TubeSplineLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "mimir:resource:missing");
    }

    [Fact]
    public void ValidatorRejectsOversizedFieldResourceUploads()
    {
        var frame = BuildValidTubeEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = [frame.Resources[0] with { DepthOrCount = 2, Width = 2, Format = "Float32", StrideBytes = 4 }],
            ResourceUploads =
            [
                new AquariumFieldResourceUpload
                {
                    ResourceKey = frame.Resources[0].ResourceKey,
                    Float32Data = [0.0f, 0.5f, 1.0f],
                },
            ],
            Domains = frame.Domains,
            Claims = frame.Claims,
            Candidates = frame.Candidates,
            BackendPackets = frame.BackendPackets,
            TubeSplineLowerings = frame.TubeSplineLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "mimir:resource:native-ring:asio-ch0");
    }

    [Fact]
    public void ValidatorRejectsNonFloatFieldResourceUploads()
    {
        var frame = BuildValidTubeEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = [frame.Resources[0] with { Format = "Int16", StrideBytes = 2 }],
            ResourceUploads =
            [
                new AquariumFieldResourceUpload
                {
                    ResourceKey = frame.Resources[0].ResourceKey,
                    Float32Data = [1.0f],
                },
            ],
            Domains = frame.Domains,
            Claims = frame.Claims,
            Candidates = frame.Candidates,
            BackendPackets = frame.BackendPackets,
            TubeSplineLowerings = frame.TubeSplineLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "mimir:resource:native-ring:asio-ch0");
    }

    [Fact]
    public void ValidatorRejectsTubeSplineLoweringClaimEncodingMismatch()
    {
        var frame = BuildValidTubeEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = frame.Resources,
            Domains = frame.Domains,
            Claims = [frame.Claims[0] with { Encoding = AquariumFieldEncoding.Mesh }],
            Candidates = frame.Candidates,
            BackendPackets = frame.BackendPackets,
            TubeSplineLowerings = frame.TubeSplineLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "tube-spline:mimir:spectrum:asio-ch0:42");
    }

    [Fact]
    public void ValidatorRejectsTubeSplineLoweringResourceMismatch()
    {
        var frame = BuildValidTubeEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources =
            [
                frame.Resources[0],
                frame.Resources[0] with { ResourceKey = "mimir:resource:native-ring:asio-ch1" },
            ],
            Domains = frame.Domains,
            Claims = frame.Claims,
            Candidates = frame.Candidates,
            BackendPackets = frame.BackendPackets,
            TubeSplineLowerings =
            [
                frame.TubeSplineLowerings[0] with { ResourceKey = "mimir:resource:native-ring:asio-ch1" },
            ],
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "tube-spline:mimir:spectrum:asio-ch0:42");
    }

    [Fact]
    public void ValidatorAcceptsStereoDepthLoweringWithComputeWritableDisparity()
    {
        var frame = BuildValidStereoDepthEvidenceFrame();

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.False(report.HasErrors);
        Assert.Empty(report.Issues);
        Assert.True(frame.HasInput);
        Assert.Equal(AquariumFieldShaderAccess.UnorderedAccess, frame.Resources[2].Access);
    }

    [Fact]
    public void ValidatorRejectsStereoDepthLoweringClaimEncodingMismatch()
    {
        var frame = BuildValidStereoDepthEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources = frame.Resources,
            Domains = frame.Domains,
            Claims = [frame.Claims[0] with { Encoding = AquariumFieldEncoding.Mesh }],
            Candidates = frame.Candidates,
            StereoDepthLowerings = frame.StereoDepthLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "stereo-depth:mimir:leap:libsgm");
    }

    [Fact]
    public void ValidatorRejectsStereoDepthLoweringWithoutUnorderedAccessDisparity()
    {
        var frame = BuildValidStereoDepthEvidenceFrame();
        frame = new AquariumFieldEvidenceFrame
        {
            Resources =
            [
                frame.Resources[0],
                frame.Resources[1],
                frame.Resources[2] with { Access = AquariumFieldShaderAccess.ShaderResource },
            ],
            Domains = frame.Domains,
            Claims = frame.Claims,
            Candidates = frame.Candidates,
            StereoDepthLowerings = frame.StereoDepthLowerings,
            AccumulationWindowSeconds = frame.AccumulationWindowSeconds,
            PresentationDelaySeconds = frame.PresentationDelaySeconds,
        };

        var report = AquariumFieldEvidenceValidator.Validate(frame);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Key == "stereo-depth:mimir:leap:libsgm");
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
        Assert.Equal(frame.Claims[0].Proposal.SourcePdf, requests[0].Proposal.SourcePdf);
        Assert.Equal(frame.Claims[0].Proposal.TargetContribution, requests[0].Proposal.TargetContribution);
        Assert.Equal(frame.Claims[0].Proposal.RepresentedCandidateCount, requests[0].Proposal.RepresentedCandidateCount);
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
        Assert.Equal(frame.Claims[0].Proposal.SourcePdf, plan.Packets[0].Proposal.SourcePdf);
        Assert.Equal(frame.Claims[0].Proposal.TargetContribution, plan.Packets[0].Proposal.TargetContribution);
        Assert.Equal(frame.Claims[0].Proposal.RepresentedCandidateCount, plan.Packets[0].Proposal.RepresentedCandidateCount);
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
    public void LoweringPlannerSelectsAudioPathPhaseConfidenceAsDebugOverlay()
    {
        var support = new AquariumFieldSupport(
            Center: Vector3.Zero,
            Radius: new Vector3(0.01f, 0.01f, 0.0005f),
            LocalFrame: Matrix4x4.Identity,
            ConservativeRadius: 0.01f,
            ProjectedError: 0.0005f,
            Curvature: 0.0f,
            TemporalUncertainty: 0.0005f);
        var proposal = new AquariumFieldProposalPolicy(
            AquariumFieldProposalKind.CalibrationConstraint,
            1.0f,
            0.72f,
            1,
            42u);
        var frame = new AquariumFieldEvidenceFrame
        {
            Domains =
            [
                new AquariumFieldDomain(
                    "mimir:calibration:loopback->mic:complex-contour",
                    "",
                    AquariumFieldDomainKind.AudioPath,
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
                    "calibration:loopback->mic:complex-contour",
                    "mimir:calibration:loopback->mic:complex-contour",
                    "loopback->mic",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Confidence,
                    support,
                    proposal,
                    "direct-path",
                    0,
                    0.72f)
            ],
            Candidates =
            [
                new AquariumFieldCandidate(
                    "calibration:loopback->mic:complex-contour:candidate",
                    "calibration:loopback->mic:complex-contour",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Confidence,
                    proposal,
                    AquariumFieldGuide.Valid(0.72f))
            ],
        };

        var plan = AquariumFieldLoweringPlanner.Plan(frame);

        Assert.Single(plan.Packets);
        Assert.Empty(plan.DeferredRequests);
        Assert.Equal(AquariumFieldBackendKind.DebugOverlay, plan.Packets[0].Backend);
        Assert.Equal(AquariumFieldEncoding.Confidence, plan.Packets[0].Encoding);
    }

    [Fact]
    public void LoweringPlannerSelectsDeterministicCameraFeatureAsDebugOverlay()
    {
        var support = new AquariumFieldSupport(
            Center: new Vector3(0.0f, 1.2f, 2.5f),
            Radius: new Vector3(0.05f),
            LocalFrame: Matrix4x4.Identity,
            ConservativeRadius: 0.05f,
            ProjectedError: 0.02f,
            Curvature: 0.0f,
            TemporalUncertainty: 0.0f);
        var proposal = new AquariumFieldProposalPolicy(
            AquariumFieldProposalKind.DeterministicStructural,
            1.0f,
            0.88f,
            2,
            123u);
        var frame = new AquariumFieldEvidenceFrame
        {
            Domains =
            [
                new AquariumFieldDomain(
                    "mimir:marker:synthetic-board:marker-a",
                    "",
                    AquariumFieldDomainKind.CameraSensor,
                    Matrix4x4.Identity,
                    Matrix4x4.Identity,
                    new Vector3(-0.05f, 1.15f, 2.45f),
                    new Vector3(0.05f, 1.25f, 2.55f),
                    Vector3.Zero,
                    "Mimir.Runtime")
            ],
            Claims =
            [
                new AquariumFieldClaim(
                    "visual-marker:synthetic-board:marker-a",
                    "mimir:marker:synthetic-board:marker-a",
                    "mimir-marker-fusion",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Feature,
                    support,
                    proposal,
                    "synthetic-board",
                    10_000_000,
                    0.88f)
            ],
            Candidates =
            [
                new AquariumFieldCandidate(
                    "visual-marker:synthetic-board:marker-a:candidate",
                    "visual-marker:synthetic-board:marker-a",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Feature,
                    proposal,
                    AquariumFieldGuide.Valid(0.88f))
            ],
        };

        var plan = AquariumFieldLoweringPlanner.Plan(frame);

        Assert.Single(plan.Packets);
        Assert.Empty(plan.DeferredRequests);
        Assert.Equal(AquariumFieldBackendKind.DebugOverlay, plan.Packets[0].Backend);
        Assert.Equal(AquariumFieldEncoding.Feature, plan.Packets[0].Encoding);
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
                    proposal,
                    AquariumFieldGuide.Valid(0.91f, sampleAgeSeconds: 0.016f),
                    "mimir:resource:native-ring:asio-ch0")
            ],
            TubeSplineLowerings =
            [
                new AquariumFieldTubeSplineLowering(
                    "tube-spline:mimir:spectrum:asio-ch0:42",
                    "claim:mimir:spectrum:asio-ch0:42",
                    "mimir:resource:native-ring:asio-ch0",
                    Width: 64,
                    Height: 8,
                    StrideBytes: 4,
                    FirstColumn: 0,
                    ColumnCount: 4,
                    ColumnStride: 1,
                    RollingModulo: 8,
                    RollingOffset: 0,
                    Origin: Vector3.Zero,
                    AxisStep: new Vector3(0.01f, 0.0f, 0.0f),
                    ColumnStep: new Vector3(0.0f, 0.0f, 0.02f),
                    AmplitudePower: 2.0f,
                    AmplitudeScale: 0.25f,
                    NormalizeMin: 0.0f,
                    NormalizeMax: 1.0f,
                    BaseRadius: 0.01f,
                    RadiusScale: 0.02f,
                    Alpha: 1.0f,
                    Feather: 0.2f,
                    RampTexturePath: @"D:\WIP4\Projects\Aetheria\Assets\Resources\Ramps\blackbody.png",
                    RampResourceKey: "",
                    EmissionScale: 10.0f,
                    CatmullRomSubdivisions: 4)
            ],
        };
    }

    private static AquariumFieldEvidenceFrame BuildValidStereoDepthEvidenceFrame()
    {
        var support = new AquariumFieldSupport(
            Center: new Vector3(320.0f, 240.0f, 2.0f),
            Radius: new Vector3(320.0f, 240.0f, 2.0f),
            LocalFrame: Matrix4x4.Identity,
            ConservativeRadius: 320.0f,
            ProjectedError: 1.0f / 128.0f,
            Curvature: 0.0f,
            TemporalUncertainty: 0.0f);

        var proposal = new AquariumFieldProposalPolicy(
            AquariumFieldProposalKind.SensorObservation,
            SourcePdf: 1.0f,
            TargetContribution: 1.0f,
            RepresentedCandidateCount: 1,
            Seed: 9u);

        return new AquariumFieldEvidenceFrame
        {
            Resources =
            [
                new AquariumFieldResourceDeclaration(
                    "mimir:resource:leap:left-ir",
                    AquariumFieldResourceKind.Texture2D,
                    AquariumFieldResourceResidency.SharedGpu,
                    AquariumFieldShaderAccess.ShaderResource,
                    "R8_UNorm",
                    Width: 640,
                    Height: 480,
                    DepthOrCount: 1,
                    StrideBytes: 1,
                    ValidFromNs: 42,
                    ValidUntilNs: 42,
                    Version: 42,
                    NativeHandle: new IntPtr(0x1001),
                    NativeHandleKind: "shared-d3d12-texture"),
                new AquariumFieldResourceDeclaration(
                    "mimir:resource:leap:right-ir",
                    AquariumFieldResourceKind.Texture2D,
                    AquariumFieldResourceResidency.SharedGpu,
                    AquariumFieldShaderAccess.ShaderResource,
                    "R8_UNorm",
                    Width: 640,
                    Height: 480,
                    DepthOrCount: 1,
                    StrideBytes: 1,
                    ValidFromNs: 42,
                    ValidUntilNs: 42,
                    Version: 42,
                    NativeHandle: new IntPtr(0x1002),
                    NativeHandleKind: "shared-d3d12-texture"),
                new AquariumFieldResourceDeclaration(
                    "mimir:resource:leap:disparity-r16f",
                    AquariumFieldResourceKind.SurfacePage,
                    AquariumFieldResourceResidency.GpuResident,
                    AquariumFieldShaderAccess.UnorderedAccess,
                    "R16Float",
                    Width: 640,
                    Height: 480,
                    DepthOrCount: 1,
                    StrideBytes: 2,
                    ValidFromNs: 42,
                    ValidUntilNs: 42,
                    Version: 42,
                    NativeHandle: IntPtr.Zero,
                    NativeHandleKind: "fensalir-stereo-depth-disparity"),
            ],
            Domains =
            [
                new AquariumFieldDomain(
                    "mimir:stereo-depth:leap:libsgm",
                    "mimir:leap",
                    AquariumFieldDomainKind.Surface2D,
                    Matrix4x4.Identity,
                    Matrix4x4.Identity,
                    Vector3.Zero,
                    new Vector3(640.0f, 480.0f, 4.0f),
                    Vector3.Zero,
                    "Mimir.Runtime")
            ],
            Claims =
            [
                new AquariumFieldClaim(
                    "claim:mimir:stereo-depth:leap:libsgm",
                    "mimir:stereo-depth:leap:libsgm",
                    "mimir:leap",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Height,
                    support,
                    proposal,
                    "mimir:resource:leap:disparity-r16f",
                    ObservedTimeNs: 42,
                    Confidence: 0.8f)
            ],
            Candidates =
            [
                new AquariumFieldCandidate(
                    "candidate:mimir:stereo-depth:leap:libsgm",
                    "claim:mimir:stereo-depth:leap:libsgm",
                    AquariumFieldLayer.Form,
                    AquariumFieldEncoding.Height,
                    proposal,
                    AquariumFieldGuide.Valid(0.8f))
            ],
            StereoDepthLowerings =
            [
                new AquariumFieldStereoDepthLowering(
                    "stereo-depth:mimir:leap:libsgm",
                    "claim:mimir:stereo-depth:leap:libsgm",
                    "d3d12-sgm-libsgm-provenance",
                    "leap-calibration",
                    "leap-ir-pair",
                    "mimir:resource:leap:left-ir",
                    "mimir:resource:leap:right-ir",
                    "mimir:resource:leap:disparity-r16f",
                    "",
                    Width: 640,
                    Height: 480,
                    MinDisparity: 0,
                    DisparityLevels: 128,
                    AggregationPathCount: 4,
                    CensusRadius: 2,
                    SmoothnessPenaltySmall: 8.0f,
                    SmoothnessPenaltyLarge: 96.0f,
                    MinDepthMeters: 0.15f,
                    MaxDepthMeters: 4.0f)
            ],
        };
    }
}
