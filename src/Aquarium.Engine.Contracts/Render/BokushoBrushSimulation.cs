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
        var load = Math.Clamp(frame.InkLoad / 1.25f, 0.0f, 1.8f);
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
                var seedSplit = Saturate((0.28f - LaneHash(chainStart, tuft, 53)) * 3.0f) * Saturate((edge - 0.20f) * 1.5f) * seedStroke.SplitScale;
                var initialCohesion = LaneCohesion(edge, seedSplit, wetness, seedPressure);
                var tip = StrokePoint(seedStroke, 0.0f);
                var poseBias = seedStroke.ShaftTilt * 0.18f + seedStroke.ShaftRotation * 0.08f;
                var offset = (restOffset + poseBias * (1.0f - edge * 0.35f)) * seedNormalRadius * (0.42f + splay * 0.38f) + (laneHash - 0.5f) * seedNormalRadius * 0.05f;
                var stateLoad = load * (0.86f + initialCohesion * 0.48f) * (1.0f - edge * 0.08f) * laneLoad * (1.0f - seedSplit * 0.18f);
                var stateWet = wetness * (0.74f + initialCohesion * 0.24f - seedSplit * 0.08f);

                for (var replayStrokeIndex = chainStart; replayStrokeIndex < strokeIndex; replayStrokeIndex++)
                {
                    SimulateTuftSegment(strokes, strokes[replayStrokeIndex], replayStrokeIndex, chainStart, tuft, sampleCount, tuftCount, frame.PhysicsHz, frame.Pressure, frame.BrushRadius, wetness, load, splay, bend, friction, restOffset, edge, ref offset, ref tip, ref stateLoad, ref stateWet, Span<float>.Empty, Span<float>.Empty, Span<Vector4>.Empty, writeOutput: false);
                }

                SimulateTuftSegment(strokes, stroke, strokeIndex, chainStart, tuft, sampleCount, tuftCount, frame.PhysicsHz, frame.Pressure, frame.BrushRadius, wetness, load, splay, bend, friction, restOffset, edge, ref offset, ref tip, ref stateLoad, ref stateWet, trace, canvas, tips, writeOutput: true);
            }
        }
    }

    public static float[] EvaluatePage(
        AquariumBokushoBrushFrame source,
        int width,
        int height,
        Vector2 viewCenter,
        float viewRadius)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var frame = source.HasInput ? source.Normalized() : AquariumBokushoBrushFrame.Empty;
        if (!frame.HasInput)
        {
            return new float[checked(width * height)];
        }

        var sampleCount = Math.Clamp(frame.SampleCount, 2, 4096);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, 4096);
        var strokes = EffectiveStrokes(frame);
        var page = new float[checked(width * height)];
        var wetness = Saturate(frame.Wetness / 1.6f);
        var load = Math.Clamp(frame.InkLoad / 1.25f, 0.0f, 1.8f);
        var splay = Saturate(frame.Splay / 2.0f);
        var bend = Saturate(frame.Bend / 2.4f);
        var friction = Saturate(frame.Friction);
        var safeRadius = MathF.Max(viewRadius, 0.001f);

        for (var strokeIndex = 0; strokeIndex < strokes.Length; strokeIndex++)
        {
            var stroke = strokes[strokeIndex];
            var chainStart = ChainStart(strokes, strokeIndex);

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
                var seedSplit = Saturate((0.28f - LaneHash(chainStart, tuft, 53)) * 3.0f) * Saturate((edge - 0.20f) * 1.5f) * seedStroke.SplitScale;
                var initialCohesion = LaneCohesion(edge, seedSplit, wetness, seedPressure);
                var tip = StrokePoint(seedStroke, 0.0f);
                var poseBias = seedStroke.ShaftTilt * 0.18f + seedStroke.ShaftRotation * 0.08f;
                var offset = (restOffset + poseBias * (1.0f - edge * 0.35f)) * seedNormalRadius * (0.42f + splay * 0.38f) + (laneHash - 0.5f) * seedNormalRadius * 0.05f;
                var stateLoad = load * (0.86f + initialCohesion * 0.48f) * (1.0f - edge * 0.08f) * laneLoad * (1.0f - seedSplit * 0.18f);
                var stateWet = wetness * (0.74f + initialCohesion * 0.24f - seedSplit * 0.08f);

                for (var replayStrokeIndex = chainStart; replayStrokeIndex < strokeIndex; replayStrokeIndex++)
                {
                    SimulateTuftSegmentToPage(strokes[replayStrokeIndex], replayStrokeIndex, chainStart, tuft, sampleCount, frame.PhysicsHz, frame.Pressure, frame.BrushRadius, splay, bend, friction, restOffset, edge, ref offset, ref tip, ref stateLoad, ref stateWet, page, width, height, viewCenter, safeRadius, writeOutput: false);
                }

                SimulateTuftSegmentToPage(stroke, strokeIndex, chainStart, tuft, sampleCount, frame.PhysicsHz, frame.Pressure, frame.BrushRadius, splay, bend, friction, restOffset, edge, ref offset, ref tip, ref stateLoad, ref stateWet, page, width, height, viewCenter, safeRadius, writeOutput: true);
            }
        }

        return page;
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
        var split = Saturate((0.28f - LaneHash(laneKey, tuft, 53)) * 3.0f) * Saturate((edge - 0.20f) * 1.5f) * stroke.SplitScale;
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var t = sampleCount <= 1 ? 0.0f : sample / (float)(sampleCount - 1);
            var center = StrokePoint(stroke, t);
            var tangent = StrokeTangent(stroke, t, sampleCount);
            var normal = new Vector2(-tangent.Y, tangent.X);
            var strokeT = StrokeProgress(stroke, t);
            var taper = StrokeTaper(strokeT, stroke.EntryTaper, stroke.ExitTaper);
            var localPressure = pressure * taper;
            var laneCore = SmoothStep(0.0f, 0.78f, 1.0f - edge);
            var cohesion = Saturate(0.28f + stateWet * 0.44f + laneCore * 0.24f + localPressure * 0.10f - split * 0.18f);
            var localNormalRadius = MathF.Max(normalRadius * (0.18f + taper * 0.82f) * (0.72f + splay * 0.34f + localPressure * 0.16f - cohesion * 0.08f), 0.0001f);
            var localTangentRadius = MathF.Max(tangentRadius * (0.24f + taper * 0.76f) * (0.86f + bend * 0.18f), 0.0001f);
            var targetOffset = (restOffset + stroke.ShaftRotation * 0.12f) * localNormalRadius * (0.58f + splay * 0.34f + localPressure * 0.18f - cohesion * 0.20f);
            var recovery = Saturate(0.05f + stroke.Compliance * 0.08f + stateWet * 0.08f + localPressure * 0.10f);
            offset = Lerp(offset, targetOffset, recovery);

            var poseLead = stroke.ShaftTilt * 0.36f + stroke.ShaftRotation * 0.16f;
            var lag = localTangentRadius * (0.10f + bend * 0.32f + friction * localPressure * 0.16f + edge * 0.06f);
            var dragVector = Normalize(tangent + normal * poseLead);
            var desiredTip = center + normal * offset - dragVector * lag;
            var slip = desiredTip - tip;
            var velocity = slip.Length() * physicsHz * segmentVelocityScale / MathF.Max(radius, 0.001f);
            var contact = Saturate(localPressure * stateLoad * (0.24f + stateWet * 0.62f + laneCore * 0.18f));
            var drag = contact * friction * (0.28f + stateWet * 0.34f);
            var previousTip = tip;
            tip += slip * (0.18f + recovery * 0.82f) * (1.0f - drag * 0.52f);
            var sweptDistance = (tip - previousTip).Length();
            var sweptPatch = Saturate(sweptDistance / MathF.Max(localTangentRadius, 0.001f) * 0.42f);

            var tension = Saturate(velocity * 0.014f + MathF.Abs(targetOffset - offset) / MathF.Max(localNormalRadius, 0.001f) * 0.22f + edge * 0.12f);
            var separation = Saturate(split * 0.34f + tension * 0.46f + edge * 0.20f - cohesion * 0.24f);
            var adhesion = Saturate(stateWet * (0.36f + cohesion * 0.40f) + localPressure * 0.10f - separation * 0.22f);
            var dryMemory = Saturate((1.0f - stateWet) * 0.62f + separation * 0.32f + velocity * 0.004f);
            var fiberNoise = LaneHash(laneKey + sample * 13, tuft, 101);
            var continuity = 1.0f - SmoothStep(0.18f + dryMemory * 0.28f, 0.96f, fiberNoise) * dryMemory * (0.38f + edge * 0.22f);
            var contactTransfer = contact * stateLoad * (0.16f + stateWet * 0.92f) * (0.30f + drag * 0.64f + localPressure * 0.22f + sweptPatch * 0.18f) * (0.68f + laneCore * 0.50f - separation * 0.14f) * continuity;
            var airborneRelease = (1.0f - contact) * stateLoad * stateWet * Saturate(velocity * 0.010f - adhesion * 0.16f) * (0.20f + separation * 0.42f + edge * 0.18f);
            var consumedPigment = contactTransfer + airborneRelease;
            stateLoad = MathF.Max(0.0f, stateLoad - consumedPigment * segmentSpan * (0.010f + localPressure * 0.006f));
            stateWet = MathF.Max(0.0f, stateWet - consumedPigment * segmentSpan * (0.006f + dryMemory * 0.003f));

            if (writeOutput)
            {
                var index = ((strokeIndex * tuftCount) + tuft) * sampleCount + sample;
                var pigment = (contactTransfer * (0.95f + localPressure * 0.34f + sweptPatch * 0.24f) + airborneRelease * (1.6f + velocity * 0.002f)) * stroke.PigmentScale;
                canvas[index] = Saturate(pigment);
                trace[index] = Saturate(contact * 0.70f + airborneRelease * 2.0f + stateLoad * 0.18f);
                tips[index] = new Vector4(
                    tip.X,
                    tip.Y,
                    localNormalRadius * (0.84f + laneCore * 0.20f + localPressure * 0.14f + sweptPatch * 0.08f - separation * 0.10f),
                    localTangentRadius * (0.92f + bend * 0.22f + drag * 0.16f) + sweptDistance * 0.36f);
            }
        }
    }

    private static void SimulateTuftSegmentToPage(
        AquariumBokushoBrushStroke stroke,
        int strokeIndex,
        int laneKey,
        int tuft,
        int sampleCount,
        float physicsHz,
        float framePressure,
        float frameBrushRadius,
        float splay,
        float bend,
        float friction,
        float restOffset,
        float edge,
        ref float offset,
        ref Vector2 tip,
        ref float stateLoad,
        ref float stateWet,
        Span<float> page,
        int width,
        int height,
        Vector2 viewCenter,
        float viewRadius,
        bool writeOutput)
    {
        var pressure = Saturate(framePressure * stroke.PressureScale * 0.5f) * 2.0f;
        var radius = MathF.Max(frameBrushRadius * stroke.RadiusScale, 0.0001f);
        var normalRadius = MathF.Max(radius * stroke.NormalScale, 0.0001f);
        var tangentRadius = MathF.Max(radius * stroke.TangentScale, 0.0001f);
        var segmentSpan = SegmentSpan(stroke, sampleCount);
        var stepCount = Math.Clamp((int)MathF.Ceiling(segmentSpan * physicsHz), 2, 4096);
        var segmentVelocityScale = 1.0f / segmentSpan;
        var split = Saturate((0.28f - LaneHash(laneKey, tuft, 53)) * 3.0f) * Saturate((edge - 0.20f) * 1.5f) * stroke.SplitScale;

        for (var step = 0; step < stepCount; step++)
        {
            var t = stepCount <= 1 ? 0.0f : step / (float)(stepCount - 1);
            var center = StrokePoint(stroke, t);
            var tangent = StrokeTangent(stroke, t, stepCount);
            var normal = new Vector2(-tangent.Y, tangent.X);
            var strokeT = StrokeProgress(stroke, t);
            var taper = StrokeTaper(strokeT, stroke.EntryTaper, stroke.ExitTaper);
            var localPressure = pressure * taper;
            var laneCore = SmoothStep(0.0f, 0.78f, 1.0f - edge);
            var cohesion = Saturate(0.28f + stateWet * 0.44f + laneCore * 0.24f + localPressure * 0.10f - split * 0.18f);
            var localNormalRadius = MathF.Max(normalRadius * (0.18f + taper * 0.82f) * (0.72f + splay * 0.34f + localPressure * 0.16f - cohesion * 0.08f), 0.0001f);
            var localTangentRadius = MathF.Max(tangentRadius * (0.24f + taper * 0.76f) * (0.86f + bend * 0.18f), 0.0001f);
            var targetOffset = (restOffset + stroke.ShaftRotation * 0.12f) * localNormalRadius * (0.58f + splay * 0.34f + localPressure * 0.18f - cohesion * 0.20f);
            var recovery = Saturate(0.05f + stroke.Compliance * 0.08f + stateWet * 0.08f + localPressure * 0.10f);
            offset = Lerp(offset, targetOffset, recovery);

            var poseLead = stroke.ShaftTilt * 0.36f + stroke.ShaftRotation * 0.16f;
            var lag = localTangentRadius * (0.10f + bend * 0.32f + friction * localPressure * 0.16f + edge * 0.06f);
            var dragVector = Normalize(tangent + normal * poseLead);
            var desiredTip = center + normal * offset - dragVector * lag;
            var slip = desiredTip - tip;
            var velocity = slip.Length() * physicsHz * segmentVelocityScale / MathF.Max(radius, 0.001f);
            var contact = Saturate(localPressure * stateLoad * (0.24f + stateWet * 0.62f + laneCore * 0.18f));
            var drag = contact * friction * (0.28f + stateWet * 0.34f);
            var previousTip = tip;
            tip += slip * (0.18f + recovery * 0.82f) * (1.0f - drag * 0.52f);
            var sweptDistance = (tip - previousTip).Length();
            var sweptPatch = Saturate(sweptDistance / MathF.Max(localTangentRadius, 0.001f) * 0.42f);

            var tension = Saturate(velocity * 0.014f + MathF.Abs(targetOffset - offset) / MathF.Max(localNormalRadius, 0.001f) * 0.22f + edge * 0.12f);
            var separation = Saturate(split * 0.34f + tension * 0.46f + edge * 0.20f - cohesion * 0.24f);
            var adhesion = Saturate(stateWet * (0.36f + cohesion * 0.40f) + localPressure * 0.10f - separation * 0.22f);
            var dryMemory = Saturate((1.0f - stateWet) * 0.62f + separation * 0.32f + velocity * 0.004f);
            var fiberNoise = LaneHash(laneKey + step * 13, tuft, 101);
            var continuity = 1.0f - SmoothStep(0.18f + dryMemory * 0.28f, 0.96f, fiberNoise) * dryMemory * (0.38f + edge * 0.22f);
            var contactTransfer = contact * stateLoad * (0.16f + stateWet * 0.92f) * (0.30f + drag * 0.64f + localPressure * 0.22f + sweptPatch * 0.18f) * (0.68f + laneCore * 0.50f - separation * 0.14f) * continuity;
            var airborneRelease = (1.0f - contact) * stateLoad * stateWet * Saturate(velocity * 0.010f - adhesion * 0.16f) * (0.20f + separation * 0.42f + edge * 0.18f);
            var consumedPigment = contactTransfer + airborneRelease;
            stateLoad = MathF.Max(0.0f, stateLoad - consumedPigment * segmentSpan * (0.010f + localPressure * 0.006f));
            stateWet = MathF.Max(0.0f, stateWet - consumedPigment * segmentSpan * (0.006f + dryMemory * 0.003f));

            if (writeOutput)
            {
                var pigment = (contactTransfer * (0.95f + localPressure * 0.34f + sweptPatch * 0.24f) + airborneRelease * (1.6f + velocity * 0.002f)) * stroke.PigmentScale;
                var patchNormalRadius = localNormalRadius * (0.84f + laneCore * 0.20f + localPressure * 0.14f + sweptPatch * 0.08f - separation * 0.10f);
                var patchTangentRadius = localTangentRadius * (0.92f + bend * 0.22f + drag * 0.16f) + sweptDistance * 0.36f;
                DepositSweptPatch(page, width, height, viewCenter, viewRadius, previousTip, tip, patchNormalRadius, patchTangentRadius, pigment);
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
        var lateral = Vector2.Dot(world - centerPoint, normal);
        var strokeT = StrokeProgress(stroke, bestT);
        var taper = StrokeTaper(strokeT, stroke.EntryTaper, stroke.ExitTaper);
        var radius = MathF.Max(frame.BrushRadius * stroke.RadiusScale * stroke.NormalScale * (0.18f + taper * 0.82f), 0.0001f);
        var splay = Saturate(frame.Splay / 2.0f);
        var pressure = Saturate(frame.Pressure * stroke.PressureScale * 0.5f) * taper;
        var wetSpread = 0.86f + Saturate(frame.Wetness / 1.6f) * 0.18f;
        var footprint = radius * wetSpread * (0.72f + splay * 0.44f + pressure * 0.22f);
        var tuftT = Saturate(lateral / MathF.Max(footprint, 0.001f) * 0.5f + 0.5f);
        var distance = MathF.Sqrt(bestDistance);
        var contact = SmoothStep(1.0f, 0.0f, distance / MathF.Max(footprint * 0.96f, 0.001f));
        var contactCore = contact * contact * (3.0f - 2.0f * contact);
        var centerSample = Math.Clamp((int)MathF.Round(bestT * (sampleCount - 1)), 0, sampleCount - 1);
        var centerTuft = Math.Clamp((int)MathF.Round(tuftT * MathF.Max(tuftCount - 1.0f, 0.0f)), 0, tuftCount - 1);
        var pigmentPeak = 0.0f;
        var pigmentFlow = 0.0f;

        for (var sampleDelta = -3; sampleDelta <= 3; sampleDelta++)
        {
            var rawSampleIndex = centerSample + sampleDelta;
            var sampleStrokeIndex = strokeIndex;
            var sampleStroke = stroke;
            var sampleIndex = Math.Clamp(rawSampleIndex, 0, sampleCount - 1);
            var sampleT = sampleIndex / MathF.Max(sampleCount - 1.0f, 1.0f);
            var sampleTangent = StrokeTangent(sampleStroke, sampleT, sampleCount);
            var sampleNormal = new Vector2(-sampleTangent.Y, sampleTangent.X);

            for (var tuftDelta = -2; tuftDelta <= 2; tuftDelta++)
            {
                var tuftIndex = Math.Clamp(centerTuft + tuftDelta, 0, tuftCount - 1);
                var previousStrokeIndex = sampleStrokeIndex;
                var previousSampleIndex = Math.Max(sampleIndex - 1, 0);

                var index = ((sampleStrokeIndex * tuftCount) + tuftIndex) * sampleCount + sampleIndex;
                var previousIndex = ((previousStrokeIndex * tuftCount) + tuftIndex) * sampleCount + previousSampleIndex;
                var tip = tips[index];
                var previousTip = tips[previousIndex];
                var pigment = canvas[index];
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
                var contribution = pigment
                    * tipContact
                    * (0.006f + contactCore * 0.994f)
                    * (0.06f + longitudinalGate * 0.94f)
                    * (1.0f - MathF.Abs(sampleDelta) * 0.070f)
                    * (0.82f + sweepContact * 0.24f);
                pigmentPeak = MathF.Max(pigmentPeak, contribution);
                pigmentFlow += contribution;
            }
        }

        var decisiveInk = Saturate((pigmentPeak - 0.012f) * 2.25f + pigmentFlow * 0.002f);
        return decisiveInk * (0.44f + pressure * 0.36f + Saturate(frame.InkLoad * 0.5f) * 0.20f);
    }

    private static void DepositSweptPatch(
        Span<float> page,
        int width,
        int height,
        Vector2 viewCenter,
        float viewRadius,
        Vector2 previousTip,
        Vector2 tip,
        float normalRadius,
        float tangentRadius,
        float pigment)
    {
        var ink = Saturate(pigment);
        if (ink <= 0.000001f)
        {
            return;
        }

        var sweep = tip - previousTip;
        var sweepLength = sweep.Length();
        var tangent = sweepLength > 0.000001f ? sweep / sweepLength : Vector2.UnitX;
        var normal = new Vector2(-tangent.Y, tangent.X);
        var patchTangentRadius = MathF.Max(tangentRadius * 0.22f + sweepLength * 0.5f, 0.001f);
        var patchNormalRadius = MathF.Max(normalRadius * 0.16f, 0.001f);
        var center = (previousTip + tip) * 0.5f;
        var extents = new Vector2(
            MathF.Abs(tangent.X) * patchTangentRadius + MathF.Abs(normal.X) * patchNormalRadius,
            MathF.Abs(tangent.Y) * patchTangentRadius + MathF.Abs(normal.Y) * patchNormalRadius);
        var minWorld = center - extents;
        var maxWorld = center + extents;
        var minPixel = WorldToPixel(minWorld, width, height, viewCenter, viewRadius);
        var maxPixel = WorldToPixel(maxWorld, width, height, viewCenter, viewRadius);
        var minX = Math.Clamp((int)MathF.Floor(MathF.Min(minPixel.X, maxPixel.X)), 0, width - 1);
        var maxX = Math.Clamp((int)MathF.Ceiling(MathF.Max(minPixel.X, maxPixel.X)), 0, width - 1);
        var minY = Math.Clamp((int)MathF.Floor(MathF.Min(minPixel.Y, maxPixel.Y)), 0, height - 1);
        var maxY = Math.Clamp((int)MathF.Ceiling(MathF.Max(minPixel.Y, maxPixel.Y)), 0, height - 1);

        for (var y = minY; y <= maxY; y++)
        {
            var uvY = height <= 1 ? 0.0f : y / (float)(height - 1);
            for (var x = minX; x <= maxX; x++)
            {
                var uvX = width <= 1 ? 0.0f : x / (float)(width - 1);
                var world = viewCenter + (new Vector2(uvX, uvY) * 2.0f - Vector2.One) * viewRadius;
                var local = world - center;
                var tangentDistance = Vector2.Dot(local, tangent) / patchTangentRadius;
                var normalDistance = Vector2.Dot(local, normal) / patchNormalRadius;
                var ellipse = MathF.Sqrt(tangentDistance * tangentDistance + normalDistance * normalDistance);
                var coverage = SmoothStep(1.0f, 0.0f, ellipse);
                if (coverage <= 0.0f)
                {
                    continue;
                }

                var sampleIndex = y * width + x;
                var contribution = ink * coverage * 0.002f;
                page[sampleIndex] = 1.0f - (1.0f - page[sampleIndex]) * (1.0f - contribution);
            }
        }
    }

    private static Vector2 WorldToPixel(Vector2 world, int width, int height, Vector2 viewCenter, float viewRadius)
    {
        var uv = (world - viewCenter) / MathF.Max(viewRadius, 0.001f) * 0.5f + new Vector2(0.5f, 0.5f);
        return new Vector2(uv.X * (width - 1), uv.Y * (height - 1));
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
        var contact = MathF.Pow(entry * exit, 1.12f);
        return 0.018f + contact * 0.982f;
    }

    private static float StrokeProgress(AquariumBokushoBrushStroke stroke, float t)
    {
        var start = Math.Clamp(stroke.SegmentStart, 0.0f, 1.0f);
        var end = Math.Clamp(stroke.SegmentEnd, start, 1.0f);
        return Saturate(Lerp(start, end, t));
    }

    private static float LaneCohesion(float edge, float split, float wetness, float pressure)
    {
        return Saturate(0.50f + (1.0f - edge) * 0.38f + wetness * 0.22f + pressure * 0.12f - split * 0.18f);
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

public sealed record BokushoBrushSimulationResult(int SampleCount, int TuftCount, int StrokeCount, float[] Trace, float[] Canvas, Vector4[] Tips)
{
    public static BokushoBrushSimulationResult Empty { get; } = new(0, 0, 0, [], [], []);

    public bool HasSamples => Trace.Length > 0 && Canvas.Length == Trace.Length;
}
