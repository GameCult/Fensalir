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
    public void CpuBrushSimulationAccumulatesPigmentPerTuftWithoutRetroactiveRepair()
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
            var previous = 0.0f;
            for (var sample = 0; sample < result.SampleCount; sample++)
            {
                var current = result.Canvas[tuft * result.SampleCount + sample];
                Assert.True(current >= previous, $"Canvas pigment for tuft {tuft} sample {sample} moved backward.");
                previous = current;
            }
        }

        var centerTuft = result.TuftCount / 2;
        var centerCanvas = result.Canvas[centerTuft * result.SampleCount + result.SampleCount - 1];
        var edgeCanvas = result.Canvas[result.SampleCount - 1];
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
}
