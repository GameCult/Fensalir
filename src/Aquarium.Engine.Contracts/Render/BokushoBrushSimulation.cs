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
        var strokeCount = EffectiveStrokes(frame).Length;
        var trace = new float[checked(sampleCount * tuftCount * strokeCount)];
        var canvas = new float[trace.Length];
        Evaluate(frame, trace, canvas);
        return new BokushoBrushSimulationResult(sampleCount, tuftCount, strokeCount, trace, canvas);
    }

    public static void Evaluate(AquariumBokushoBrushFrame source, Span<float> trace, Span<float> canvas)
    {
        var frame = source.Normalized();
        var sampleCount = Math.Clamp(frame.SampleCount, 2, 4096);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, 4096);
        var strokes = EffectiveStrokes(frame);
        var valueCount = checked(sampleCount * tuftCount * strokes.Length);
        if (trace.Length < valueCount)
        {
            throw new ArgumentException("Trace buffer is smaller than the brush simulation frame.", nameof(trace));
        }

        if (canvas.Length < valueCount)
        {
            throw new ArgumentException("Canvas buffer is smaller than the brush simulation frame.", nameof(canvas));
        }

        var wetness = Saturate(frame.Wetness / 1.6f);
        var load = Saturate(frame.InkLoad / 2.0f);
        var splay = Saturate(frame.Splay / 2.0f);
        var bend = Saturate(frame.Bend / 2.4f);
        var friction = Saturate(frame.Friction);

        for (var strokeIndex = 0; strokeIndex < strokes.Length; strokeIndex++)
        {
            var stroke = strokes[strokeIndex];
            var pressure = Saturate(frame.Pressure * stroke.PressureScale * 0.5f) * 2.0f;
            var radius = MathF.Max(frame.BrushRadius * stroke.RadiusScale, 0.0001f);
            var normalRadius = MathF.Max(radius * stroke.NormalScale, 0.0001f);
            var tangentRadius = MathF.Max(radius * stroke.TangentScale, 0.0001f);

            for (var tuft = 0; tuft < tuftCount; tuft++)
            {
                var laneT = tuftCount <= 1 ? 0.5f : tuft / (float)(tuftCount - 1);
                var restOffset = laneT * 2.0f - 1.0f;
                var edge = MathF.Abs(restOffset);
                var laneHash = LaneHash(strokeIndex, tuft, 17);
                var laneLoad = 0.76f + laneHash * 0.34f;
                var split = Saturate((0.20f - LaneHash(strokeIndex, tuft, 53)) * 4.0f) * Saturate((edge - 0.18f) * 1.7f);
                var tip = StrokePoint(stroke, 0.0f);
                var offset = restOffset * normalRadius * (0.42f + splay * 0.38f) + (laneHash - 0.5f) * normalRadius * 0.05f;
                var stateLoad = load * (1.0f - edge * 0.36f) * laneLoad * (1.0f - split * 0.58f);
                var stateWet = wetness * (0.86f + (1.0f - edge) * 0.14f);

                for (var sample = 0; sample < sampleCount; sample++)
                {
                    var t = sampleCount <= 1 ? 0.0f : sample / (float)(sampleCount - 1);
                    var center = StrokePoint(stroke, t);
                    var tangent = StrokeTangent(stroke, t, sampleCount);
                    var normal = new Vector2(-tangent.Y, tangent.X);
                    var taper = StrokeTaper(t);
                    var localPressure = pressure * taper;
                    var localNormalRadius = MathF.Max(normalRadius * (0.34f + taper * 0.66f), 0.0001f);
                    var localTangentRadius = MathF.Max(tangentRadius * (0.48f + taper * 0.52f), 0.0001f);
                    var turn = MathF.Sin(t * MathF.Tau + strokeIndex * 0.37f);
                    var targetOffset = restOffset * localNormalRadius * (0.38f + splay * 0.52f + localPressure * 0.08f - wetness * 0.10f) + turn * localNormalRadius * 0.14f * (1.0f - edge);
                    var recovery = Saturate(0.08f + stateWet * 0.12f + localPressure * 0.08f + (1.0f - edge) * 0.07f);
                    offset = Lerp(offset, targetOffset, recovery);

                    var lag = localTangentRadius * (0.12f + bend * 0.72f + friction * localPressure * 0.34f + edge * 0.18f);
                    var desiredTip = center + normal * offset - tangent * lag;
                    var slip = desiredTip - tip;
                    var contact = Saturate(localPressure * stateWet * stateLoad * (0.70f + (1.0f - edge) * 0.26f));
                    var drag = contact * friction * (0.38f + stateWet * 0.22f + edge * 0.18f);
                    tip += slip * (1.0f - drag);

                    var velocity = slip.Length() * frame.PhysicsHz / MathF.Max(radius, 0.001f);
                    var tension = Saturate(MathF.Abs(targetOffset - offset) / MathF.Max(localNormalRadius, 0.001f) * 0.38f + velocity * 0.018f + drag * 0.46f);
                    var separation = Saturate(edge * 0.22f + tension * 0.34f + velocity * 0.010f - stateWet * 0.16f);
                    var adhesion = Saturate(stateWet * (0.52f + localPressure * 0.20f) - separation * 0.18f - tension * 0.08f);
                    var deposition = contact * stateLoad * stateWet * Saturate(0.10f + drag * 0.72f + velocity * 0.012f) * (0.82f + separation * 0.22f);
                    stateLoad = MathF.Max(0.0f, stateLoad - deposition * (0.040f + localPressure * 0.025f));
                    stateWet = MathF.Max(0.0f, stateWet - deposition * 0.010f);

                    var index = ((strokeIndex * tuftCount) + tuft) * sampleCount + sample;
                    var localPigment = Saturate(deposition * (7.5f + contact * 2.5f) + contact * stateLoad * stateWet * 0.22f + adhesion * contact * 0.08f);
                    trace[index] = Saturate(contact * (0.30f + stateLoad * 0.42f + adhesion * 0.20f) + deposition * 1.8f);
                    canvas[index] = localPigment;
                }
            }
        }
    }

    public static float[] ProjectCanvasToPage(
        AquariumBokushoBrushFrame source,
        ReadOnlySpan<float> canvas,
        int width,
        int height,
        Vector2 viewCenter,
        float viewRadius)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var frame = source.Normalized();
        var sampleCount = Math.Clamp(frame.SampleCount, 2, 4096);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, 4096);
        var strokes = EffectiveStrokes(frame);
        var valueCount = checked(sampleCount * tuftCount * strokes.Length);
        if (canvas.Length < valueCount)
        {
            throw new ArgumentException("Canvas buffer is smaller than the brush simulation frame.", nameof(canvas));
        }

        var page = new float[checked(width * height)];
        var safeRadius = MathF.Max(viewRadius, 0.001f);
        for (var y = 0; y < height; y++)
        {
            var uvY = height <= 1 ? 0.0f : y / (float)(height - 1);
            for (var x = 0; x < width; x++)
            {
                var uvX = width <= 1 ? 0.0f : x / (float)(width - 1);
                var world = viewCenter + (new Vector2(uvX, uvY) * 2.0f - Vector2.One) * safeRadius;
                var value = 0.0f;
                for (var strokeIndex = 0; strokeIndex < strokes.Length; strokeIndex++)
                {
                    value += ProjectCanvasSample(frame, strokes[strokeIndex], canvas, strokeIndex, world);
                }

                page[y * width + x] = Saturate(value);
            }
        }

        return page;
    }

    private static AquariumBokushoBrushStroke[] EffectiveStrokes(AquariumBokushoBrushFrame frame)
    {
        if (frame.Strokes.Count > 0)
        {
            return frame.Strokes.Select(stroke => stroke.Normalized()).ToArray();
        }

        return
        [
            new AquariumBokushoBrushStroke
            {
                StrokeP0 = frame.StrokeP0,
                StrokeP1 = frame.StrokeP1,
                StrokeP2 = frame.StrokeP2,
                StrokeP3 = frame.StrokeP3,
                RadiusScale = frame.RadiusScale,
                PressureScale = frame.PressureScale,
                NormalScale = frame.NormalScale,
                TangentScale = frame.TangentScale,
            }.Normalized()
        ];
    }

    private static Vector2 StrokePoint(AquariumBokushoBrushStroke stroke, float t)
    {
        return CatmullRom(
            new Vector2(stroke.StrokeP0.X, stroke.StrokeP0.Y),
            new Vector2(stroke.StrokeP1.X, stroke.StrokeP1.Y),
            new Vector2(stroke.StrokeP2.X, stroke.StrokeP2.Y),
            new Vector2(stroke.StrokeP3.X, stroke.StrokeP3.Y),
            t);
    }

    private static Vector2 StrokeTangent(AquariumBokushoBrushStroke stroke, float t, int sampleCount)
    {
        var dt = 1.0f / MathF.Max(sampleCount - 1.0f, 1.0f);
        var before = StrokePoint(stroke, Saturate(t - dt));
        var after = StrokePoint(stroke, Saturate(t + dt));
        return Normalize(after - before);
    }

    private static float ProjectCanvasSample(AquariumBokushoBrushFrame frame, AquariumBokushoBrushStroke stroke, ReadOnlySpan<float> canvas, int strokeIndex, Vector2 world)
    {
        var bestDistance = float.PositiveInfinity;
        var bestT = 0.0f;
        for (var scan = 0; scan < 16; scan++)
        {
            var t = scan / 15.0f;
            var center = StrokePoint(stroke, t);
            var distanceSquared = Vector2.DistanceSquared(world, center);
            if (distanceSquared < bestDistance)
            {
                bestDistance = distanceSquared;
                bestT = t;
            }
        }

        for (var refine = 0; refine < 3; refine++)
        {
            var span = 1.0f / (15.0f * MathF.Pow(2.0f, refine));
            var leftT = Saturate(bestT - span);
            var rightT = Saturate(bestT + span);
            var leftDistance = Vector2.DistanceSquared(world, StrokePoint(stroke, leftT));
            var rightDistance = Vector2.DistanceSquared(world, StrokePoint(stroke, rightT));
            if (leftDistance < bestDistance)
            {
                bestDistance = leftDistance;
                bestT = leftT;
            }

            if (rightDistance < bestDistance)
            {
                bestDistance = rightDistance;
                bestT = rightT;
            }
        }

        var sampleCount = Math.Clamp(frame.SampleCount, 2, 4096);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, 4096);
        var centerPoint = StrokePoint(stroke, bestT);
        var tangent = StrokeTangent(stroke, bestT, sampleCount);
        var normal = new Vector2(-tangent.Y, tangent.X);
        var lateral = Vector2.Dot(world - centerPoint, normal);
        var taper = StrokeTaper(bestT);
        var radius = MathF.Max(frame.BrushRadius * stroke.RadiusScale * stroke.NormalScale * (0.34f + taper * 0.66f), 0.0001f);
        var splay = Saturate(frame.Splay / 2.0f);
        var pressure = Saturate(frame.Pressure * stroke.PressureScale * 0.5f) * taper;
        var footprint = radius * (0.42f + splay * 0.74f + pressure * 0.18f);
        var tuftT = Saturate(lateral / MathF.Max(footprint, 0.001f) * 0.5f + 0.5f);
        var distance = MathF.Sqrt(bestDistance);
        var contact = SmoothStep(1.0f, 0.0f, distance / MathF.Max(footprint * 0.80f, 0.001f));
        var samplePosition = bestT * MathF.Max(sampleCount - 1.0f, 1.0f);
        var tuftPosition = tuftT * MathF.Max(tuftCount - 1.0f, 0.0f);
        var pigment = BilinearCanvasSample(canvas, strokeIndex, tuftCount, sampleCount, samplePosition, tuftPosition);
        return pigment * contact * (0.10f + pressure * 0.18f + Saturate(frame.InkLoad * 0.5f) * 0.08f);
    }

    private static float StrokeTaper(float t)
    {
        var entry = SmoothStep(0.0f, 0.10f, t);
        var exit = 1.0f - SmoothStep(0.86f, 1.0f, t);
        return 0.18f + entry * exit * 0.82f;
    }

    private static float LaneHash(int strokeIndex, int tuft, uint salt)
    {
        var value = (uint)(strokeIndex + 1) * 0x9E3779B9u ^ (uint)(tuft + 1) * 0x85EBCA6Bu ^ salt;
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 16777215.0f;
    }

    private static float BilinearCanvasSample(ReadOnlySpan<float> canvas, int strokeIndex, int tuftCount, int sampleCount, float samplePosition, float tuftPosition)
    {
        var sample0 = Math.Clamp((int)MathF.Floor(samplePosition), 0, sampleCount - 1);
        var sample1 = Math.Clamp(sample0 + 1, 0, sampleCount - 1);
        var tuft0 = Math.Clamp((int)MathF.Floor(tuftPosition), 0, tuftCount - 1);
        var tuft1 = Math.Clamp(tuft0 + 1, 0, tuftCount - 1);
        var sampleBlend = Saturate(samplePosition - sample0);
        var tuftBlend = Saturate(tuftPosition - tuft0);
        var rowOffset = strokeIndex * tuftCount;
        var p00 = canvas[(rowOffset + tuft0) * sampleCount + sample0];
        var p10 = canvas[(rowOffset + tuft0) * sampleCount + sample1];
        var p01 = canvas[(rowOffset + tuft1) * sampleCount + sample0];
        var p11 = canvas[(rowOffset + tuft1) * sampleCount + sample1];
        var lower = Lerp(p00, p10, sampleBlend);
        var upper = Lerp(p01, p11, sampleBlend);
        return Saturate(Lerp(lower, upper, tuftBlend));
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

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Saturate((x - edge0) / (edge1 - edge0));
        return t * t * (3.0f - 2.0f * t);
    }

    private static float Saturate(float value) => Math.Clamp(value, 0.0f, 1.0f);
}

public sealed record BokushoBrushSimulationResult(int SampleCount, int TuftCount, int StrokeCount, float[] Trace, float[] Canvas)
{
    public static BokushoBrushSimulationResult Empty { get; } = new(0, 0, 0, [], []);

    public bool HasSamples => Trace.Length > 0 && Canvas.Length == Trace.Length;
}
