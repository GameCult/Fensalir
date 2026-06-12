using System.Numerics;

namespace Aquarium.Engine.Render;

public static class BokushoBrushSimulation
{
    public static BokushoBrushSimulationResult Evaluate(AquariumBokushoBrushFrame source)
    {
        var frame = source.HasInput ? source.Normalized() : AquariumBokushoBrushFrame.Empty;
        if (!frame.HasInput)
        {
            return BokushoBrushSimulationResult.Empty;
        }

        var sampleCount = Math.Clamp(frame.SampleCount, 2, 4096);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, 4096);
        var trace = new float[checked(sampleCount * tuftCount)];
        var canvas = new float[trace.Length];
        Evaluate(frame, trace, canvas);
        return new BokushoBrushSimulationResult(sampleCount, tuftCount, trace, canvas);
    }

    public static void Evaluate(AquariumBokushoBrushFrame source, Span<float> trace, Span<float> canvas)
    {
        var frame = source.Normalized();
        var sampleCount = Math.Clamp(frame.SampleCount, 2, 4096);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, 4096);
        var valueCount = checked(sampleCount * tuftCount);
        if (trace.Length < valueCount)
        {
            throw new ArgumentException("Trace buffer is smaller than the brush simulation frame.", nameof(trace));
        }

        if (canvas.Length < valueCount)
        {
            throw new ArgumentException("Canvas buffer is smaller than the brush simulation frame.", nameof(canvas));
        }

        var pressure = Saturate(frame.Pressure * 0.5f) * 2.0f;
        var wetness = Saturate(frame.Wetness / 1.6f);
        var load = Saturate(frame.InkLoad / 2.0f);
        var splay = Saturate(frame.Splay / 2.0f);
        var bend = Saturate(frame.Bend / 2.4f);
        var friction = Saturate(frame.Friction);
        var radius = MathF.Max(frame.BrushRadius, 0.0001f);

        for (var tuft = 0; tuft < tuftCount; tuft++)
        {
            var laneT = tuftCount <= 1 ? 0.5f : tuft / (float)(tuftCount - 1);
            var restOffset = laneT * 2.0f - 1.0f;
            var edge = MathF.Abs(restOffset);
            var tip = StrokePoint(frame, 0.0f);
            var offset = restOffset * radius * (0.42f + splay * 0.38f);
            var stateLoad = load * (1.0f - edge * 0.36f);
            var stateWet = wetness * (0.86f + (1.0f - edge) * 0.14f);
            var shed = 0.0f;

            for (var sample = 0; sample < sampleCount; sample++)
            {
                var t = sampleCount <= 1 ? 0.0f : sample / (float)(sampleCount - 1);
                var center = StrokePoint(frame, t);
                var tangent = StrokeTangent(frame, t, sampleCount);
                var normal = new Vector2(-tangent.Y, tangent.X);
                var turn = MathF.Sin(t * MathF.Tau + 0.0f);
                var targetOffset = restOffset * radius * (0.38f + splay * 0.52f + pressure * 0.08f - wetness * 0.10f) + turn * radius * 0.14f * (1.0f - edge);
                var recovery = Saturate(0.08f + stateWet * 0.12f + pressure * 0.08f + (1.0f - edge) * 0.07f);
                offset = Lerp(offset, targetOffset, recovery);

                var lag = radius * (0.12f + bend * 0.72f + friction * pressure * 0.34f + edge * 0.18f);
                var desiredTip = center + normal * offset - tangent * lag;
                var slip = desiredTip - tip;
                var contact = Saturate(pressure * stateWet * stateLoad * (0.70f + (1.0f - edge) * 0.26f));
                var drag = contact * friction * (0.38f + stateWet * 0.22f + edge * 0.18f);
                tip += slip * (1.0f - drag);

                var velocity = slip.Length() * frame.PhysicsHz / MathF.Max(radius, 0.001f);
                var tension = Saturate(MathF.Abs(targetOffset - offset) / MathF.Max(radius, 0.001f) * 0.38f + velocity * 0.018f + drag * 0.46f);
                var separation = Saturate(edge * 0.22f + tension * 0.34f + velocity * 0.010f - stateWet * 0.16f);
                var adhesion = Saturate(stateWet * (0.52f + pressure * 0.20f) - separation * 0.18f - tension * 0.08f);
                var deposition = contact * stateLoad * stateWet * Saturate(0.10f + drag * 0.72f + velocity * 0.012f) * (0.82f + separation * 0.22f);
                stateLoad = MathF.Max(0.0f, stateLoad - deposition * (0.040f + pressure * 0.025f));
                stateWet = MathF.Max(0.0f, stateWet - deposition * 0.010f);
                shed += deposition;

                var index = tuft * sampleCount + sample;
                trace[index] = Saturate(contact * (0.30f + stateLoad * 0.42f + adhesion * 0.20f) + deposition * 1.8f);
                canvas[index] = Saturate(shed);
            }
        }
    }

    private static Vector2 StrokePoint(AquariumBokushoBrushFrame frame, float t)
    {
        return CatmullRom(
            new Vector2(frame.StrokeP0.X, frame.StrokeP0.Y),
            new Vector2(frame.StrokeP1.X, frame.StrokeP1.Y),
            new Vector2(frame.StrokeP2.X, frame.StrokeP2.Y),
            new Vector2(frame.StrokeP3.X, frame.StrokeP3.Y),
            t);
    }

    private static Vector2 StrokeTangent(AquariumBokushoBrushFrame frame, float t, int sampleCount)
    {
        var dt = 1.0f / MathF.Max(sampleCount - 1.0f, 1.0f);
        var before = StrokePoint(frame, Saturate(t - dt));
        var after = StrokePoint(frame, Saturate(t + dt));
        return Normalize(after - before);
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * ((2.0f * p1) +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3);
    }

    private static Vector2 Normalize(Vector2 value)
    {
        var length = value.Length();
        return length > 0.000001f && float.IsFinite(length) ? value / length : Vector2.Zero;
    }

    private static float Lerp(float left, float right, float t) => left + (right - left) * t;

    private static float Saturate(float value) => Math.Clamp(value, 0.0f, 1.0f);
}

public sealed record BokushoBrushSimulationResult(int SampleCount, int TuftCount, float[] Trace, float[] Canvas)
{
    public static BokushoBrushSimulationResult Empty { get; } = new(0, 0, [], []);

    public bool HasSamples => Trace.Length > 0 && Canvas.Length == Trace.Length;
}
