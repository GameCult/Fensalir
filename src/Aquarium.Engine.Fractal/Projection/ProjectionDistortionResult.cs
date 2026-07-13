using CultMath;

namespace Aquarium.Engine.Fractal.Projection;

public readonly record struct ProjectionDistortionResult(
    string ProjectionName,
    PlanetaryCubeFace Face,
    int SamplesPerAxis,
    double AverageAreaScale,
    double MinRelativeArea,
    double MaxRelativeArea,
    double MeanAbsoluteRelativeAreaError,
    double RootMeanSquareRelativeAreaError);
