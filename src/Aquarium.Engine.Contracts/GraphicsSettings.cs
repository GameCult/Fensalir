namespace Aquarium.Engine;

public readonly record struct GraphicsSettings(
    int RenderDebugMode,
    float SceneExposure,
    float BloomIntensity,
    float BloomVeilIntensity,
    int FieldReservoirMode,
    float FieldReservoirScale,
    float FieldReservoirSpatialReuseBudget)
{
    public const int MinRenderDebugMode = 0;
    public const int MaxRenderDebugMode = 21;
    public const int FieldReservoirModeNativeDomain = 0;
    public const int FieldReservoirModeTexelBaseline = 1;
    public const float MinFieldReservoirScale = 0.25f;
    public const float MaxFieldReservoirScale = 0.75f;
    public const float MinFieldReservoirSpatialReuseBudget = 0.25f;
    public const float MaxFieldReservoirSpatialReuseBudget = 1.0f;
    public const float MinSceneExposure = 0.02f;
    public const float MaxSceneExposure = 1.2f;
    public const float MinBloomIntensity = 0.0f;
    public const float MaxBloomIntensity = 0.8f;
    public const float MinBloomVeilIntensity = 0.0f;
    public const float MaxBloomVeilIntensity = 0.35f;

    public static GraphicsSettings Default { get; } = new(
        RenderDebugMode: 0,
        SceneExposure: 0.16f,
        BloomIntensity: 0.072f,
        BloomVeilIntensity: 0.014f,
        FieldReservoirMode: FieldReservoirModeNativeDomain,
        FieldReservoirScale: 0.5f,
        FieldReservoirSpatialReuseBudget: 0.5f);

    public GraphicsSettings Normalized()
    {
        return new GraphicsSettings(
            Math.Clamp(RenderDebugMode, MinRenderDebugMode, MaxRenderDebugMode),
            Math.Clamp(SceneExposure, MinSceneExposure, MaxSceneExposure),
            Math.Clamp(BloomIntensity, MinBloomIntensity, MaxBloomIntensity),
            Math.Clamp(BloomVeilIntensity, MinBloomVeilIntensity, MaxBloomVeilIntensity),
            Math.Clamp(FieldReservoirMode, FieldReservoirModeNativeDomain, FieldReservoirModeTexelBaseline),
            Math.Clamp(FieldReservoirScale, MinFieldReservoirScale, MaxFieldReservoirScale),
            Math.Clamp(FieldReservoirSpatialReuseBudget, MinFieldReservoirSpatialReuseBudget, MaxFieldReservoirSpatialReuseBudget));
    }
}
