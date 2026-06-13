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
        var tips = new Vector4[trace.Length];
        Evaluate(frame, trace, canvas, tips);
        return new BokushoBrushSimulationResult(sampleCount, tuftCount, strokeCount, trace, canvas, tips);
    }

    public static void Evaluate(AquariumBokushoBrushFrame source, Span<float> trace, Span<float> canvas)
    {
        var frame = source.Normalized();
        var sampleCount = Math.Clamp(frame.SampleCount, 2, 4096);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, 4096);
        var strokeCount = EffectiveStrokes(frame).Length;
        var tips = new Vector4[checked(sampleCount * tuftCount * strokeCount)];
        Evaluate(frame, trace, canvas, tips);
    }

    public static void Evaluate(AquariumBokushoBrushFrame source, Span<float> trace, Span<float> canvas, Span<Vector4> tips)
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

        if (tips.Length < valueCount)
        {
            throw new ArgumentException("Tip buffer is smaller than the brush simulation frame.", nameof(tips));
        }

        var wetness = Saturate(frame.Wetness / 1.6f);
        var load = Saturate(frame.InkLoad / 2.0f);
        var splay = Saturate(frame.Splay / 2.0f);
        var bend = Saturate(frame.Bend / 2.4f);
        var friction = Saturate(frame.Friction);

        for (var strokeIndex = 0; strokeIndex < strokes.Length; strokeIndex++)
        {
            var stroke = strokes[strokeIndex];
            var chainStart = ChainStart(strokes, strokeIndex);
            var strokeStart = Math.Clamp(stroke.SegmentStart, 0.0f, 1.0f);
            var pressure = Saturate(frame.Pressure * stroke.PressureScale * 0.5f) * 2.0f;
            var radius = MathF.Max(frame.BrushRadius * stroke.RadiusScale, 0.0001f);
            var normalRadius = MathF.Max(radius * stroke.NormalScale, 0.0001f);
            var tangentRadius = MathF.Max(radius * stroke.TangentScale, 0.0001f);
            var poseTilt = stroke.ShaftTilt;
            var poseRotation = stroke.ShaftRotation;
            var gripHeight = stroke.GripHeight;
            var compliance = stroke.Compliance;

            for (var tuft = 0; tuft < tuftCount; tuft++)
            {
                var laneT = tuftCount <= 1 ? 0.5f : tuft / (float)(tuftCount - 1);
                var restOffset = laneT * 2.0f - 1.0f;
                var edge = MathF.Abs(restOffset);
                var laneHash = LaneHash(chainStart, tuft, 17);
                var laneLoad = 0.76f + laneHash * 0.34f;
                var seedStroke = strokes[chainStart];
                var seedPressure = Saturate(frame.Pressure * seedStroke.PressureScale * 0.5f) * 2.0f;
                var seedRadius = MathF.Max(frame.BrushRadius * seedStroke.RadiusScale, 0.0001f);
                var seedNormalRadius = MathF.Max(seedRadius * seedStroke.NormalScale, 0.0001f);
                var seedSplit = Saturate((0.20f - LaneHash(chainStart, tuft, 53)) * 4.0f) * Saturate((edge - 0.18f) * 1.7f) * seedStroke.SplitScale;
                var initialCohesion = LaneCohesion(edge, seedSplit, wetness, seedPressure);
                var tip = StrokePoint(seedStroke, 0.0f);
                var poseBias = seedStroke.ShaftTilt * 0.18f + seedStroke.ShaftRotation * 0.08f;
                var offset = (restOffset + poseBias * (1.0f - edge * 0.35f)) * seedNormalRadius * (0.42f + splay * 0.38f) + (laneHash - 0.5f) * seedNormalRadius * 0.05f;
                var stateLoad = load * (0.62f + initialCohesion * 0.46f) * (1.0f - edge * 0.18f) * laneLoad * (1.0f - seedSplit * 0.36f);
                var stateWet = wetness * (0.74f + initialCohesion * 0.24f - seedSplit * 0.08f);

                for (var replayStrokeIndex = chainStart; replayStrokeIndex < strokeIndex; replayStrokeIndex++)
                {
                    SimulateTuftSegment(strokes, strokes[replayStrokeIndex], replayStrokeIndex, chainStart, tuft, sampleCount, tuftCount, frame.PhysicsHz, frame.Pressure, frame.BrushRadius, wetness, load, splay, bend, friction, restOffset, edge, ref offset, ref tip, ref stateLoad, ref stateWet, Span<float>.Empty, Span<float>.Empty, Span<Vector4>.Empty, writeOutput: false);
                }

                SimulateTuftSegment(strokes, stroke, strokeIndex, chainStart, tuft, sampleCount, tuftCount, frame.PhysicsHz, frame.Pressure, frame.BrushRadius, wetness, load, splay, bend, friction, restOffset, edge, ref offset, ref tip, ref stateLoad, ref stateWet, trace, canvas, tips, writeOutput: true);
            }
        }
    }

    private static void SimulateTuftSegment(
        IReadOnlyList<AquariumBokushoBrushStroke> strokes,
        AquariumBokushoBrushStroke stroke,
        int strokeIndex,
        int laneKey,
        int tuft,
        int sampleCount,
        int tuftCount,
        float physicsHz,
        float framePressure,
        float frameBrushRadius,
        float wetness,
        float load,
        float splay,
        float bend,
        float friction,
        float restOffset,
        float edge,
        ref float offset,
        ref Vector2 tip,
        ref float stateLoad,
        ref float stateWet,
        Span<float> trace,
        Span<float> canvas,
        Span<Vector4> tips,
        bool writeOutput)
    {
        var pressure = Saturate(framePressure * stroke.PressureScale * 0.5f) * 2.0f;
        var radius = MathF.Max(frameBrushRadius * stroke.RadiusScale, 0.0001f);
        var normalRadius = MathF.Max(radius * stroke.NormalScale, 0.0001f);
        var tangentRadius = MathF.Max(radius * stroke.TangentScale, 0.0001f);
        var segmentSpan = SegmentSpan(stroke, sampleCount);
        var segmentVelocityScale = 1.0f / segmentSpan;
        var split = Saturate((0.20f - LaneHash(laneKey, tuft, 53)) * 4.0f) * Saturate((edge - 0.18f) * 1.7f) * stroke.SplitScale;
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var t = sampleCount <= 1 ? 0.0f : sample / (float)(sampleCount - 1);
            var center = StrokePoint(stroke, t);
            var tangent = StrokeTangent(stroke, t, sampleCount);
            var normal = new Vector2(-tangent.Y, tangent.X);
            var strokeT = StrokeProgress(stroke, t);
            var taper = StrokeTaper(strokeT, stroke.EntryTaper, stroke.ExitTaper);
            var normalShape = StrokeNormalRadiusShape(taper);
            var tangentShape = StrokeTangentRadiusShape(taper);
            var pressureShape = StrokePressureShape(strokeT, stroke.EntryTaper, stroke.ExitTaper);
            var gesturePressure = StrokeGesturePressureShape(stroke, t, sampleCount);
            var gestureWidth = StrokeGestureWidthShape(stroke, t, sampleCount);
            var localPressure = pressure * taper * pressureShape * gesturePressure;
            var cohesion = LaneCohesion(edge, split, stateWet, localPressure);
            var poseSpread = Saturate(0.92f + MathF.Abs(stroke.ShaftTilt) * 0.22f + stroke.Compliance * 0.10f - stroke.GripHeight * 0.04f);
            var localNormalRadius = MathF.Max(normalRadius * normalShape * gestureWidth * poseSpread, 0.0001f);
            var localTangentRadius = MathF.Max(tangentRadius * tangentShape * (0.86f + gestureWidth * 0.08f + stroke.GripHeight * 0.10f), 0.0001f);
            var turnKey = stroke.SourceStrokeId >= 0 ? stroke.SourceStrokeId : strokeIndex;
            var turn = MathF.Sin(strokeT * MathF.Tau + turnKey * 0.37f);
            var rotatedRest = restOffset + stroke.ShaftRotation * 0.10f * (1.0f - edge);
            var targetOffset = rotatedRest * localNormalRadius * (0.38f + splay * 0.52f + localPressure * 0.08f - wetness * 0.10f) + (turn + stroke.ShaftRotation * 0.22f) * localNormalRadius * 0.14f * (1.0f - edge);
            var recovery = Saturate(0.06f + stateWet * 0.10f + localPressure * 0.07f + (1.0f - edge) * 0.06f + stroke.Compliance * 0.07f);
            offset = Lerp(offset, targetOffset, recovery);

            var lag = localTangentRadius * (0.04f + bend * 0.32f + stroke.GripHeight * 0.10f + friction * localPressure * 0.16f + edge * 0.08f);
            var desiredTip = center + normal * offset - tangent * lag;
            var slip = desiredTip - tip;
            var poseContact = 0.90f + stroke.Compliance * 0.10f + (1.0f - Saturate(stroke.GripHeight / 2.4f)) * 0.10f;
            var contact = Saturate(localPressure * poseContact * stateWet * stateLoad * (0.58f + cohesion * 0.40f + (1.0f - edge) * 0.18f));
            var drag = contact * friction * (0.38f + stateWet * 0.22f + edge * 0.18f);
            tip += slip * (1.0f - drag);

            var velocity = slip.Length() * physicsHz * segmentVelocityScale / MathF.Max(radius, 0.001f);
            var tension = Saturate(MathF.Abs(targetOffset - offset) / MathF.Max(localNormalRadius, 0.001f) * 0.38f + velocity * 0.018f + drag * 0.46f);
            var separation = Saturate(edge * 0.18f + tension * (0.24f + split * 0.18f) + velocity * 0.008f - stateWet * (0.12f + cohesion * 0.10f));
            var adhesion = Saturate(stateWet * (0.44f + cohesion * 0.28f + localPressure * 0.20f) - separation * 0.16f - tension * 0.07f);
            var laneCore = SmoothStep(0.0f, 0.74f, 1.0f - edge);
            var edgeComb = Saturate(edge * 1.08f + split * 0.26f + separation * 0.34f - cohesion * 0.12f);
            var dryMemory = Saturate((1.0f - stateWet) * 0.68f + edgeComb * 0.42f + velocity * 0.004f - localPressure * 0.10f);
            var fiberNoise = LaneHash(laneKey + sample * 13, tuft, 101);
            var fiberGate = SmoothStep(0.20f + dryMemory * 0.24f, 0.96f, fiberNoise);
            var tearNoise = LaneHash(laneKey + sample * 29, tuft, 211);
            var tearReadiness = Saturate(edge * 1.10f + separation * 0.64f + split * 0.18f + dryMemory * 0.22f - laneCore * 0.36f);
            var bristleTear = SmoothStep(0.50f - separation * 0.16f - dryMemory * 0.10f, 0.98f, tearNoise) * tearReadiness;
            var bristleContinuity = 1.0f - bristleTear * (0.42f + dryMemory * 0.24f + edge * 0.16f);
            var depositBody = 0.70f + laneCore * 0.58f - edgeComb * 0.14f;
            var depositIntermittency = (1.0f - fiberGate * dryMemory * (0.48f + edge * 0.24f)) * bristleContinuity;
            var deposition = contact * stateLoad * stateWet * Saturate(0.10f + drag * 0.72f + velocity * 0.010f) * (0.74f + separation * 0.18f + cohesion * 0.26f) * depositBody * depositIntermittency;
            stateLoad = MathF.Max(0.0f, stateLoad - deposition * segmentSpan * (0.032f + localPressure * 0.020f - cohesion * 0.008f));
            stateWet = MathF.Max(0.0f, stateWet - deposition * segmentSpan * (0.010f + dryMemory * 0.004f + edgeComb * 0.003f));

            if (writeOutput)
            {
                var index = ((strokeIndex * tuftCount) + tuft) * sampleCount + sample;
                var pigmentSurvival = 0.62f + cohesion * 0.30f + laneCore * 0.36f - split * 0.08f - dryMemory * 0.12f;
                var joinBlend = SegmentJoinBlend(strokes, strokeIndex, stroke, t);
                var pigment = deposition * (8.4f + contact * 2.8f) + contact * stateLoad * stateWet * (0.14f + laneCore * 0.18f) + adhesion * contact * 0.08f;
                canvas[index] = Saturate(pigment * pigmentSurvival * stroke.PigmentScale * joinBlend * bristleContinuity);
                trace[index] = Saturate((contact * (0.24f + stateLoad * 0.36f + adhesion * 0.18f + laneCore * 0.12f) + deposition * 1.9f) * joinBlend * (0.72f + bristleContinuity * 0.28f));
                tips[index] = new Vector4(
                    tip.X,
                    tip.Y,
                    localNormalRadius * (0.54f + laneCore * 0.24f + localPressure * 0.22f + splay * 0.20f - split * 0.04f) * (1.0f - bristleTear * (0.34f + edge * 0.16f)),
                    localTangentRadius * (0.84f + drag * 0.34f + bend * 0.18f) * (1.0f - bristleTear * 0.18f));
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
        var tips = Evaluate(frame).Tips;
        return ProjectCanvasToPage(source, canvas, tips, width, height, viewCenter, viewRadius);
    }

    public static float[] ProjectCanvasToPage(
        AquariumBokushoBrushFrame source,
        ReadOnlySpan<float> canvas,
        ReadOnlySpan<Vector4> tips,
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

        if (tips.Length < valueCount)
        {
            throw new ArgumentException("Tip buffer is smaller than the brush simulation frame.", nameof(tips));
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
                    value += ProjectCanvasSample(frame, strokes, canvas, tips, strokeIndex, world);
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
                EntryTaper = 0.10f,
                ExitTaper = 0.14f,
                PigmentScale = 1.0f,
                SplitScale = 1.0f,
                SegmentStart = 0.0f,
                SegmentEnd = 1.0f,
                SourceStrokeId = 0,
            }.Normalized()
        ];
    }

    private static int ChainStart(IReadOnlyList<AquariumBokushoBrushStroke> strokes, int strokeIndex)
    {
        var chainStart = strokeIndex;
        var sourceStrokeId = strokes[strokeIndex].SourceStrokeId;
        var expectedStart = Math.Clamp(strokes[strokeIndex].SegmentStart, 0.0f, 1.0f);
        while (chainStart > 0 && expectedStart > 0.0001f)
        {
            var previous = strokes[chainStart - 1];
            if (sourceStrokeId >= 0 && previous.SourceStrokeId != sourceStrokeId)
            {
                break;
            }

            var previousEnd = Math.Clamp(previous.SegmentEnd, 0.0f, 1.0f);
            if (MathF.Abs(previousEnd - expectedStart) > 0.001f)
            {
                break;
            }

            chainStart--;
            expectedStart = Math.Clamp(strokes[chainStart].SegmentStart, 0.0f, 1.0f);
        }

        return chainStart;
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

    private static float ProjectCanvasSample(AquariumBokushoBrushFrame frame, IReadOnlyList<AquariumBokushoBrushStroke> strokes, ReadOnlySpan<float> canvas, ReadOnlySpan<Vector4> tips, int strokeIndex, Vector2 world)
    {
        var stroke = strokes[strokeIndex];
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
        var projectionGate = InternalSegmentProjectionGate(strokes, strokeIndex, stroke, bestT);
        var lateral = Vector2.Dot(world - centerPoint, normal);
        var strokeT = StrokeProgress(stroke, bestT);
        var taper = StrokeTaper(strokeT, stroke.EntryTaper, stroke.ExitTaper);
        var gestureWidth = StrokeGestureWidthShape(stroke, bestT, sampleCount);
        var poseSpread = Saturate(0.92f + MathF.Abs(stroke.ShaftTilt) * 0.22f + stroke.Compliance * 0.10f - stroke.GripHeight * 0.04f);
        var radius = MathF.Max(frame.BrushRadius * stroke.RadiusScale * stroke.NormalScale * StrokeNormalRadiusShape(taper) * gestureWidth * poseSpread, 0.0001f);
        var splay = Saturate(frame.Splay / 2.0f);
        var pressure = Saturate(frame.Pressure * stroke.PressureScale * 0.5f) * taper;
        var poseContact = 0.90f + stroke.Compliance * 0.10f + (1.0f - Saturate(stroke.GripHeight / 2.4f)) * 0.10f;
        pressure *= StrokePressureShape(strokeT, stroke.EntryTaper, stroke.ExitTaper) * StrokeGesturePressureShape(stroke, bestT, sampleCount) * poseContact;
        var footprint = radius * (0.42f + splay * 0.74f + pressure * 0.18f);
        var tuftT = Saturate(lateral / MathF.Max(footprint, 0.001f) * 0.5f + 0.5f);
        var distance = MathF.Sqrt(bestDistance);
        var contact = SmoothStep(1.0f, 0.0f, distance / MathF.Max(footprint * 0.80f, 0.001f));
        var contactCore = contact * contact * (3.0f - 2.0f * contact);
        var paperKey = stroke.SourceStrokeId >= 0 ? stroke.SourceStrokeId : strokeIndex;
        var tooth = PaperTooth(world, paperKey);
        var edge = Saturate(distance / MathF.Max(footprint * 0.80f, 0.001f));
        var dryBreak = SmoothStep(0.18f + tooth * 0.18f, 0.92f, edge) * (1.0f - Saturate(frame.Wetness / 1.6f)) * (0.34f + stroke.SplitScale * 0.12f);
        var hold = Saturate(0.52f + tooth * 0.42f + pressure * 0.28f - dryBreak);
        var centerSample = Math.Clamp((int)MathF.Round(bestT * (sampleCount - 1)), 0, sampleCount - 1);
        var centerTuft = Math.Clamp((int)MathF.Round(tuftT * MathF.Max(tuftCount - 1.0f, 0.0f)), 0, tuftCount - 1);
        var pigmentPeak = 0.0f;
        var pigmentFlow = 0.0f;

        for (var sampleDelta = -3; sampleDelta <= 3; sampleDelta++)
        {
            var rawSampleIndex = centerSample + sampleDelta;
            var sampleStrokeIndex = strokeIndex;
            var sampleStroke = stroke;
            var sampleIndex = rawSampleIndex;
            if (rawSampleIndex < 0)
            {
                if (CanBorrowSourceSample(strokes, strokeIndex, strokeIndex - 1))
                {
                    sampleStrokeIndex = strokeIndex - 1;
                    sampleStroke = strokes[sampleStrokeIndex];
                    sampleIndex = sampleCount + rawSampleIndex;
                }
                else
                {
                    sampleIndex = 0;
                }
            }
            else if (rawSampleIndex >= sampleCount)
            {
                if (CanBorrowSourceSample(strokes, strokeIndex, strokeIndex + 1))
                {
                    sampleStrokeIndex = strokeIndex + 1;
                    sampleStroke = strokes[sampleStrokeIndex];
                    sampleIndex = rawSampleIndex - sampleCount;
                }
                else
                {
                    sampleIndex = sampleCount - 1;
                }
            }

            sampleIndex = Math.Clamp(sampleIndex, 0, sampleCount - 1);
            var sampleT = sampleIndex / MathF.Max(sampleCount - 1.0f, 1.0f);
            var sampleTangent = StrokeTangent(sampleStroke, sampleT, sampleCount);
            var sampleNormal = new Vector2(-sampleTangent.Y, sampleTangent.X);

            for (var tuftDelta = -2; tuftDelta <= 2; tuftDelta++)
            {
                var tuftIndex = Math.Clamp(centerTuft + tuftDelta, 0, tuftCount - 1);
                var previousStrokeIndex = sampleStrokeIndex;
                var previousSampleIndex = sampleIndex - 1;
                if (previousSampleIndex < 0)
                {
                    if (CanBorrowSourceSample(strokes, sampleStrokeIndex, sampleStrokeIndex - 1))
                    {
                        previousStrokeIndex = sampleStrokeIndex - 1;
                        previousSampleIndex = sampleCount - 1;
                    }
                    else
                    {
                        previousSampleIndex = 0;
                    }
                }

                var index = ((sampleStrokeIndex * tuftCount) + tuftIndex) * sampleCount + sampleIndex;
                var previousIndex = ((previousStrokeIndex * tuftCount) + tuftIndex) * sampleCount + previousSampleIndex;
                var tip = tips[index];
                var previousTip = tips[previousIndex];
                var tipPoint = new Vector2(tip.X, tip.Y);
                var previousTipPoint = new Vector2(previousTip.X, previousTip.Y);
                var sweep = tipPoint - previousTipPoint;
                var sweepLengthSquared = sweep.LengthSquared();
                var sweepT = sweepLengthSquared <= 0.000001f
                    ? 1.0f
                    : Saturate(Vector2.Dot(world - previousTipPoint, sweep) / sweepLengthSquared);
                var contactPoint = previousTipPoint + sweep * sweepT;
                var sweepDistance = MathF.Sqrt(Vector2.DistanceSquared(world, contactPoint));
                var sweepContact = SmoothStep(1.0f, 0.0f, sweepDistance / MathF.Max(MathF.Max(tip.Z, previousTip.Z) * 1.04f, 0.001f));
                var delta = world - contactPoint;
                var normalDistance = Vector2.Dot(delta, sampleNormal) / MathF.Max(tip.Z, 0.001f);
                var tangentDistance = Vector2.Dot(delta, sampleTangent) / MathF.Max(tip.W, 0.001f);
                var ellipse = MathF.Sqrt(normalDistance * normalDistance + tangentDistance * tangentDistance);
                var tipContact = MathF.Max(SmoothStep(1.0f, 0.0f, ellipse), sweepContact * 0.86f);
                var longitudinalGate = SmoothStep(1.0f, 0.0f, MathF.Abs(tangentDistance) * 0.62f);
                var contribution = canvas[index]
                    * tipContact
                    * (0.006f + contactCore * 0.994f)
                    * (0.06f + longitudinalGate * 0.94f)
                    * (1.0f - MathF.Abs(sampleDelta) * 0.070f)
                    * (0.82f + sweepContact * 0.24f);
                pigmentPeak = MathF.Max(pigmentPeak, contribution);
                pigmentFlow += contribution;
            }
        }

        return Saturate(pigmentPeak * 1.75f + pigmentFlow * 0.004f) * hold * projectionGate * (0.36f + pressure * 0.44f + Saturate(frame.InkLoad * 0.5f) * 0.20f);
    }

    private static bool CanBorrowSourceSample(IReadOnlyList<AquariumBokushoBrushStroke> strokes, int strokeIndex, int candidateIndex)
    {
        return candidateIndex >= 0
            && candidateIndex < strokes.Count
            && strokes[strokeIndex].SourceStrokeId >= 0
            && strokes[candidateIndex].SourceStrokeId == strokes[strokeIndex].SourceStrokeId;
    }

    private static float InternalSegmentProjectionGate(IReadOnlyList<AquariumBokushoBrushStroke> strokes, int strokeIndex, AquariumBokushoBrushStroke stroke, float t)
    {
        var gate = 1.0f;
        if (stroke.SegmentStart > 0.0001f && CanBorrowSourceSample(strokes, strokeIndex, strokeIndex - 1))
        {
            gate *= 0.34f + SmoothStep(0.0f, 0.16f, t) * 0.66f;
        }

        if (stroke.SegmentEnd < 0.9999f && CanBorrowSourceSample(strokes, strokeIndex, strokeIndex + 1))
        {
            gate *= 0.34f + (1.0f - SmoothStep(0.84f, 1.0f, t)) * 0.66f;
        }

        return gate;
    }

    private static float SegmentSpan(AquariumBokushoBrushStroke stroke, int sampleCount)
    {
        var start = Math.Clamp(stroke.SegmentStart, 0.0f, 1.0f);
        var end = Math.Clamp(stroke.SegmentEnd, start, 1.0f);
        return MathF.Max(end - start, 1.0f / MathF.Max(sampleCount - 1.0f, 1.0f));
    }

    private static float StrokeTaper(float t, float entryTaper, float exitTaper)
    {
        var entry = SmoothStep(0.0f, entryTaper, t);
        var exit = 1.0f - SmoothStep(1.0f - exitTaper, 1.0f, t);
        return 0.04f + entry * exit * 0.96f;
    }

    private static float StrokeNormalRadiusShape(float taper) => 0.08f + taper * 0.92f;

    private static float StrokeTangentRadiusShape(float taper) => 0.16f + taper * 0.84f;

    private static float StrokeProgress(AquariumBokushoBrushStroke stroke, float t)
    {
        var start = Math.Clamp(stroke.SegmentStart, 0.0f, 1.0f);
        var end = Math.Clamp(stroke.SegmentEnd, start, 1.0f);
        return Saturate(Lerp(start, end, t));
    }

    private static float SegmentJoinBlend(IReadOnlyList<AquariumBokushoBrushStroke> strokes, int strokeIndex, AquariumBokushoBrushStroke stroke, float t)
    {
        var blend = 1.0f;
        if (stroke.SegmentStart > 0.0001f)
        {
            var sameSourcePrevious = strokeIndex > 0 && stroke.SourceStrokeId >= 0 && strokes[strokeIndex - 1].SourceStrokeId == stroke.SourceStrokeId;
            var floor = sameSourcePrevious ? 0.76f : 0.58f;
            blend *= floor + SmoothStep(0.0f, 0.08f, t) * (1.0f - floor);
        }

        if (stroke.SegmentEnd < 0.9999f)
        {
            var sameSourceNext = strokeIndex + 1 < strokes.Count && stroke.SourceStrokeId >= 0 && strokes[strokeIndex + 1].SourceStrokeId == stroke.SourceStrokeId;
            var floor = sameSourceNext ? 0.76f : 0.58f;
            blend *= floor + (1.0f - SmoothStep(0.92f, 1.0f, t)) * (1.0f - floor);
        }

        return Math.Clamp(blend, 0.0f, 1.0f);
    }

    private static float StrokePressureShape(float t, float entryTaper, float exitTaper)
    {
        var pressIn = SmoothStep(0.0f, MathF.Min(entryTaper * 1.6f, 0.62f), t);
        var liftOut = 1.0f - SmoothStep(MathF.Max(1.0f - exitTaper * 1.35f, 0.20f), 1.0f, t);
        var belly = SmoothStep(0.12f, 0.42f, t) * (1.0f - SmoothStep(0.68f, 0.96f, t));
        return Saturate(0.54f + pressIn * liftOut * 0.28f + belly * 0.30f);
    }

    private static float LaneCohesion(float edge, float split, float wetness, float pressure)
    {
        return Saturate(0.50f + (1.0f - edge) * 0.38f + wetness * 0.22f + pressure * 0.12f - split * 0.18f);
    }

    private static float StrokeGesturePressureShape(AquariumBokushoBrushStroke stroke, float t, int sampleCount)
    {
        var dt = 1.0f / MathF.Max(sampleCount - 1.0f, 1.0f);
        var localSpeed = StrokeSpeed(stroke, t, dt);
        var expectedSpeed = MathF.Max(Vector2.Distance(new Vector2(stroke.StrokeP3.X, stroke.StrokeP3.Y), new Vector2(stroke.StrokeP0.X, stroke.StrokeP0.Y)), 0.001f);
        var slowPress = Saturate((expectedSpeed * 1.18f - localSpeed) / MathF.Max(expectedSpeed * 0.80f, 0.001f));
        var turnPress = StrokeTurn(stroke, t, dt);
        return Saturate(0.88f + slowPress * 0.30f + turnPress * 0.20f);
    }

    private static float StrokeGestureWidthShape(AquariumBokushoBrushStroke stroke, float t, int sampleCount)
    {
        var dt = 1.0f / MathF.Max(sampleCount - 1.0f, 1.0f);
        var localSpeed = StrokeSpeed(stroke, t, dt);
        var expectedSpeed = MathF.Max(Vector2.Distance(new Vector2(stroke.StrokeP3.X, stroke.StrokeP3.Y), new Vector2(stroke.StrokeP0.X, stroke.StrokeP0.Y)), 0.001f);
        var slowSpread = Saturate((expectedSpeed * 1.08f - localSpeed) / MathF.Max(expectedSpeed * 0.85f, 0.001f));
        var turnSpread = StrokeTurn(stroke, t, dt);
        return Saturate(0.84f + slowSpread * 0.36f + turnSpread * 0.20f);
    }

    private static float StrokeSpeed(AquariumBokushoBrushStroke stroke, float t, float dt)
    {
        var before = StrokePoint(stroke, Saturate(t - dt));
        var after = StrokePoint(stroke, Saturate(t + dt));
        return Vector2.Distance(before, after) / MathF.Max(dt * 2.0f, 0.001f);
    }

    private static float StrokeTurn(AquariumBokushoBrushStroke stroke, float t, float dt)
    {
        var before = StrokeLocalTangent(stroke, Saturate(t - dt), dt);
        var after = StrokeLocalTangent(stroke, Saturate(t + dt), dt);
        return Saturate(MathF.Abs(before.X * after.Y - before.Y * after.X) * 1.8f);
    }

    private static Vector2 StrokeLocalTangent(AquariumBokushoBrushStroke stroke, float t, float dt)
    {
        var before = StrokePoint(stroke, Saturate(t - dt));
        var after = StrokePoint(stroke, Saturate(t + dt));
        return Normalize(after - before);
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

    private static float PaperTooth(Vector2 world, int strokeIndex)
    {
        var fine = HashNoiseCell(world * 22.0f, strokeIndex, 0x6A09E667u);
        var fiber = HashNoiseCell(new Vector2(world.X * 7.0f + world.Y * 0.35f, world.Y * 2.4f), strokeIndex, 0xBB67AE85u);
        return Saturate(fine * 0.58f + fiber * 0.42f);
    }

    private static float HashNoiseCell(Vector2 point, int strokeIndex, uint salt)
    {
        unchecked
        {
            var x = (uint)(int)MathF.Floor(point.X);
            var y = (uint)(int)MathF.Floor(point.Y);
            var value = (uint)(strokeIndex + 1) * 0x9E3779B9u ^ x * 0x85EBCA6Bu ^ y * 0xC2B2AE35u ^ salt;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215.0f;
        }
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

public sealed record BokushoBrushSimulationResult(int SampleCount, int TuftCount, int StrokeCount, float[] Trace, float[] Canvas, Vector4[] Tips)
{
    public static BokushoBrushSimulationResult Empty { get; } = new(0, 0, 0, [], [], []);

    public bool HasSamples => Trace.Length > 0 && Canvas.Length == Trace.Length;
}
