using System.Numerics;
using CultMath;
using GameCult.Geometry;

namespace Aquarium.Engine.Fractal.Projection;

public static class ProjectionDistortionSampler
{
    private const double EqualAreaScalePerCubeCoordinate = Math.PI / 6.0;
    private const double DerivativeStep = 1.0e-4;

    public static ProjectionDistortionResult SampleFace(
        ICubeSphereProjection projection,
        PlanetaryCubeFace face,
        int samplesPerAxis)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (samplesPerAxis <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(samplesPerAxis), samplesPerAxis, "Sample count must be positive.");
        }

        var minRelativeArea = double.PositiveInfinity;
        var maxRelativeArea = double.NegativeInfinity;
        var totalAreaScale = 0.0;
        var totalAbsRelativeError = 0.0;
        var totalSquaredRelativeError = 0.0;
        var sampleCount = 0;

        for (var y = 0; y < samplesPerAxis; y++)
        {
            var v = -1.0 + (2.0 * (y + 0.5) / samplesPerAxis);
            for (var x = 0; x < samplesPerAxis; x++)
            {
                var u = -1.0 + (2.0 * (x + 0.5) / samplesPerAxis);
                var areaScale = SurfaceAreaScale(projection, face, u, v);
                var relativeArea = areaScale / EqualAreaScalePerCubeCoordinate;
                var relativeError = relativeArea - 1.0;

                minRelativeArea = Math.Min(minRelativeArea, relativeArea);
                maxRelativeArea = Math.Max(maxRelativeArea, relativeArea);
                totalAreaScale += areaScale;
                totalAbsRelativeError += Math.Abs(relativeError);
                totalSquaredRelativeError += relativeError * relativeError;
                sampleCount++;
            }
        }

        return new ProjectionDistortionResult(
            projection.Name,
            face,
            samplesPerAxis,
            totalAreaScale / sampleCount,
            minRelativeArea,
            maxRelativeArea,
            totalAbsRelativeError / sampleCount,
            Math.Sqrt(totalSquaredRelativeError / sampleCount));
    }

    private static double SurfaceAreaScale(ICubeSphereProjection projection, PlanetaryCubeFace face, double u, double v)
    {
        var du = Derivative(projection, face, u, v, DerivativeAxis.U);
        var dv = Derivative(projection, face, u, v, DerivativeAxis.V);
        return Vector3.Cross(du, dv).Length();
    }

    private static Vector3 Derivative(ICubeSphereProjection projection, PlanetaryCubeFace face, double u, double v, DerivativeAxis axis)
    {
        var low = axis == DerivativeAxis.U ? u : v;
        var high = low;
        var backward = Math.Min(DerivativeStep, low + 1.0);
        var forward = Math.Min(DerivativeStep, 1.0 - high);

        if (backward == 0.0)
        {
            backward = forward;
        }
        else if (forward == 0.0)
        {
            forward = backward;
        }

        var lowCoordinate = low - backward;
        var highCoordinate = high + forward;

        Vector3 lowPoint;
        Vector3 highPoint;
        if (axis == DerivativeAxis.U)
        {
            lowPoint = projection.Project(new PlanetaryFaceCoordinate(face, lowCoordinate, v));
            highPoint = projection.Project(new PlanetaryFaceCoordinate(face, highCoordinate, v));
        }
        else
        {
            lowPoint = projection.Project(new PlanetaryFaceCoordinate(face, u, lowCoordinate));
            highPoint = projection.Project(new PlanetaryFaceCoordinate(face, u, highCoordinate));
        }

        return (highPoint - lowPoint) / (float)(forward + backward);
    }

    private enum DerivativeAxis
    {
        U,
        V,
    }
}
