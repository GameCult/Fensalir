using System.Numerics;
using Aquarium.Engine.Render;

namespace Aquarium.Engine.Tests;

public sealed class BokushoBrushSimulationTests
{
    [Fact]
    public void CpuBrushSimulationProducesDeterministicTraceAndCanvasFields()
    {
        var frame = new AquariumBokushoBrushFrame
        {
            TuftCount = 9,
            SampleCount = 32,
            PhysicsHz = 500.0f,
            BrushRadius = 2.2f,
            Pressure = 0.84f,
            InkLoad = 1.12f,
            Wetness = 0.92f,
            Splay = 0.86f,
            Bend = 0.72f,
            Friction = 0.66f,
            StrokeP0 = new Vector4(-7.2f, 0.95f, 0.0f, 0.0f),
            StrokeP1 = new Vector4(-2.8f, -1.95f, 0.0f, 0.0f),
            StrokeP2 = new Vector4(2.6f, -1.70f, 0.0f, 0.0f),
            StrokeP3 = new Vector4(7.0f, 0.82f, 0.0f, 0.0f),
        };

        var first = BokushoBrushSimulation.Evaluate(frame);
        var second = BokushoBrushSimulation.Evaluate(frame);

        Assert.True(first.HasSamples);
        Assert.Equal(32, first.SampleCount);
        Assert.Equal(9, first.TuftCount);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.Canvas, second.Canvas);
        Assert.All(first.Trace, value => Assert.InRange(value, 0.0f, 1.0f));
        Assert.All(first.Canvas, value => Assert.InRange(value, 0.0f, 1.0f));
    }

    [Fact]
    public void CpuBrushSimulationDepositsLocalPigmentPerTuftWithoutHistorySmear()
    {
        var frame = new AquariumBokushoBrushFrame
        {
            TuftCount = 7,
            SampleCount = 48,
            PhysicsHz = 500.0f,
            Pressure = 0.76f,
            InkLoad = 1.24f,
            Wetness = 0.88f,
            Splay = 0.74f,
            Bend = 0.68f,
            Friction = 0.62f,
        };

        var result = BokushoBrushSimulation.Evaluate(frame);

        for (var tuft = 0; tuft < result.TuftCount; tuft++)
        {
            var variation = 0.0f;
            for (var sample = 0; sample < result.SampleCount; sample++)
            {
                var current = result.Canvas[tuft * result.SampleCount + sample];
                Assert.InRange(current, 0.0f, 1.0f);
                if (sample > 0)
                {
                    var previous = result.Canvas[tuft * result.SampleCount + sample - 1];
                    variation += MathF.Abs(current - previous);
                }
            }

            Assert.True(variation > 0.0001f, $"Canvas pigment for tuft {tuft} collapsed to an unchanging history field.");
        }

        var centerTuft = result.TuftCount / 2;
        var centerCanvas = result.Canvas
            .Skip(centerTuft * result.SampleCount)
            .Take(result.SampleCount)
            .Max();
        var edgeCanvas = result.Canvas.Take(result.SampleCount).Max();
        Assert.True(centerCanvas > edgeCanvas);
    }

    [Fact]
    public void CpuBrushSimulationProjectsDepositedCanvasToPageField()
    {
        var frame = new AquariumBokushoBrushFrame
        {
            TuftCount = 11,
            SampleCount = 64,
            PhysicsHz = 500.0f,
            BrushRadius = 2.0f,
            Pressure = 0.92f,
            InkLoad = 1.36f,
            Wetness = 0.94f,
            Splay = 0.82f,
            Bend = 0.78f,
            Friction = 0.68f,
            StrokeP0 = new Vector4(-7.0f, 0.6f, 0.0f, 0.0f),
            StrokeP1 = new Vector4(-2.6f, -1.8f, 0.0f, 0.0f),
            StrokeP2 = new Vector4(2.4f, -1.6f, 0.0f, 0.0f),
            StrokeP3 = new Vector4(7.0f, 0.7f, 0.0f, 0.0f),
        };
        var result = BokushoBrushSimulation.Evaluate(frame);

        var page = BokushoBrushSimulation.ProjectCanvasToPage(
            frame,
            result.Canvas,
            width: 96,
            height: 96,
            viewCenter: Vector2.Zero,
            viewRadius: 8.0f);

        var max = page.Max();
        var offStrokeCorner = page[0];

        Assert.True(max > 0.01f);
        Assert.True(max > offStrokeCorner * 8.0f);
        Assert.Contains(page, value => value > 0.0f);
    }

    [Fact]
    public void CpuBrushProfileControlsPageFootprint()
    {
        var narrow = new AquariumBokushoBrushFrame
        {
            TuftCount = 9,
            SampleCount = 48,
            PhysicsHz = 500.0f,
            BrushRadius = 1.8f,
            Pressure = 0.86f,
            InkLoad = 1.18f,
            Wetness = 0.88f,
            NormalScale = 0.55f,
            TangentScale = 1.10f,
        };
        var wide = new AquariumBokushoBrushFrame
        {
            TuftCount = narrow.TuftCount,
            SampleCount = narrow.SampleCount,
            PhysicsHz = narrow.PhysicsHz,
            BrushRadius = narrow.BrushRadius,
            Pressure = narrow.Pressure,
            InkLoad = narrow.InkLoad,
            Wetness = narrow.Wetness,
            NormalScale = 1.45f,
            TangentScale = narrow.TangentScale,
        };
        var narrowResult = BokushoBrushSimulation.Evaluate(narrow);
        var wideResult = BokushoBrushSimulation.Evaluate(wide);

        var narrowPage = BokushoBrushSimulation.ProjectCanvasToPage(narrow, narrowResult.Canvas, 64, 64, Vector2.Zero, 8.0f);
        var widePage = BokushoBrushSimulation.ProjectCanvasToPage(wide, wideResult.Canvas, 64, 64, Vector2.Zero, 8.0f);
        var narrowCoverage = narrowPage.Count(value => value > 0.001f);
        var wideCoverage = widePage.Count(value => value > 0.001f);

        Assert.True(wideCoverage > narrowCoverage);
    }

    [Fact]
    public void CpuBrushSimulationProjectsExplicitStrokePacketsToSharedPage()
    {
        var frame = new AquariumBokushoBrushFrame
        {
            TuftCount = 9,
            SampleCount = 48,
            PhysicsHz = 500.0f,
            BrushRadius = 1.5f,
            Pressure = 0.90f,
            InkLoad = 1.22f,
            Wetness = 0.90f,
            Splay = 0.78f,
            Bend = 0.70f,
            Friction = 0.64f,
            Strokes =
            [
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-5.5f, 1.2f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(-3.2f, 0.8f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(-0.4f, 0.8f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(1.8f, 1.1f, 0.0f, 0.0f),
                    RadiusScale = 0.78f,
                    PressureScale = 0.82f,
                    NormalScale = 0.70f,
                    TangentScale = 1.10f,
                },
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(1.4f, 1.0f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(0.4f, 0.2f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(-0.2f, -1.6f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(-1.5f, -2.6f, 0.0f, 0.0f),
                    RadiusScale = 1.08f,
                    PressureScale = 1.12f,
                    NormalScale = 0.86f,
                    TangentScale = 1.24f,
                },
            ],
        };

        var result = BokushoBrushSimulation.Evaluate(frame);
        var page = BokushoBrushSimulation.ProjectCanvasToPage(frame, result.Canvas, 96, 96, Vector2.Zero, 6.0f);

        Assert.Equal(2, result.StrokeCount);
        Assert.Equal(frame.SampleCount * frame.TuftCount * 2, result.Canvas.Length);
        Assert.True(SamplePage(page, 96, 96, new Vector2(-2.6f, 0.8f), 6.0f) > 0.001f);
        Assert.True(SamplePage(page, 96, 96, new Vector2(0.0f, -1.0f), 6.0f) > 0.001f);
    }

    [Fact]
    public void CpuBrushStrokeDynamicsControlPigmentAndTaper()
    {
        var faint = StrokeDynamicsFrame(pigmentScale: 0.35f, entryTaper: 0.24f);
        var strong = StrokeDynamicsFrame(pigmentScale: 1.65f, entryTaper: 0.04f);

        var faintResult = BokushoBrushSimulation.Evaluate(faint);
        var strongResult = BokushoBrushSimulation.Evaluate(strong);
        var faintPage = BokushoBrushSimulation.ProjectCanvasToPage(faint, faintResult.Canvas, 96, 96, Vector2.Zero, 4.0f);
        var strongPage = BokushoBrushSimulation.ProjectCanvasToPage(strong, strongResult.Canvas, 96, 96, Vector2.Zero, 4.0f);

        Assert.True(strongPage.Max() > faintPage.Max() * 1.8f);
        Assert.True(strongPage.Sum() > faintPage.Sum() * 1.45f);
    }

    [Fact]
    public void CpuBrushPageProjectionAppliesDryPaperEdgeBreakup()
    {
        var dry = new AquariumBokushoBrushFrame
        {
            TuftCount = 13,
            SampleCount = 64,
            PhysicsHz = 500.0f,
            BrushRadius = 1.8f,
            Pressure = 0.64f,
            InkLoad = 1.0f,
            Wetness = 0.0f,
            Splay = 0.88f,
            Strokes =
            [
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-3.2f, 0.0f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(-2.0f, 0.0f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(2.0f, 0.0f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(3.2f, 0.0f, 0.0f, 0.0f),
                    RadiusScale = 1.0f,
                    PressureScale = 1.0f,
                    NormalScale = 0.84f,
                    TangentScale = 1.0f,
                    EntryTaper = 0.08f,
                    ExitTaper = 0.08f,
                    PigmentScale = 1.0f,
                    SplitScale = 1.5f,
                },
            ],
        };
        var wet = new AquariumBokushoBrushFrame
        {
            TuftCount = dry.TuftCount,
            SampleCount = dry.SampleCount,
            PhysicsHz = dry.PhysicsHz,
            BrushRadius = dry.BrushRadius,
            Pressure = dry.Pressure,
            InkLoad = dry.InkLoad,
            Wetness = 1.6f,
            Splay = dry.Splay,
            Strokes = dry.Strokes,
        };
        var canvas = Enumerable.Repeat(1.0f, dry.TuftCount * dry.SampleCount).ToArray();

        var dryPage = BokushoBrushSimulation.ProjectCanvasToPage(dry, canvas, 96, 96, Vector2.Zero, 4.0f);
        var wetPage = BokushoBrushSimulation.ProjectCanvasToPage(wet, canvas, 96, 96, Vector2.Zero, 4.0f);
        var edge = new Vector2(0.0f, 0.66f);

        Assert.True(wetPage.Sum() > dryPage.Sum() * 1.05f);
        Assert.True(SamplePage(wetPage, 96, 96, edge, 4.0f) > SamplePage(dryPage, 96, 96, edge, 4.0f));
    }

    [Fact]
    public void CpuBrushPageProjectionNarrowsTaperedStrokeEnds()
    {
        var frame = new AquariumBokushoBrushFrame
        {
            TuftCount = 11,
            SampleCount = 80,
            PhysicsHz = 500.0f,
            BrushRadius = 1.8f,
            Pressure = 0.84f,
            InkLoad = 1.0f,
            Wetness = 1.2f,
            Splay = 0.72f,
            Strokes =
            [
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-3.2f, 0.0f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(-2.1f, 0.0f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(2.1f, 0.0f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(3.2f, 0.0f, 0.0f, 0.0f),
                    RadiusScale = 1.0f,
                    PressureScale = 1.0f,
                    NormalScale = 0.80f,
                    TangentScale = 1.0f,
                    EntryTaper = 0.20f,
                    ExitTaper = 0.20f,
                    PigmentScale = 1.0f,
                    SplitScale = 1.0f,
                },
            ],
        };
        var canvas = Enumerable.Repeat(1.0f, frame.TuftCount * frame.SampleCount).ToArray();

        var page = BokushoBrushSimulation.ProjectCanvasToPage(frame, canvas, 128, 128, Vector2.Zero, 4.0f);
        var middleShoulder = SamplePage(page, 128, 128, new Vector2(0.0f, 0.46f), 4.0f);
        var entryShoulder = SamplePage(page, 128, 128, new Vector2(-2.1f, 0.46f), 4.0f);

        Assert.True(middleShoulder > entryShoulder * 1.8f);
    }

    [Fact]
    public void CpuBrushLaneCohesionPreservesWetCenterPigment()
    {
        var wet = LaneCohesionFrame(wetness: 1.35f);
        var dry = LaneCohesionFrame(wetness: 0.32f);

        var wetResult = BokushoBrushSimulation.Evaluate(wet);
        var dryResult = BokushoBrushSimulation.Evaluate(dry);
        var centerTuft = wetResult.TuftCount / 2;
        var wetCenter = TuftSum(wetResult.Canvas, wetResult.SampleCount, centerTuft);
        var wetEdge = TuftSum(wetResult.Canvas, wetResult.SampleCount, 0);
        var dryCenter = TuftSum(dryResult.Canvas, dryResult.SampleCount, centerTuft);
        var sustainedWetSamples = wetResult.Canvas
            .Skip(centerTuft * wetResult.SampleCount)
            .Take(wetResult.SampleCount)
            .Count(value => value > 0.06f);

        Assert.True(wetCenter > dryCenter * 1.25f);
        Assert.True(wetCenter > wetEdge * 1.8f);
        Assert.True(sustainedWetSamples > wetResult.SampleCount / 3);
    }

    [Fact]
    public void CpuBrushLaneCoreCarriesLoadedBodyWhileEdgesBreakDry()
    {
        var frame = LaneCohesionFrame(wetness: 0.68f, inkLoad: 1.46f, splay: 1.08f);

        var result = BokushoBrushSimulation.Evaluate(frame);
        var centerTuft = result.TuftCount / 2;
        var edgeTuft = 0;
        var center = result.Canvas.Skip(centerTuft * result.SampleCount).Take(result.SampleCount).ToArray();
        var edge = result.Canvas.Skip(edgeTuft * result.SampleCount).Take(result.SampleCount).ToArray();
        var centerSum = center.Sum();
        var edgeSum = edge.Sum();
        var centerBodySamples = center.Count(value => value > 0.10f);
        var edgeDryBreaks = edge.Count(value => value < 0.035f);

        Assert.True(centerSum > edgeSum * 2.2f);
        Assert.True(centerBodySamples > result.SampleCount / 3);
        Assert.True(edgeDryBreaks > result.SampleCount / 2);
    }

    [Fact]
    public void CpuBrushSourceStrokeIdPreventsAccidentalSegmentReplay()
    {
        var shared = SourceIdentityFrame(sameSourceId: true);
        var split = SourceIdentityFrame(sameSourceId: false);

        var sharedResult = BokushoBrushSimulation.Evaluate(shared);
        var splitResult = BokushoBrushSimulation.Evaluate(split);
        var secondStrokeOffset = sharedResult.SampleCount * sharedResult.TuftCount;
        var sharedSecond = sharedResult.Canvas.Skip(secondStrokeOffset).Sum();
        var splitSecond = splitResult.Canvas.Skip(secondStrokeOffset).Sum();

        Assert.True(MathF.Abs(sharedSecond - splitSecond) > 0.05f);
    }

    [Fact]
    public void CpuBrushSourceStrokeIdKeepsFanPhaseContinuousAcrossSegments()
    {
        var shared = SourceIdentityFrame(sameSourceId: true);
        var split = SourceIdentityFrame(sameSourceId: false);

        var sharedResult = BokushoBrushSimulation.Evaluate(shared);
        var splitResult = BokushoBrushSimulation.Evaluate(split);
        var centerTuft = sharedResult.TuftCount / 2;
        var sharedJump = TipJumpAtSecondSegmentStart(sharedResult, centerTuft);
        var splitJump = TipJumpAtSecondSegmentStart(splitResult, centerTuft);

        Assert.True(sharedJump < splitJump * 1.25f);
    }

    [Fact]
    public void CpuBrushProjectionUsesSourceStrokePaperResponseAcrossSegments()
    {
        var shared = SourceIdentityFrame(sameSourceId: true);
        var split = SourceIdentityFrame(sameSourceId: false);
        var sharedResult = BokushoBrushSimulation.Evaluate(shared);
        var splitResult = BokushoBrushSimulation.Evaluate(split);

        var sharedPage = BokushoBrushSimulation.ProjectCanvasToPage(shared, sharedResult.Canvas, sharedResult.Tips, 128, 128, Vector2.Zero, 4.0f);
        var splitPage = BokushoBrushSimulation.ProjectCanvasToPage(split, splitResult.Canvas, splitResult.Tips, 128, 128, Vector2.Zero, 4.0f);
        var sharedJoin = SamplePage(sharedPage, 128, 128, new Vector2(0.0f, 0.0f), 4.0f);
        var splitJoin = SamplePage(splitPage, 128, 128, new Vector2(0.0f, 0.0f), 4.0f);

        Assert.True(sharedJoin > 0.0f);
        Assert.True(MathF.Abs(sharedJoin - splitJoin) > 0.0001f);
    }

    [Fact]
    public void CpuBrushProjectionBorrowsSameSourceSamplesAcrossSegmentEdges()
    {
        var shared = SourceIdentityFrame(sameSourceId: true);
        var split = SourceIdentityFrame(sameSourceId: false);
        var sharedResult = BokushoBrushSimulation.Evaluate(shared);
        var splitResult = BokushoBrushSimulation.Evaluate(split);
        var sharedCanvas = LastSampleOnly(sharedResult.Canvas, sharedResult.SampleCount, sharedResult.TuftCount);
        var splitCanvas = LastSampleOnly(splitResult.Canvas, splitResult.SampleCount, splitResult.TuftCount);

        var sharedPage = BokushoBrushSimulation.ProjectCanvasToPage(shared, sharedCanvas, sharedResult.Tips, 160, 160, Vector2.Zero, 4.0f);
        var splitPage = BokushoBrushSimulation.ProjectCanvasToPage(split, splitCanvas, splitResult.Tips, 160, 160, Vector2.Zero, 4.0f);
        var sharedJoin = SamplePage(sharedPage, 160, 160, new Vector2(0.03f, 0.0f), 4.0f);
        var splitJoin = SamplePage(splitPage, 160, 160, new Vector2(0.03f, 0.0f), 4.0f);

        Assert.True(sharedJoin > 0.0f);
        Assert.True(sharedJoin > splitJoin * 1.05f, $"shared join={sharedJoin:0.000000}; split join={splitJoin:0.000000}");
    }

    [Fact]
    public void CpuBrushProjectionTreatsSameSourceSegmentEdgesAsThroughPoints()
    {
        var shared = SourceIdentityFrame(sameSourceId: true);
        var split = SourceIdentityFrame(sameSourceId: false);
        var sharedResult = BokushoBrushSimulation.Evaluate(shared);
        var splitResult = BokushoBrushSimulation.Evaluate(split);

        var sharedPage = BokushoBrushSimulation.ProjectCanvasToPage(shared, sharedResult.Canvas, sharedResult.Tips, 160, 160, Vector2.Zero, 4.0f);
        var splitPage = BokushoBrushSimulation.ProjectCanvasToPage(split, splitResult.Canvas, splitResult.Tips, 160, 160, Vector2.Zero, 4.0f);
        var sharedJoin = SamplePage(sharedPage, 160, 160, new Vector2(0.0f, 0.0f), 4.0f);
        var splitJoin = SamplePage(splitPage, 160, 160, new Vector2(0.0f, 0.0f), 4.0f);

        Assert.True(sharedJoin > 0.0f);
        Assert.True(sharedJoin < splitJoin, $"shared join={sharedJoin:0.000000}; split join={splitJoin:0.000000}");
    }

    [Fact]
    public void CpuBrushSegmentSpanKeepsLaterFittedPiecesLoaded()
    {
        var frame = SourceIdentityFrame(sameSourceId: true);
        var result = BokushoBrushSimulation.Evaluate(frame);
        var firstStrokeInk = StrokeSum(result.Canvas, result.SampleCount, result.TuftCount, stroke: 0);
        var secondStrokeInk = StrokeSum(result.Canvas, result.SampleCount, result.TuftCount, stroke: 1);

        Assert.True(secondStrokeInk > firstStrokeInk * 0.25f, $"first={firstStrokeInk:0.000000}; second={secondStrokeInk:0.000000}");
    }

    [Fact]
    public void CpuBrushGeometryEnrichesCurvedStrokePressureAndWidth()
    {
        var straight = GestureFrame(curved: false);
        var curved = GestureFrame(curved: true);

        var straightResult = BokushoBrushSimulation.Evaluate(straight);
        var curvedResult = BokushoBrushSimulation.Evaluate(curved);
        var straightPage = BokushoBrushSimulation.ProjectCanvasToPage(straight, straightResult.Canvas, 128, 128, Vector2.Zero, 4.0f);
        var curvedPage = BokushoBrushSimulation.ProjectCanvasToPage(curved, curvedResult.Canvas, 128, 128, Vector2.Zero, 4.0f);
        var straightCoverage = straightPage.Count(value => value > 0.004f);
        var curvedCoverage = curvedPage.Count(value => value > 0.004f);

        Assert.True(curvedResult.Canvas.Sum() > straightResult.Canvas.Sum() * 1.08f);
        Assert.True(curvedCoverage > straightCoverage);
    }

    [Fact]
    public void CpuBrushPoseControlsStrokeSpreadAndPigment()
    {
        var neutral = PoseFrame(tilt: 0.0f, rotation: 0.0f, gripHeight: 1.0f, compliance: 1.0f);
        var expressive = PoseFrame(tilt: 0.85f, rotation: 0.65f, gripHeight: 0.58f, compliance: 1.65f);

        var neutralResult = BokushoBrushSimulation.Evaluate(neutral);
        var expressiveResult = BokushoBrushSimulation.Evaluate(expressive);
        var neutralPage = BokushoBrushSimulation.ProjectCanvasToPage(neutral, neutralResult.Canvas, 128, 128, Vector2.Zero, 4.0f);
        var expressivePage = BokushoBrushSimulation.ProjectCanvasToPage(expressive, expressiveResult.Canvas, 128, 128, Vector2.Zero, 4.0f);
        var neutralCoverage = neutralPage.Count(value => value > 0.004f);
        var expressiveCoverage = expressivePage.Count(value => value > 0.004f);

        Assert.True(expressiveResult.Canvas.Sum() > neutralResult.Canvas.Sum() * 1.04f);
        Assert.True(expressiveCoverage > neutralCoverage);
    }

    private static AquariumBokushoBrushFrame StrokeDynamicsFrame(float pigmentScale, float entryTaper)
    {
        return new AquariumBokushoBrushFrame
        {
            TuftCount = 11,
            SampleCount = 56,
            PhysicsHz = 500.0f,
            BrushRadius = 1.4f,
            Pressure = 0.92f,
            InkLoad = 1.24f,
            Wetness = 0.90f,
            Splay = 0.82f,
            Bend = 0.74f,
            Friction = 0.66f,
            Strokes =
            [
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-2.8f, 0.1f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(-1.7f, 0.0f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(1.4f, 0.0f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(2.8f, -0.1f, 0.0f, 0.0f),
                    RadiusScale = 0.86f,
                    PressureScale = 1.0f,
                    NormalScale = 0.66f,
                    TangentScale = 1.2f,
                    EntryTaper = entryTaper,
                    ExitTaper = 0.18f,
                    PigmentScale = pigmentScale,
                    SplitScale = 1.0f,
                },
            ],
        };
    }

    private static AquariumBokushoBrushFrame GestureFrame(bool curved)
    {
        return new AquariumBokushoBrushFrame
        {
            TuftCount = 13,
            SampleCount = 84,
            PhysicsHz = 500.0f,
            BrushRadius = 1.25f,
            Pressure = 0.84f,
            InkLoad = 1.20f,
            Wetness = 1.0f,
            Splay = 0.78f,
            Bend = 0.72f,
            Friction = 0.66f,
            Strokes =
            [
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-2.8f, curved ? 0.8f : 0.0f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(-1.4f, curved ? -1.2f : 0.0f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(1.4f, curved ? 1.1f : 0.0f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(2.8f, curved ? -0.7f : 0.0f, 0.0f, 0.0f),
                    RadiusScale = 0.88f,
                    PressureScale = 1.0f,
                    NormalScale = 0.62f,
                    TangentScale = 1.12f,
                    EntryTaper = 0.08f,
                    ExitTaper = 0.24f,
                    PigmentScale = 1.0f,
                    SplitScale = 1.1f,
                },
            ],
        };
    }

    private static AquariumBokushoBrushFrame PoseFrame(float tilt, float rotation, float gripHeight, float compliance)
    {
        return new AquariumBokushoBrushFrame
        {
            TuftCount = 15,
            SampleCount = 84,
            PhysicsHz = 500.0f,
            BrushRadius = 1.16f,
            Pressure = 0.86f,
            InkLoad = 1.24f,
            Wetness = 1.08f,
            Splay = 0.84f,
            Bend = 0.76f,
            Friction = 0.68f,
            Strokes =
            [
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-2.8f, 0.4f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(-1.2f, -0.7f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(1.6f, 0.5f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(2.8f, -0.3f, 0.0f, 0.0f),
                    RadiusScale = 0.90f,
                    PressureScale = 1.0f,
                    NormalScale = 0.66f,
                    TangentScale = 1.18f,
                    ShaftTilt = tilt,
                    ShaftRotation = rotation,
                    GripHeight = gripHeight,
                    Compliance = compliance,
                    EntryTaper = 0.08f,
                    ExitTaper = 0.24f,
                    PigmentScale = 1.0f,
                    SplitScale = 1.25f,
                },
            ],
        };
    }

    private static AquariumBokushoBrushFrame LaneCohesionFrame(float wetness, float inkLoad = 1.32f, float splay = 0.92f)
    {
        return new AquariumBokushoBrushFrame
        {
            TuftCount = 15,
            SampleCount = 72,
            PhysicsHz = 500.0f,
            BrushRadius = 1.2f,
            Pressure = 0.88f,
            InkLoad = inkLoad,
            Wetness = wetness,
            Splay = splay,
            Bend = 0.78f,
            Friction = 0.70f,
            Strokes =
            [
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-3.2f, 0.2f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(-1.8f, -0.2f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(1.8f, -0.1f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(3.2f, 0.3f, 0.0f, 0.0f),
                    RadiusScale = 0.92f,
                    PressureScale = 1.0f,
                    NormalScale = 0.72f,
                    TangentScale = 1.16f,
                    EntryTaper = 0.08f,
                    ExitTaper = 0.22f,
                    PigmentScale = 1.0f,
                    SplitScale = 1.8f,
                },
            ],
        };
    }

    private static AquariumBokushoBrushFrame SourceIdentityFrame(bool sameSourceId)
    {
        var secondId = sameSourceId ? 7 : 8;
        return new AquariumBokushoBrushFrame
        {
            TuftCount = 9,
            SampleCount = 48,
            PhysicsHz = 500.0f,
            BrushRadius = 1.35f,
            Pressure = 0.92f,
            InkLoad = 1.28f,
            Wetness = 1.02f,
            Splay = 0.82f,
            Bend = 0.74f,
            Friction = 0.68f,
            Strokes =
            [
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-3.0f, 0.2f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(-2.0f, 0.0f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(-0.6f, 0.0f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(0.0f, 0.1f, 0.0f, 0.0f),
                    RadiusScale = 0.90f,
                    PressureScale = 1.0f,
                    NormalScale = 0.70f,
                    TangentScale = 1.08f,
                    SegmentStart = 0.0f,
                    SegmentEnd = 0.5f,
                    SourceStrokeId = 7,
                },
                new AquariumBokushoBrushStroke
                {
                    StrokeP0 = new Vector4(-0.1f, 0.1f, 0.0f, 0.0f),
                    StrokeP1 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f),
                    StrokeP2 = new Vector4(1.8f, 0.0f, 0.0f, 0.0f),
                    StrokeP3 = new Vector4(3.0f, -0.2f, 0.0f, 0.0f),
                    RadiusScale = 1.0f,
                    PressureScale = 1.0f,
                    NormalScale = 0.70f,
                    TangentScale = 1.08f,
                    SegmentStart = 0.5f,
                    SegmentEnd = 1.0f,
                    SourceStrokeId = secondId,
                },
            ],
        };
    }

    private static float TuftSum(float[] canvas, int sampleCount, int tuft)
    {
        return canvas.Skip(tuft * sampleCount).Take(sampleCount).Sum();
    }

    private static float StrokeSum(float[] canvas, int sampleCount, int tuftCount, int stroke)
    {
        return canvas.Skip(stroke * sampleCount * tuftCount).Take(sampleCount * tuftCount).Sum();
    }

    private static float[] LastSampleOnly(IReadOnlyList<float> canvas, int sampleCount, int tuftCount)
    {
        var sparse = new float[canvas.Count];
        for (var tuft = 0; tuft < tuftCount; tuft++)
        {
            var index = (tuft * sampleCount) + sampleCount - 1;
            sparse[index] = canvas[index];
        }

        return sparse;
    }

    private static float TipJumpAtSecondSegmentStart(BokushoBrushSimulationResult result, int tuft)
    {
        var lastFirstIndex = (tuft * result.SampleCount) + result.SampleCount - 1;
        var firstSecondIndex = ((result.TuftCount + tuft) * result.SampleCount);
        var first = result.Tips[lastFirstIndex];
        var second = result.Tips[firstSecondIndex];
        return Vector2.Distance(new Vector2(first.X, first.Y), new Vector2(second.X, second.Y));
    }

    private static float SamplePage(float[] page, int width, int height, Vector2 world, float viewRadius)
    {
        var uv = world / viewRadius * 0.5f + new Vector2(0.5f, 0.5f);
        var x = Math.Clamp((int)MathF.Round(uv.X * (width - 1)), 0, width - 1);
        var y = Math.Clamp((int)MathF.Round(uv.Y * (height - 1)), 0, height - 1);
        return page[y * width + x];
    }
}
