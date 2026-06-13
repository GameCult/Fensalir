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
        Assert.True(SamplePage(strongPage, 96, 96, new Vector2(-1.8f, 0.0f), 4.0f) > SamplePage(faintPage, 96, 96, new Vector2(-1.8f, 0.0f), 4.0f));
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

    private static float SamplePage(float[] page, int width, int height, Vector2 world, float viewRadius)
    {
        var uv = world / viewRadius * 0.5f + new Vector2(0.5f, 0.5f);
        var x = Math.Clamp((int)MathF.Round(uv.X * (width - 1)), 0, width - 1);
        var y = Math.Clamp((int)MathF.Round(uv.Y * (height - 1)), 0, height - 1);
        return page[y * width + x];
    }
}
