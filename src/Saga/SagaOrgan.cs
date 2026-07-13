using CultMath;
using System.Numerics;

namespace Saga;

public static class SagaOrgan
{
    public const string Name = "Saga";

    public const string Description =
        "Cube-sphere stochastic planetary flow for Fensalir Transport fields.";
}

public readonly record struct SagaFlowDomain(
    string DomainId,
    double PlanetRadiusMeters,
    string ProjectionId,
    string GridVersion);

public readonly record struct SagaFlowRequest(
    SagaFlowDomain Domain,
    PlanetaryTileAddress Tile,
    int SampleResolution,
    double TimeSeconds,
    SagaCoriolisFrame Coriolis,
    IReadOnlyList<SagaForcingSource> ForcingSources);

public readonly record struct SagaCoriolisFrame(
    Vector3 RotationAxis,
    double AngularVelocityRadiansPerSecond,
    double LocalF,
    Vector2 LocalBeta);

public readonly record struct SagaForcingSource(
    string SourceId,
    SagaForcingKind Kind,
    SagaForcingGeometry Geometry,
    Vector3 PlanetDirection,
    Vector3 LocalTangentDirection,
    double Intensity,
    double RadiusMeters,
    double Attenuation,
    double TimePhase);

public readonly record struct SagaAdvectionSample(
    Vector2 MeanVelocity,
    SagaCovariance2D Diffusivity,
    double Confidence);

public readonly record struct SagaCovariance2D(double C00, double C01, double C11);

public enum SagaForcingKind
{
    Energy,
    Mass,
    Momentum,
}

public enum SagaForcingGeometry
{
    Directional,
    Point,
    Area,
    Volume,
}
