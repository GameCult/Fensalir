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
}
