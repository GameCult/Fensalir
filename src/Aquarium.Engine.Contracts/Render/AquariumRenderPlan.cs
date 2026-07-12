using System.Numerics;
using System.Runtime.InteropServices;
using Aquarium.Engine.Fractal;

namespace Aquarium.Engine.Render;

public sealed class AquariumRenderPlan
{
    public AquariumRenderPlan()
    {
        Shaders = new AquariumShaderManifest();
        Graph = new AquariumRenderGraphDescription();
    }

    public AquariumShaderManifest Shaders { get; }

    public AquariumRenderGraphDescription Graph { get; }
}

public sealed class AquariumShaderManifest
{
    private readonly List<string> sdfShaderPaths = [];
    private readonly List<string> includePaths = [];

    public string? ShaderRoot { get; private set; }

    public string HeightFieldShader { get; private set; } = "D3D12HeightField.hlsl";

    public string SceneShader { get; private set; } = "D3D12Scene.hlsl";

    public string TemporalGaussianShader { get; private set; } = "D3D12TemporalGaussian.hlsl";

    public string GpuSensorFusionShader { get; private set; } = "D3D12GpuSensorFusion.hlsl";

    public string FractalReservoirShader { get; private set; } = "D3D12FractalReservoirCompute.hlsl";

    public string FractalSplatRenderShader { get; private set; } = "D3D12FractalSplatRender.hlsl";

    public string PlanetarySurfacePageShader { get; private set; } = "D3D12PlanetarySurfacePage.hlsl";

    public string PlanetarySurfacePageSummaryShader { get; private set; } = "D3D12PlanetarySurfacePageSummary.hlsl";

    public string PostShader { get; private set; } = "D3D12Post.hlsl";

    public string SdfCommonInclude { get; private set; } = "D3D12SdfCommon.hlsli";

    public string SdfProxyInclude { get; private set; } = "D3D12SdfProxy.hlsli";

    public string SdfMathInclude { get; private set; } = "D3D12SdfMath.hlsli";

    public string? SdfLibraryInclude { get; private set; }

    public IReadOnlyList<string> SdfShaderPaths => sdfShaderPaths;

    public IReadOnlyList<string> IncludePaths => includePaths;

    public AquariumShaderManifest Root(string path)
    {
        ShaderRoot = path;
        return this;
    }

    public AquariumShaderManifest HeightField(string path)
    {
        HeightFieldShader = path;
        return this;
    }

    public AquariumShaderManifest Scene(string path)
    {
        SceneShader = path;
        return this;
    }

    public AquariumShaderManifest TemporalGaussian(string path)
    {
        TemporalGaussianShader = path;
        return this;
    }

    public AquariumShaderManifest GpuSensorFusion(string path)
    {
        GpuSensorFusionShader = path;
        return this;
    }

    public AquariumShaderManifest FractalReservoir(string path)
    {
        FractalReservoirShader = path;
        return this;
    }

    public AquariumShaderManifest FractalSplatRender(string path)
    {
        FractalSplatRenderShader = path;
        return this;
    }

    public AquariumShaderManifest PlanetarySurfacePage(string path)
    {
        PlanetarySurfacePageShader = path;
        return this;
    }

    public AquariumShaderManifest PlanetarySurfacePageSummary(string path)
    {
        PlanetarySurfacePageSummaryShader = path;
        return this;
    }

    public AquariumShaderManifest Post(string path)
    {
        PostShader = path;
        return this;
    }

    public AquariumShaderManifest SdfCommon(string path)
    {
        SdfCommonInclude = path;
        return this;
    }

    public AquariumShaderManifest SdfProxy(string path)
    {
        SdfProxyInclude = path;
        return this;
    }

    public AquariumShaderManifest SdfMath(string path)
    {
        SdfMathInclude = path;
        return this;
    }

    public AquariumShaderManifest SdfLibrary(string? path)
    {
        SdfLibraryInclude = path;
        return this;
    }

    public AquariumShaderManifest Include(string path)
    {
        includePaths.Add(path);
        return this;
    }

    public AquariumShaderManifest SdfShader(string path)
    {
        sdfShaderPaths.Add(path);
        return this;
    }
}

public sealed class AquariumRenderGraphDescription
{
    private readonly List<AquariumRenderTargetDescription> renderTargets = [];
    private readonly List<AquariumCameraDescription> cameras = [];
    private readonly List<AquariumPassDescription> passes = [];
    private readonly List<AquariumDebugViewDescription> debugViews = [];

    public IReadOnlyList<AquariumRenderTargetDescription> RenderTargets => renderTargets;

    public IReadOnlyList<AquariumCameraDescription> Cameras => cameras;

    public IReadOnlyList<AquariumPassDescription> Passes => passes;

    public IReadOnlyList<AquariumDebugViewDescription> DebugViews => debugViews;

    public RenderTargetHandle RenderTarget(string name, RenderFormat format, AquariumTargetSize size, bool sampled = true, int historyFrames = 0)
    {
        var handle = new RenderTargetHandle(name);
        renderTargets.Add(new AquariumRenderTargetDescription(handle, format, size, sampled, historyFrames));
        return handle;
    }

    public CameraHandle Camera(string name)
    {
        var handle = new CameraHandle(name);
        cameras.Add(new AquariumCameraDescription(handle));
        return handle;
    }

    public PassHandle Pass(string name, AquariumPassKind kind)
    {
        var handle = new PassHandle(name);
        passes.Add(new AquariumPassDescription(handle, kind));
        return handle;
    }

    public AquariumRenderGraphDescription DebugView(string name, RenderTargetHandle target)
    {
        debugViews.Add(new AquariumDebugViewDescription(name, target));
        return this;
    }
}

public readonly record struct RenderTargetHandle(string Name);

public readonly record struct DepthTargetHandle(string Name);

public readonly record struct CameraHandle(string Name);

public readonly record struct ShaderHandle(string Name);

public readonly record struct BufferHandle<T>(string Name) where T : unmanaged;

public readonly record struct TextureHandle(string Name);

public readonly record struct PassHandle(string Name);

public readonly record struct AquariumRenderTargetDescription(
    RenderTargetHandle Handle,
    RenderFormat Format,
    AquariumTargetSize Size,
    bool Sampled,
    int HistoryFrames);

public readonly record struct AquariumCameraDescription(CameraHandle Handle);

public readonly record struct AquariumPassDescription(PassHandle Handle, AquariumPassKind Kind);

public readonly record struct AquariumDebugViewDescription(string Name, RenderTargetHandle Target);

public readonly record struct AquariumTargetSize(AquariumTargetSizeKind Kind, int Width, int Height, float Scale)
{
    public static AquariumTargetSize Fixed(int width, int height) => new(AquariumTargetSizeKind.Fixed, width, height, 1.0f);

    public static AquariumTargetSize MatchWindow(float scale = 1.0f) => new(AquariumTargetSizeKind.MatchWindow, 0, 0, scale);
}

public enum AquariumTargetSizeKind
{
    Fixed,
    MatchWindow,
}

public enum RenderFormat
{
    Unknown,
    R16Float,
    Rgba16Float,
    Bgra8Unorm,
    Depth32Float,
}

public enum AquariumPassKind
{
    Fullscreen,
    Proxy,
    Instanced,
    Compute,
    Copy,
    Present,
    Overlay,
}

[StructLayout(LayoutKind.Sequential)]
public readonly record struct AquariumSdfLight(
    Vector4 CenterRadius,
    Vector4 RadianceFieldId);

[StructLayout(LayoutKind.Sequential)]
public readonly record struct AquariumSdfObject(
    Vector4 CenterRadius,
    Vector4 PreviousCenterPad,
    Vector4 State);

public readonly record struct AquariumHeightFieldBrush(
    Vector2 Center,
    float Radius,
    float Power,
    float Amplitude,
    float WaveAmplitude,
    float WaveFrequency,
    float WaveSpeed,
    float WaveSinePower,
    float RadiusY = 0.0f,
    float RotationRadians = 0.0f,
    float EnvelopeFalloff = 0.0f,
    float DomainFace = -1.0f,
    float DomainLevel = 0.0f,
    float DomainX = 0.0f,
    float DomainY = 0.0f);

public readonly record struct AquariumTemporalSdfGaussian(
    string StableKey,
    Vector3 Center,
    Vector3 PreviousCenter,
    Vector3 Velocity,
    Vector3 Radii,
    Quaternion Orientation,
    Vector4 ColorOpacity,
    float Confidence,
    float HistoryWeight,
    float LastObservedTimeSeconds,
    float Falloff,
    float ShapePower,
    int FieldId);

public readonly record struct AquariumGpuFusionSeed(
    string StableKey,
    Vector3 Center,
    Vector3 PreviousCenter,
    Vector3 Velocity,
    Vector3 Radii,
    Vector4 ColorOpacity,
    float Confidence,
    float HistoryWeight,
    float Falloff,
    float ShapePower,
    int FieldId);

public enum AquariumGpuSensorKind
{
    Unknown,
    RgbCamera,
    HighRateTracker,
    LeapPackedMap,
}

public enum AquariumGpuSensorPixelFormat
{
    Unknown,
    Bgra8Unorm,
    Rgba8Unorm,
    R8Unorm,
    R16Unorm,
    R16Float,
    Rg8Unorm,
    LeapPackedMap,
    Yuy2,
}

public readonly record struct AquariumExternalGpuTexture(
    TextureHandle Handle,
    IntPtr SharedHandle,
    int Width,
    int Height,
    AquariumGpuSensorPixelFormat PixelFormat,
    long TimestampNs,
    int PlaneIndex = 0,
    string SharedHandleName = "");

public readonly record struct AquariumGpuSensorCamera(
    string SensorId,
    AquariumGpuSensorKind Kind,
    Matrix4x4 WorldFromSensor,
    Matrix4x4 SensorFromWorld,
    Vector4 Intrinsics,
    Vector4 Distortion01,
    Vector4 Distortion23,
    int Width,
    int Height,
    int FirstTextureIndex,
    int TextureCount,
    long TimestampNs,
    AquariumGpuSensorPixelFormat PixelFormat = AquariumGpuSensorPixelFormat.Unknown);

public sealed class AquariumGpuSensorFrame
{
    public static AquariumGpuSensorFrame Empty { get; } = new();

    public IReadOnlyList<AquariumGpuSensorCamera> Cameras { get; init; } = [];

    public IReadOnlyList<AquariumExternalGpuTexture> ExternalTextures { get; init; } = [];

    public float AccumulationWindowSeconds { get; init; }

    public float PresentationDelaySeconds { get; init; }

    public bool HasInput => Cameras.Count > 0 || ExternalTextures.Count > 0;
}

public enum AquariumAcousticConstraintKind
{
    Unknown,
    UltrasonicReflector,
    VoiceTdoa,
    SpeakerProbe,
    ClapImpact,
}

public readonly record struct AquariumAcousticConstraint(
    string StableKey,
    AquariumAcousticConstraintKind Kind,
    Vector3 Position,
    Vector3 Velocity,
    float RadiusMeters,
    float Confidence,
    long TimestampNs);

public sealed class AquariumAcousticFieldFrame
{
    public static AquariumAcousticFieldFrame Empty { get; } = new();

    public IReadOnlyList<AquariumAcousticConstraint> Constraints { get; init; } = [];

    public long TimingOracleNs { get; init; }

    public float TimingConfidence { get; init; }

    public float TimingUncertaintyMicroseconds { get; init; }

    public float AccumulationWindowSeconds { get; init; }

    public float PresentationDelaySeconds { get; init; }

    public bool HasInput => Constraints.Count > 0;
}

public readonly record struct AquariumClapCalibrationEvent(
    string StableKey,
    Vector3 Position,
    long AcousticOracleNs,
    long VisualObservedNs,
    float TimingUncertaintyMicroseconds,
    float VisualConfidence,
    float AcousticConfidence);

public sealed class AquariumCalibrationEventFrame
{
    public static AquariumCalibrationEventFrame Empty { get; } = new();

    public IReadOnlyList<AquariumClapCalibrationEvent> ClapEvents { get; init; } = [];

    public bool HasInput => ClapEvents.Count > 0;
}

public sealed class AquariumGpuFusionField
{
    public static AquariumGpuFusionField Empty { get; } = new();

    public IReadOnlyList<AquariumGpuFusionSeed> Seeds { get; init; } = [];

    public AquariumGpuFusionPointBuffer PointBuffer { get; init; }

    public float AccumulationWindowSeconds { get; init; }

    public float PresentationDelaySeconds { get; init; }

    public bool HasInput => Seeds.Count > 0 || PointBuffer.HasInput;
}

public readonly record struct AquariumGpuFusionPointBuffer(
    IntPtr Buffer,
    int Count,
    int StrideBytes)
{
    // Caller owns the memory and must keep it stable until the render frame has uploaded it.
    public bool HasInput => Buffer != IntPtr.Zero && Count > 0 && StrideBytes > 0;
}

public sealed class AquariumTemporalGaussianField
{
    public static AquariumTemporalGaussianField Empty { get; } = new();

    public IReadOnlyList<AquariumTemporalSdfGaussian> Gaussians { get; init; } = [];

    public float AccumulationWindowSeconds { get; init; }

    public float PresentationDelaySeconds { get; init; }
}

public sealed class AquariumFractalReservoirField
{
    public static AquariumFractalReservoirField Empty { get; } = new();

    public int SplatCount { get; init; }

    public int Depth { get; init; } = 8;

    public uint Seed { get; init; } = 0xA17EA11u;

    public int CandidatesPerReservoirUpdate { get; init; } = 2;

    public int SplatUpdatesPerFrame { get; init; }

    public int ReservoirUpdatesPerPass { get; init; }

    public IReadOnlyList<AquariumPackedFractalIfsTransform> ProgramTransforms { get; init; } = [];

    public uint ProgramMode { get; init; }

    public Vector4 WorldCenterRadius { get; init; }

    public Vector4 PriorityFocus { get; init; }

    public bool HasInput =>
        SplatCount > 0 &&
        Depth > 0 &&
        CandidatesPerReservoirUpdate > 0 &&
        SplatUpdatesPerFrame > 0 &&
        SplatUpdatesPerFrame <= SplatCount &&
        ReservoirUpdatesPerPass > 0 &&
        ReservoirUpdatesPerPass <= SplatCount;
}

public sealed class AquariumSceneState
{
    public static AquariumSceneState Empty { get; } = new();

    public bool TraceHeightFieldSurface { get; init; } = true;

    public bool UseStudioBackground { get; init; } = true;

    public bool UseStarfieldBackground { get; init; }

    public IReadOnlyList<AquariumHeightFieldBrush> HeightFieldBrushes { get; init; } = [];

    public IReadOnlyList<AquariumSdfObject> SdfObjects { get; init; } = [];

    public IReadOnlyList<AquariumSdfLight> SdfLights { get; init; } = [];

    public AquariumTemporalGaussianField TemporalGaussianField { get; init; } = AquariumTemporalGaussianField.Empty;

    public AquariumFractalReservoirField FractalReservoirField { get; init; } = AquariumFractalReservoirField.Empty;

    public AquariumPlanetarySurfacePageSet PlanetarySurfacePages { get; init; } = AquariumPlanetarySurfacePageSet.Empty;

    public AquariumGpuSensorFrame GpuSensorFrame { get; init; } = AquariumGpuSensorFrame.Empty;

    public AquariumAcousticFieldFrame AcousticFieldFrame { get; init; } = AquariumAcousticFieldFrame.Empty;

    public AquariumCalibrationEventFrame CalibrationEventFrame { get; init; } = AquariumCalibrationEventFrame.Empty;

    public AquariumGpuFusionField GpuFusionField { get; init; } = AquariumGpuFusionField.Empty;

    public AquariumFieldEvidenceFrame FieldEvidenceFrame { get; init; } = AquariumFieldEvidenceFrame.Empty;

    public AquariumBufferFieldFrame BufferFieldFrame { get; init; } = AquariumBufferFieldFrame.Empty;

    public AquariumBokushoBrushFrame BokushoBrushFrame { get; init; } = AquariumBokushoBrushFrame.Empty;

    public AquariumSplineFrame SplineFrame { get; init; } = AquariumSplineFrame.Empty;
}

[StructLayout(LayoutKind.Sequential)]
public readonly record struct AquariumPlanetarySurfacePageInput(Vector4 DirectionRadius, Vector4 Sampling);

[StructLayout(LayoutKind.Sequential)]
public readonly record struct AquariumPlanetarySurfacePageMetadata(Vector4 Address, Vector4 Layout, Vector4 Bounds, Vector4 State);

public sealed class AquariumPlanetarySurfacePageSet
{
    public static AquariumPlanetarySurfacePageSet Empty { get; } = new();
    public string GeneratorEntryPoint { get; init; } = "D3D12PlanetaryTerrainPageCS";
    public long Version { get; init; }
    public IReadOnlyList<AquariumPlanetarySurfacePageInput> Samples { get; init; } = [];
    public AquariumPlanetarySurfacePageMetadata Metadata { get; init; }
    public bool HasInput => Version > 0 && Samples.Count > 0;
}

public sealed class AquariumBokushoBrushFrame
{
    public static AquariumBokushoBrushFrame Empty { get; } = new();

    public int TuftCount { get; init; }

    public int SampleCount { get; init; }

    public float PhysicsHz { get; init; } = 500.0f;

    public float BrushRadius { get; init; } = 2.2f;

    public float Pressure { get; init; } = 0.72f;

    public float InkLoad { get; init; } = 0.90f;

    public float Wetness { get; init; } = 0.86f;

    public float Splay { get; init; } = 0.72f;

    public float Bend { get; init; } = 0.58f;

    public float Friction { get; init; } = 0.62f;

    public float RadiusScale { get; init; } = 1.0f;

    public float PressureScale { get; init; } = 1.0f;

    public float NormalScale { get; init; } = 1.0f;

    public float TangentScale { get; init; } = 1.0f;

    public Vector4 StrokeP0 { get; init; } = new(-7.2f, 1.0f, 0.0f, 0.0f);

    public Vector4 StrokeP1 { get; init; } = new(-2.4f, -1.8f, 0.0f, 0.0f);

    public Vector4 StrokeP2 { get; init; } = new(2.4f, -1.6f, 0.0f, 0.0f);

    public Vector4 StrokeP3 { get; init; } = new(7.2f, 0.8f, 0.0f, 0.0f);

    public IReadOnlyList<AquariumBokushoBrushStroke> Strokes { get; init; } = [];

    public IReadOnlyList<AquariumBokushoSourcePoint> SourcePoints { get; init; } = [];

    public bool HasInput => TuftCount > 0 && SampleCount > 1 && PhysicsHz > 0.0f;

    public AquariumBokushoBrushFrame Normalized() => new()
    {
        TuftCount = Math.Clamp(TuftCount, 1, 4096),
        SampleCount = Math.Clamp(SampleCount, 2, 4096),
        PhysicsHz = Math.Clamp(PhysicsHz, 60.0f, 2000.0f),
        BrushRadius = MathF.Max(0.0001f, BrushRadius),
        Pressure = Math.Clamp(Pressure, 0.0f, 2.0f),
        InkLoad = Math.Clamp(InkLoad, 0.0f, 3.0f),
        Wetness = Math.Clamp(Wetness, 0.0f, 1.6f),
        Splay = Math.Clamp(Splay, 0.0f, 2.0f),
        Bend = Math.Clamp(Bend, 0.0f, 2.4f),
        Friction = Math.Clamp(Friction, 0.0f, 1.0f),
        RadiusScale = Math.Clamp(RadiusScale, 0.1f, 4.0f),
        PressureScale = Math.Clamp(PressureScale, 0.1f, 4.0f),
        NormalScale = Math.Clamp(NormalScale, 0.1f, 4.0f),
        TangentScale = Math.Clamp(TangentScale, 0.1f, 4.0f),
        StrokeP0 = StrokeP0,
        StrokeP1 = StrokeP1,
        StrokeP2 = StrokeP2,
        StrokeP3 = StrokeP3,
        Strokes = Strokes.Select(stroke => stroke.Normalized()).ToArray(),
        SourcePoints = SourcePoints.Select(point => point.Normalized()).ToArray(),
    };
}

public sealed class AquariumBokushoSourcePoint
{
    public int SourceStrokeId { get; init; } = -1;

    public float T { get; init; }

    public Vector2 Position { get; init; }

    public AquariumBokushoSourcePoint Normalized() => new()
    {
        SourceStrokeId = SourceStrokeId,
        T = Math.Clamp(T, 0.0f, 1.0f),
        Position = Position,
    };
}

public sealed class AquariumBokushoBrushStroke
{
    public Vector4 StrokeP0 { get; init; } = new(-7.2f, 1.0f, 0.0f, 0.0f);

    public Vector4 StrokeP1 { get; init; } = new(-2.4f, -1.8f, 0.0f, 0.0f);

    public Vector4 StrokeP2 { get; init; } = new(2.4f, -1.6f, 0.0f, 0.0f);

    public Vector4 StrokeP3 { get; init; } = new(7.2f, 0.8f, 0.0f, 0.0f);

    public float RadiusScale { get; init; } = 1.0f;

    public float PressureScale { get; init; } = 1.0f;

    public float NormalScale { get; init; } = 1.0f;

    public float TangentScale { get; init; } = 1.0f;

    public float ShaftTilt { get; init; }

    public float ShaftRotation { get; init; }

    public float GripHeight { get; init; } = 1.0f;

    public float Compliance { get; init; } = 1.0f;

    public float EntryTaper { get; init; } = 0.10f;

    public float ExitTaper { get; init; } = 0.14f;

    public float PigmentScale { get; init; } = 1.0f;

    public float SplitScale { get; init; } = 1.0f;

    public float SegmentStart { get; init; }

    public float SegmentEnd { get; init; } = 1.0f;

    public int SourceStrokeId { get; init; } = -1;

    public AquariumBokushoBrushStroke Normalized() => new()
    {
        StrokeP0 = StrokeP0,
        StrokeP1 = StrokeP1,
        StrokeP2 = StrokeP2,
        StrokeP3 = StrokeP3,
        RadiusScale = Math.Clamp(RadiusScale, 0.1f, 4.0f),
        PressureScale = Math.Clamp(PressureScale, 0.1f, 4.0f),
        NormalScale = Math.Clamp(NormalScale, 0.1f, 4.0f),
        TangentScale = Math.Clamp(TangentScale, 0.1f, 4.0f),
        ShaftTilt = Math.Clamp(ShaftTilt, -1.5f, 1.5f),
        ShaftRotation = Math.Clamp(ShaftRotation, -2.0f, 2.0f),
        GripHeight = Math.Clamp(GripHeight, 0.2f, 2.4f),
        Compliance = Math.Clamp(Compliance, 0.1f, 2.5f),
        EntryTaper = Math.Clamp(EntryTaper, 0.01f, 0.50f),
        ExitTaper = Math.Clamp(ExitTaper, 0.01f, 0.50f),
        PigmentScale = Math.Clamp(PigmentScale, 0.05f, 4.0f),
        SplitScale = Math.Clamp(SplitScale, 0.0f, 4.0f),
        SegmentStart = Math.Clamp(SegmentStart, 0.0f, 1.0f),
        SegmentEnd = Math.Clamp(SegmentEnd, 0.0f, 1.0f),
        SourceStrokeId = SourceStrokeId,
    };
}

public sealed class AquariumBufferFieldFrame
{
    public static AquariumBufferFieldFrame Empty { get; } = new();

    public static AquariumBufferFieldFrame Compose(Action<AquariumBufferFieldFrameBuilder> compose)
    {
        var builder = new AquariumBufferFieldFrameBuilder();
        compose(builder);
        return builder.Build();
    }

    public IReadOnlyList<AquariumSplineTubeField> SplineTubeFields { get; init; } = [];

    public IReadOnlyList<AquariumTextureFieldBinding> Textures { get; init; } = [];

    public IReadOnlyList<AquariumTextureSplineFieldProgram> TextureSplineFields { get; init; } = [];

    public AquariumFractalReservoirField Reservoir { get; init; } = AquariumFractalReservoirField.Empty;

    public AquariumFieldLoweringPolicy LoweringPolicy { get; init; } = AquariumFieldLoweringPolicy.Default;

    public bool UseReservoirLowering => LoweringPolicy.Normalized().Mode == AquariumFieldLoweringMode.ReservoirSplats;

    public string? SourceScript { get; init; }

    public bool HasInput => SplineTubeFields.Count > 0 || TextureSplineFields.Count > 0;
}

public enum AquariumFieldLoweringMode
{
    Auto = 0,
    DirectSdfTubes = 1,
    ReservoirSplats = 2,
    Mesh = 3,
}

public sealed record AquariumFieldLoweringPolicy(
    AquariumFieldLoweringMode Mode,
    int MaxDirectSplines,
    int MaxDirectControlPoints,
    int MaxReservoirSplats,
    float LodBias)
{
    public static AquariumFieldLoweringPolicy Default { get; } = new(
        AquariumFieldLoweringMode.Auto,
        MaxDirectSplines: 512,
        MaxDirectControlPoints: 65_536,
        MaxReservoirSplats: 131_072,
        LodBias: 1.0f);

    public AquariumFieldLoweringPolicy Normalized() => new(
        Mode,
        Math.Clamp(MaxDirectSplines, 1, 65_536),
        Math.Clamp(MaxDirectControlPoints, 2, 8_388_608),
        Math.Clamp(MaxReservoirSplats, 1, 4_194_304),
        Math.Clamp(LodBias, 0.05f, 16.0f));
}

public enum AquariumTextureAxis
{
    X = 0,
    Y = 1,
}

public enum AquariumRollingModuloMode
{
    None = 0,
    Columns = 1,
    Rows = 2,
}

public sealed record AquariumTextureFieldBinding(
    string Id,
    int Width,
    int Height,
    int Channels,
    int RollingOffset,
    AquariumRollingModuloMode RollingMode,
    IReadOnlyList<float> Samples)
{
    public bool HasInput =>
        !string.IsNullOrWhiteSpace(Id) &&
        Width > 0 &&
        Height > 0 &&
        Channels > 0 &&
        Samples.Count >= Width * Height * Channels;
}

public sealed record AquariumTextureSplineFieldProgram(
    string Id,
    string TextureId,
    AquariumTextureAxis FrequencyAxis,
    int FirstColumn,
    int ColumnCount,
    int ColumnStride,
    int RollingWindowModulo,
    int Subdivisions,
    Vector3 Origin,
    Vector3 AxisStep,
    Vector3 ColumnStep,
    int ColumnGroupSize,
    Vector3 ColumnGroupStep,
    float AmplitudeScale,
    AquariumSplineTubeAppearance Appearance,
    AquariumSplineTubeProbePolicy ProbePolicy,
    IReadOnlyList<AquariumFieldGraphNode> SurfaceGraph);

public sealed record AquariumFieldGraphNode(
    string Id,
    string Op,
    IReadOnlyList<string> Inputs,
    Vector4 Value);

public readonly record struct AquariumPackedTextureSplineFieldProgram(
    Vector4 DimensionsAxisMode,
    Vector4 AxisModeOffset,
    Vector4 ColumnModulo,
    Vector4 SubdivisionProbe,
    Vector4 OriginAmplitude,
    Vector4 AxisStepRadius,
    Vector4 ColumnStepAlpha,
    Vector4 ColumnGroup,
    Vector4 Emission,
    Vector4 SurfaceWeights0,
    Vector4 SurfaceWeights1);

public sealed record AquariumSplineTubeField(
    string Id,
    string BufferId,
    AquariumSpline3D Spline,
    AquariumFieldDomainBinding Domain,
    AquariumSplineTubeAppearance Appearance,
    AquariumSplineTubeProbePolicy ProbePolicy);

public readonly record struct AquariumFieldDomainBinding(
    string SplineDomain,
    string ObjectDomain,
    string ParentDomain,
    Matrix4x4 ObjectToParent,
    Matrix4x4 ParentToWorld)
{
    public static AquariumFieldDomainBinding Identity(string splineDomain, string objectDomain, string parentDomain) => new(
        splineDomain,
        objectDomain,
        parentDomain,
        Matrix4x4.Identity,
        Matrix4x4.Identity);
}

public readonly record struct AquariumSplineTubeAppearance(
    Vector4 Emission,
    float Radius,
    float Alpha,
    float ZeroThreshold,
    float Feather,
    float TangentWeight,
    float CurvatureWeight,
    float NormalWeight,
    float DerivativeWeight)
{
    public static AquariumSplineTubeAppearance Default { get; } = new(
        new Vector4(1.0f, 0.84f, 0.32f, 1.0f),
        0.018f,
        1.0f,
        0.78f,
        0.22f,
        1.0f,
        0.35f,
        0.25f,
        0.50f);

    public AquariumSplineTubeAppearance Normalized() => new(
        Emission,
        MathF.Max(0.0001f, Radius),
        Math.Clamp(Alpha, 0.0f, 1.0f),
        Math.Clamp(ZeroThreshold, 0.0f, 1.0f),
        MathF.Max(0.0001f, Feather),
        MathF.Max(0.0f, TangentWeight),
        MathF.Max(0.0f, CurvatureWeight),
        MathF.Max(0.0f, NormalWeight),
        MathF.Max(0.0f, DerivativeWeight));
}

public readonly record struct AquariumSplineTubeProbePolicy(
    int MaxProbeCount,
    float BaseDensity,
    float MinimumVisualContribution,
    uint Seed)
{
    public static AquariumSplineTubeProbePolicy Default { get; } = new(64, 1.0f, 0.01f, 0xB11FF13Du);

    public AquariumSplineTubeProbePolicy Normalized() => new(
        Math.Clamp(MaxProbeCount, 1, 4096),
        MathF.Max(0.0f, BaseDensity),
        MathF.Max(0.0f, MinimumVisualContribution),
        Seed);
}

public sealed class AquariumBufferFieldFrameBuilder
{
    private readonly List<AquariumSplineTubeField> splineTubeFields = [];
    private readonly List<AquariumTextureFieldBinding> textures = [];
    private readonly List<AquariumTextureSplineFieldProgram> textureSplineFields = [];
    private AquariumFractalReservoirField reservoir = AquariumFractalReservoirField.Empty;
    private AquariumFieldLoweringPolicy loweringPolicy = AquariumFieldLoweringPolicy.Default;
    private string? sourceScript;

    public AquariumBufferFieldFrameBuilder SplineTube(
        string id,
        string bufferId,
        AquariumSpline3D spline,
        AquariumFieldDomainBinding domain,
        AquariumSplineTubeAppearance? appearance = null,
        AquariumSplineTubeProbePolicy? probePolicy = null)
    {
        splineTubeFields.Add(new AquariumSplineTubeField(
            id,
            bufferId,
            spline,
            domain,
            (appearance ?? AquariumSplineTubeAppearance.Default).Normalized(),
            (probePolicy ?? AquariumSplineTubeProbePolicy.Default).Normalized()));
        return this;
    }

    public AquariumBufferFieldFrameBuilder Texture(
        string id,
        int width,
        int height,
        int channels,
        int rollingOffset,
        AquariumRollingModuloMode rollingMode,
        IReadOnlyList<float> samples)
    {
        var texture = new AquariumTextureFieldBinding(id, width, height, channels, rollingOffset, rollingMode, samples);
        if (!texture.HasInput)
        {
            throw new ArgumentException($"Texture field `{id}` has invalid dimensions or sample count.", nameof(samples));
        }

        textures.Add(texture);
        return this;
    }

    public AquariumBufferFieldFrameBuilder TextureSplineField(AquariumTextureSplineFieldProgram program)
    {
        textureSplineFields.Add(program);
        return this;
    }

    public AquariumBufferFieldFrameBuilder Reservoir(AquariumFractalReservoirField field)
    {
        reservoir = field;
        loweringPolicy = loweringPolicy with { Mode = field.HasInput ? AquariumFieldLoweringMode.ReservoirSplats : AquariumFieldLoweringMode.Auto };
        return this;
    }

    public AquariumBufferFieldFrameBuilder DirectSdfSurfaces()
    {
        loweringPolicy = loweringPolicy with { Mode = AquariumFieldLoweringMode.DirectSdfTubes };
        return this;
    }

    public AquariumBufferFieldFrameBuilder Lowering(AquariumFieldLoweringPolicy policy)
    {
        loweringPolicy = policy.Normalized();
        return this;
    }

    public AquariumBufferFieldFrameBuilder Script(string script)
    {
        sourceScript = script;
        return this;
    }

    public AquariumBufferFieldFrame Build() => new()
    {
        SplineTubeFields = splineTubeFields,
        Textures = textures,
        TextureSplineFields = textureSplineFields,
        Reservoir = reservoir,
        LoweringPolicy = loweringPolicy,
        SourceScript = sourceScript,
    };
}

public sealed class AquariumSplineFrame
{
    public static AquariumSplineFrame Empty { get; } = new();

    public static AquariumSplineFrame Compose(Action<AquariumSplineFrameBuilder> compose)
    {
        var builder = new AquariumSplineFrameBuilder();
        compose(builder);
        return builder.Build();
    }

    public IReadOnlyList<AquariumSpline3D> Splines { get; init; } = [];

    public bool HasInput => Splines.Count > 0;
}

public sealed record AquariumSpline3D(
    string Id,
    IReadOnlyList<AquariumSplineVertex> Vertices,
    AquariumSplineStyle Style,
    int CatmullRomSubdivisions = 4);

public readonly record struct AquariumSplineVertex(
    Vector3 Position,
    Vector4 Color);

public readonly record struct AquariumSplineStyle(
    float Radius,
    float Emission,
    float Alpha,
    float GlowNormalExponent,
    float AlphaNormalExponent,
    float Feather)
{
    public AquariumSplineStyle(
        float radius,
        float emission,
        float alpha,
        float normalExponent,
        float feather)
        : this(radius, emission, alpha, normalExponent, normalExponent, feather)
    {
    }

    public static AquariumSplineStyle Default { get; } = new(0.018f, 1.0f, 1.0f, 1.0f, 1.0f, 0.12f);

    public AquariumSplineStyle Normalized() => new(
        MathF.Max(0.0001f, Radius),
        MathF.Max(0.0f, Emission),
        Math.Clamp(Alpha, 0.0f, 1.0f),
        MathF.Max(0.0001f, GlowNormalExponent),
        MathF.Max(0.0001f, AlphaNormalExponent),
        MathF.Max(0.0001f, Feather));
}

public sealed class AquariumSplineFrameBuilder
{
    private readonly List<AquariumSpline3D> splines = [];

    public AquariumSplineFrameBuilder CatmullRom(
        string id,
        IReadOnlyList<AquariumSplineVertex> vertices,
        AquariumSplineStyle? style = null,
        int subdivisions = 4)
    {
        splines.Add(new AquariumSpline3D(id, vertices, (style ?? AquariumSplineStyle.Default).Normalized(), Math.Clamp(subdivisions, 1, 16)));
        return this;
    }

    public AquariumSplineFrame Build() => new() { Splines = splines };
}
