using Aquarium.Engine.Audio;
using Aquarium.Engine.Fractal;
using Aquarium.Engine.Input;
using Aquarium.Engine.Render.Features;
using Aquarium.Engine.Render.Graph;
using SharpGen.Runtime;
using Aquarium.Engine.Render.Ui;
using Aquarium.Engine.Ui;
using CultMath;
using Vortice;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11on12;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.Mathematics;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using ID3D11Device = Vortice.Direct3D11.ID3D11Device;
using ID3D11DeviceContext = Vortice.Direct3D11.ID3D11DeviceContext;
using ID3D11Resource = Vortice.Direct3D11.ID3D11Resource;
using D3D11DeviceCreationFlags = Vortice.Direct3D11.DeviceCreationFlags;
using D3D11BindFlags = Vortice.Direct3D11.BindFlags;

namespace Aquarium.Engine.Render;

public sealed class D3D12Renderer : IAquariumRenderer
{
    private const int BackBufferCount = 2;
    private const int HeightFieldTextureSize = 512;
    private const Format HeightFieldFormat = Format.R16_Float;
    private const Format SceneDepthFormat = Format.D32_Float;
    private const int MaxSdfLightCount = 64;
    private const int MaxSdfObjectCount = 64;
    private const int MaxGpuSensorCameraCount = 32;
    private const int MaxGpuSensorTextureCount = 8;
    private const int MaxAcousticConstraintCount = 128;
    private const int GpuSensorSamplesPerTexture = 16_384;
    private const int MaxTemporalGaussianCount = 1_048_576;
    private const int MaxVisibleFractalSplatCount = 524_288;
    private const int MaxTubeFieldSegments = 65_536;
    private const int MaxTubeFieldVertices = MaxTubeFieldSegments * 4;
    private const int MaxTubeFieldIndices = MaxTubeFieldSegments * 6;
    private const int MaxTubeFieldDrawBatches = 4_096;
    private const int MaxTubeFieldReplaySources = 16;
    private const int MaxBokushoBrushTufts = 1024;
    private const int MaxBokushoBrushSamples = 512;
    private const int MaxBokushoBrushStrokes = 64;
    private const int MaxBokushoBrushValues = MaxBokushoBrushTufts * MaxBokushoBrushSamples;
    private const int FieldReservoirSlotsPerPixel = 4;
    private const int FieldReservoirCandidateStrideBytes = 144;
    private const int FieldReservoirHistoryStrideBytes = 144;
    private const int GeneratedMeshDrawArgumentUIntCount = 5;
    private const int GeneratedMeshDrawArgumentBytes = GeneratedMeshDrawArgumentUIntCount * sizeof(uint);
    private const float SurfaceTransparentMinZ = -1.85f;
    private const float SurfaceTransparentMaxZ = 0.45f;
    private const int BloomLevelCount = 8;
    private const Format SceneHdrFormat = Format.R16G16B16A16_Float;
    private const Format SceneOverdrawFormat = Format.R16_Float;
    private const string StudioPmremRelativePath = "Assets/Textures/studio3_pmrem.dds";
    private const string StudioIrradianceRelativePath = "Assets/Textures/studio3_irradiance.dds";
    private const string ProgramOutputEnabledEnvironmentVariable = "FENSALIR_PROGRAM_OUTPUT_D3D12";
    private const string ProgramOutputSharedNameEnvironmentVariable = "FENSALIR_PROGRAM_OUTPUT_NAME";
    private const string ProgramOutputFenceNameEnvironmentVariable = "FENSALIR_PROGRAM_OUTPUT_FENCE_NAME";
    private const string ProgramOutputConsumerFenceNameEnvironmentVariable = "FENSALIR_PROGRAM_OUTPUT_CONSUMER_FENCE_NAME";
    private const string ProgramOutputRingCountEnvironmentVariable = "FENSALIR_PROGRAM_OUTPUT_RING_COUNT";
    private const string DefaultProgramOutputSharedName = "Global\\MimirFensalirProgramTexture";
    private const string DefaultProgramOutputFenceName = "Global\\MimirFensalirProgramFence";
    private const int MaxProgramOutputRingCount = 4;
    private const int GpuTimingQueryCount = (int)D3D12GpuTimingPass.Count * 2;
    private const int RootFrameConstants = 0;
    private const int RootSourceTexture = 1;
    private const int RootHeightFieldBrushes = 2;
    private const int RootSdfLights = 3;
    private const int RootBloom = 4;
    private const int RootCurrentSceneMetadata = 5;
    private const int RootCurrentSceneControl = 6;
    private const int RootReservoirHistoryRead = 7;
    private const int RootReservoirHistoryWrite = 8;
    private const int RootStudioPmrem = 9;
    private const int RootStudioIrradiance = 10;
    private const int RootSdfObjects = 11;
    private const int RootTemporalGaussians = 12;
    private const int RootCurrentReservoirGuide = 13;
    private const int RootFractalSplatSrv = 14;
    private const int RootFractalSdfReservoirSrv = 15;
    private const int RootFractalPbrReservoirSrv = 16;
    private const int RootFractalRadiosityReservoirSrv = 17;
    private const int RootBlueNoise = 18;
    private const int RootFieldReservoirCandidates = 19;
    private const int RootReservoirResolvedOutput = 20;
    private const int RootTubeFieldReplayManifest = 21;
    private const int RootTubeFieldReplaySources = 22;
    private const int RootSceneOverdraw = 23;
    private const int RootFusionFrameConstants = 0;
    private const int RootFusionSeeds = 1;
    private const int RootFusionSensorCameras = 2;
    private const int RootFusionSensorTextures = 3;
    private const int RootFusionAcousticConstraints = 4;
    private const int RootFusionOutput = 5;
    private const int RootFusionNativePoints = 6;
    private const int RootPointCloudFrameConstants = 0;
    private const int RootPointCloudConstants = 1;
    private const int RootPointCloudDisparity = 2;
    private const int RootPointCloudVertices = 3;
    private const int RootPointCloudIndices = 4;
    private const int RootPointCloudFractalSplats = 5;
    private const int RootPointCloudSdfReservoirs = 6;
    private const int RootPointCloudPbrReservoirs = 7;
    private const int RootPointCloudRadiosityReservoirs = 8;
    private const int RootFractalConstants = 0;
    private const int RootFractalSplats = 1;
    private const int RootFractalSdfReservoirs = 2;
    private const int RootFractalPbrReservoirs = 3;
    private const int RootFractalRadiosityReservoirs = 4;
    private const int RootFractalFlameStates = 5;
    private const int RootFractalProgramTransforms = 6;
    private const int RootFractalTextureSplinePrograms = 7;
    private const int RootFractalTextureSamples = 8;
    private const int RootTubeFieldConstants = 0;
    private const int RootTubeFieldSource = 1;
    private const int RootTubeFieldVertices = 2;
    private const int RootTubeFieldIndices = 3;
    private const int RootTubeFieldStats = 4;
    private const int RootTubeFieldDrawArguments = 5;
    private const int RootTubeFieldRamp = 6;
    private const int RootTubeFieldSegments = 7;
    private const int RootTubeFieldRenderFrameConstants = 0;
    private const int RootTubeFieldRenderConstants = 1;
    private const int RootTubeFieldRenderSource = 2;
    private const int RootTubeFieldRenderRamp = 3;
    private const int RootTubeFieldRenderBlueNoise = 4;
    private const int RootTubeFieldRenderReservoirCandidates = 5;
    private const int RootTubeFieldRenderReservoirLocks = 6;
    private const int RootBokushoBrushConstants = 0;
    private const int RootBokushoBrushTrace = 1;
    private const int RootBokushoBrushCanvas = 2;
    private const int RootBokushoBrushStrokes = 3;
    private const int RootBokushoPageConstants = 24;
    private const int RootBokushoPageCanvas = 25;
    private const int RootBokushoPageStrokes = 26;
    private static readonly DebugUi.DebugUiOption[] RenderDebugOptions =
    [
        new(0, "Final"),
        new(1, "Raw Scene"),
        new(2, "Reservoir History"),
        new(3, "Reservoir Age"),
        new(4, "Reservoir Weight"),
        new(5, "Coverage/Steps"),
        new(6, "Lane Identity"),
        new(7, "Bloom"),
        new(8, "Exposed Luminance"),
        new(9, "SdfObject Identity"),
        new(10, "SdfObject Steps"),
        new(11, "Client Fractal Domains"),
        new(12, "Reservoir Guide"),
        new(13, "Reservoir Rejection"),
        new(14, "Reservoir Stats"),
        new(15, "Reservoir Proposal"),
        new(16, "Reservoir Domain"),
        new(17, "Reservoir Support"),
        new(18, "Reservoir Shift"),
        new(19, "Reservoir Disocclusion"),
        new(20, "Reservoir Spatial Budget"),
        new(21, "Overdraw"),
    ];
    private static readonly Format[] SceneEvidenceRenderTargetFormats =
    [
        SceneHdrFormat,
        SceneHdrFormat,
        SceneHdrFormat,
        SceneHdrFormat,
        SceneOverdrawFormat,
    ];
    private static readonly DebugUi.DebugUiOption[] FieldReservoirModeOptions =
    [
        new(GraphicsSettings.FieldReservoirModeNativeDomain, "Native Domain"),
        new(GraphicsSettings.FieldReservoirModeTexelBaseline, "Texel Baseline"),
    ];
    private static readonly DebugUi.DebugUiOption[] SynthPresetOptions = AquaSynth.Dsl.BuiltInScripts.ReferenceScripts()
        .Select((preset, index) => new DebugUi.DebugUiOption(index, $"{preset.Family}/{preset.Name}"))
        .ToArray();
    private static readonly (string Family, string Name, string Script)[] SynthPresets = AquaSynth.Dsl.BuiltInScripts.ReferenceScripts().ToArray();

    private readonly IDXGIFactory4 factory;
    private readonly ID3D12Device device;
    private readonly ID3D12CommandQueue commandQueue;
    private readonly ID3D11Device overlayDevice;
    private readonly ID3D11DeviceContext overlayContext;
    private readonly ID3D11On12Device overlayOn12Device;
    private readonly IDXGISwapChain3 swapChain;
    private readonly D3D12ResourceRegistry resourceRegistry = new();
    private readonly D3D12FieldResourceRegistry fieldResourceRegistry = new();
    private D3D12DescriptorArena renderTargetViewArena;
    private D3D12DescriptorArena depthStencilViewArena;
    private D3D12DescriptorArena staticShaderDescriptorArena;
    private readonly D3D12DescriptorArena cpuShaderDescriptorArena;
    private readonly D3D12DescriptorSlot fieldReservoirCandidateCpuUnorderedAccessDescriptor;
    private readonly D3D12DescriptorSlot fieldReservoirLockCpuUnorderedAccessDescriptor;
    private readonly FrameResources[] frames = new FrameResources[BackBufferCount];
    private readonly ID3D12GraphicsCommandList commandList;
    private readonly ID3D12RootSignature fullscreenRootSignature;
    private readonly ID3D12RootSignature gpuSensorFusionRootSignature;
    private readonly ID3D12RootSignature pointCloudRootSignature;
    private readonly ID3D12RootSignature fractalReservoirRootSignature;
    private readonly ID3D12RootSignature tubeFieldRootSignature;
    private readonly ID3D12RootSignature tubeFieldRenderRootSignature;
    private readonly ID3D12RootSignature bokushoBrushRootSignature;
    private readonly ID3D12CommandSignature generatedMeshDrawCommandSignature;
    private readonly D3D12BlueNoiseTexture blueNoiseTexture;
    private D3D12FieldTexture2D? tubeFieldFallbackRampTexture;
    private ID3D12PipelineState? heightFieldBasePipelineState;
    private ID3D12PipelineState? heightFieldBrushPipelineState;
    private ID3D12PipelineState? scenePipelineState;
    private ID3D12PipelineState? temporalGaussianPipelineState;
    private ID3D12PipelineState? splinePipelineState;
    private ID3D12PipelineState? gpuSensorFusionPipelineState;
    private ID3D12PipelineState? stereoDepthPipelineState;
    private ID3D12PipelineState? pointCloudComputePipelineState;
    private ID3D12PipelineState? pointCloudRenderPipelineState;
    private ID3D12PipelineState? fractalSurfaceSplatRenderPipelineState;
    private ID3D12PipelineState? fractalTransparentSplatRenderPipelineState;
    private ID3D12PipelineState? fractalSplatPipelineState;
    private ID3D12PipelineState? fractalSdfReservoirPipelineState;
    private ID3D12PipelineState? fractalPbrReservoirPipelineState;
    private ID3D12PipelineState? fractalRadiosityReservoirPipelineState;
    private ID3D12PipelineState? tubeFieldComputePipelineState;
    private ID3D12PipelineState? tubeFieldRenderPipelineState;
    private ID3D12PipelineState? bokushoBrushPipelineState;
    private ID3D12PipelineState? fieldReservoirResolvePipelineState;
    private ID3D12PipelineState?[] sdfProxyPipelineStates = [];
    private ID3D12PipelineState? bloomPrefilterPipelineState;
    private ID3D12PipelineState? bloomDownsamplePipelineState;
    private ID3D12PipelineState? bloomBlurHorizontalPipelineState;
    private ID3D12PipelineState? bloomBlurVerticalPipelineState;
    private ID3D12PipelineState? reservoirHistoryUpdatePipelineState;
    private ID3D12PipelineState? resolvePipelineState;
    private readonly ID3D12Fence fence;
    private readonly AutoResetEvent fenceEvent = new(false);
    private DebugUi debugUi;
    private IReadOnlyList<DebugUi> clientUiPanels = [];
    private IReadOnlyList<AquariumUiSurface> clientUiSurfaces = [];
    private AquariumUiDocument? currentClientUi;
    private AquariumUiPreviewDrag? activePreviewDrag;
    private bool clientUiSurfaceWantsMouse;
    private int activeDebugTab;
    private string[] debugTabTitles = ["Aquarium", "Terminal", "Synth"];
    private string terminalInput = "help";
    private readonly List<string> terminalLines = ["Aquarium terminal ready. Type help."];
    private IReadOnlyList<AquariumConsoleCommand> clientCommands = [];
    private string synthPlaygroundScript = SynthPresets[0].Script;
    private int synthPlaygroundPreset;
    private int synthPlaygroundPlayRevision;
    private float synthPlaygroundGain = 0.45f;
    private AquariumSynthPatchStatus synthPlaygroundStatus = new("aquarium-playground", AquariumSynthPatchCompileState.Idle, "idle", 0, 0.0);
    private ID3D11Resource[] overlayWrappedBackBuffers = [];
    private DirectWriteOverlay[] overlays = [];
    private D3D12RenderTarget heightFieldRenderTarget;
    private D3D12RenderTarget sceneRenderTarget;
    private D3D12RenderTarget sceneMetadataRenderTarget;
    private D3D12RenderTarget sceneControlRenderTarget;
    private D3D12RenderTarget sceneReservoirGuideRenderTarget;
    private D3D12RenderTarget sceneCandidateRenderTarget;
    private D3D12RenderTarget sceneCandidateMetadataRenderTarget;
    private D3D12RenderTarget sceneCandidateControlRenderTarget;
    private D3D12RenderTarget sceneCandidateReservoirGuideRenderTarget;
    private D3D12RenderTarget sceneOverdrawRenderTarget;
    private D3D12RenderTarget reservoirResolvedRenderTarget;
    private readonly Dictionary<string, D3D12RenderTarget> graphRenderTargets = new(StringComparer.Ordinal);
    private D3D12TrackedResource sceneDepthTarget;
    private D3D12DescriptorSlot sceneDepthStencilView;
    private readonly D3D12StructuredBuffer[] fieldReservoirHistoryBuffers = new D3D12StructuredBuffer[2];
    private readonly D3D12RenderTarget[] bloomRenderTargets = new D3D12RenderTarget[BloomLevelCount];
    private readonly D3D12RenderTarget[] bloomScratchTargets = new D3D12RenderTarget[BloomLevelCount];
    private readonly Dictionary<string, SharedTextureLeaseSlot> sharedTextureLeases = new(StringComparer.Ordinal);
    private readonly object sharedTextureUploadLock = new();
    private readonly D3D12StructuredBuffer sdfLightBuffer;
    private readonly D3D12StructuredBuffer sdfObjectBuffer;
    private readonly D3D12StructuredBuffer gpuSensorCameraBuffer;
    private readonly D3D12StructuredBuffer acousticConstraintBuffer;
    private readonly D3D12StructuredBuffer gpuFusionSeedBuffer;
    private readonly D3D12StructuredBuffer gpuFusionPointBuffer;
    private readonly D3D12StructuredBuffer temporalGaussianBuffer;
    private D3D12StructuredBuffer? fractalSplatBuffer;
    private D3D12StructuredBuffer? fractalSdfReservoirBuffer;
    private D3D12StructuredBuffer? fractalPbrReservoirBuffer;
    private D3D12StructuredBuffer? fractalRadiosityReservoirBuffer;
    private D3D12StructuredBuffer? fractalFlameStateBuffer;
    private D3D12StructuredBuffer? fractalProgramTransformBuffer;
    private D3D12StructuredBuffer? bufferFieldTextureSplineProgramBuffer;
    private D3D12StructuredBuffer? bufferFieldTextureSampleBuffer;
    private readonly D3D12StructuredBuffer tubeFieldVertexBuffer;
    private readonly D3D12StructuredBuffer tubeFieldIndexBuffer;
    private readonly D3D12StructuredBuffer tubeFieldSegmentBuffer;
    private readonly D3D12StructuredBuffer tubeFieldStatsBuffer;
    private readonly D3D12StructuredBuffer tubeFieldDrawArgumentBuffer;
    private readonly D3D12StructuredBuffer tubeFieldReplayManifestBuffer;
    private readonly D3D12StructuredBuffer bokushoBrushTraceBuffer;
    private readonly D3D12StructuredBuffer bokushoBrushCanvasBuffer;
    private readonly D3D12StructuredBuffer bokushoBrushStrokeBuffer;
    private D3D12StructuredBuffer fieldReservoirCandidateBuffer = null!;
    private D3D12StructuredBuffer fieldReservoirLockBuffer = null!;
    private readonly List<D3D12TubeFieldDrawBatch> tubeFieldDrawBatches = [];
    private readonly Dictionary<string, int> previousTubeFieldRollingOffsets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, D3D12ExternalSensorTexture> externalSensorTextures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExternalProducerFenceSlot> externalProducerFences = new(StringComparer.Ordinal);
    private readonly List<D3D12TrackedResource> programOutputTextures = [];
    private readonly List<IntPtr> programOutputSharedHandles = [];
    private readonly List<ulong> programOutputTextureFenceValues = [];
    private ID3D12Fence? programOutputFence;
    private ID3D12Fence? programOutputConsumerFence;
    private IntPtr programOutputFenceSharedHandle;
    private ulong programOutputFenceValue;
    private ulong pendingProgramOutputFenceValue;
    private long nextProgramOutputConsumerFenceRetryTimestamp;
    private readonly bool programOutputEnabled;
    private readonly string programOutputSharedName;
    private readonly string programOutputFenceName;
    private readonly string programOutputConsumerFenceName;
    private readonly int programOutputRingCount;
    private readonly D3D12CubeTexture studioPmremTexture;
    private readonly D3D12CubeTexture studioIrradianceTexture;
    private readonly AquariumSdfLight[] sdfLights = new AquariumSdfLight[MaxSdfLightCount];
    private readonly AquariumSdfObject[] sdfObjects = new AquariumSdfObject[MaxSdfObjectCount];
    private readonly D3D12GpuSensorCameraPacket[] gpuSensorCameras = new D3D12GpuSensorCameraPacket[MaxGpuSensorCameraCount];
    private readonly string[] gpuSensorFieldTextureKeys = new string[MaxGpuSensorTextureCount];
    private readonly D3D12AcousticConstraintPacket[] acousticConstraints = new D3D12AcousticConstraintPacket[MaxAcousticConstraintCount];
    private readonly D3D12GpuFusionSeedPacket[] gpuFusionSeeds = new D3D12GpuFusionSeedPacket[MaxTemporalGaussianCount];
    private readonly D3D12TemporalGaussianPacket[] temporalGaussians = new D3D12TemporalGaussianPacket[MaxTemporalGaussianCount];
    private AquariumGpuFusionPointBuffer gpuFusionPointSource;
    private int temporalGaussianCount;
    private int gpuSensorCameraCount;
    private int gpuSensorTextureCount;
    private int gpuSensorExternalTextureCount;
    private int gpuSensorFieldTextureCount;
    private int gpuSensorFieldUnsupportedTextureCount;
    private int acousticConstraintCount;
    private int gpuFusionSeedCount;
    private int gpuFusionPointCount;
    private int visibleFractalSplatCount;
    private AquariumPackedFractalIfsTransform[] activeFractalProgramTransforms = [];
    private bool temporalGaussiansGpuGenerated;
    private AquariumFractalReservoirField activeFractalReservoirField = AquariumFractalReservoirField.Empty;
    private AquariumBufferFieldFrame activeBufferFieldFrame = AquariumBufferFieldFrame.Empty;
    private AquariumBokushoBrushFrame activeBokushoBrushFrame = AquariumBokushoBrushFrame.Empty;
    private AquariumPackedTextureSplineFieldProgram[] activeTextureSplinePrograms = [];
    private float[] activeTextureFieldSamples = [];
    private AquariumSplineFrame activeSplineFrame = AquariumSplineFrame.Empty;
    private AquariumFieldEvidenceFrame activeFieldEvidenceFrame = AquariumFieldEvidenceFrame.Empty;
    private AquariumFieldEvidenceValidationReport activeFieldEvidenceValidation = AquariumFieldEvidenceValidationReport.Empty;
    private AquariumFieldLoweringPlan activeFieldLoweringPlan = AquariumFieldLoweringPlan.Empty;
    private D3D12FieldResourceStats activeFieldResourceStats = D3D12FieldResourceStats.Empty;
    private int activeTubeFieldDrawIndexCount;
    private int activeTubeFieldDispatchedSegments;
    private int activeTubeFieldRequestedSegments;
    private int activeTubeFieldTruncatedSegments;
    private int activeTubeFieldIndirectDrawCount;
    private int activeTubeFieldSkippedDrawBatches;
    private int activeTubeFieldUnplannedLowerings;
    private int activeTubeFieldInvalidColumns;
    private int activeStereoDepthDispatchReadyLowerings;
    private int activeStereoDepthDispatchedLowerings;
    private int activeStereoDepthUnplannedLowerings;
    private int activeStereoDepthUnresolvedLowerings;
    private int activePointCloudGeneratedMeshes;
    private int activePointCloudRenderedMeshes;
    private int activePointCloudUnresolvedMeshes;
    private int activePointCloudUnplannedMeshes;
    private bool activePointCloudSurfaceReservoir;
    private int activeFieldResourceUploadCount;
    private int activeFieldResourceUploadSkippedCount;
    private Viewport viewport;
    private Viewport reservoirViewport;
    private RawRect scissorRect;
    private RawRect reservoirScissorRect;
    private int width;
    private int height;
    private int reservoirWidth;
    private int reservoirHeight;
    private ulong fenceValue;
    private int temporalFrameIndex;
    private int frameIndex;
    private Vector3 previousCameraPosition;
    private Vector3 previousCameraTarget;
    private Vector3 activeCameraPosition;
    private Vector3 activeCameraTarget;
    private Vector2 previousViewCenter;
    private Vector2 previousCursorWorld;
    private Vector2 previousJitterPixels;
    private float previousViewRadius = 0.001f;
    private float previousTimeSeconds;
    private D3D12HeightFieldBrushConstants heightFieldBrushConstants;
    private GraphicsSettings settings = GraphicsSettings.Default;
    private double accumulatedFrameCpuMilliseconds;
    private double accumulatedRecordCpuMilliseconds;
    private double accumulatedOverlayCpuMilliseconds;
    private int accumulatedTimingFrames;
    private long lastVisibleFrameTimestamp;
    private double smoothedVisibleFrameMilliseconds;
    private string visibleFrameRateText = "FPS --";
    private readonly string shaderSourceRoot;
    private readonly D3D12ShaderPaths shaderPaths;
    private readonly CompiledRenderGraph renderGraph;
    private readonly bool hasSceneRenderingPass;
    private readonly Stopwatch shaderReloadClock = Stopwatch.StartNew();
    private readonly Dictionary<string, DateTime> shaderWriteTimesUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly double[] accumulatedGpuTimingMilliseconds = new double[(int)D3D12GpuTimingPass.Count];
    private readonly int[] accumulatedGpuTimingPassFrames = new int[(int)D3D12GpuTimingPass.Count];
    private ulong gpuTimestampFrequency;
    private Task<D3D12PipelineSet>? pipelineBuildTask;
    private TimeSpan lastShaderReloadCheck;
    private bool shaderReloadFailureReported;
    private bool pipelineBuildInProgressReported;
    private bool initialPipelineReadyReported;
    private bool hasPresentedReadyFrame;
    private static readonly TimeSpan ShaderReloadPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ShaderReloadWriteSettleTime = TimeSpan.FromMilliseconds(300);

    public D3D12Renderer(
        IntPtr windowHandle,
        int width,
        int height,
        string? shaderPath = null,
        AquariumRenderPlan? renderPlan = null,
        GraphicsSettings? graphicsSettings = null,
        Action<string>? startupProgress = null)
    {
        ApplyGraphicsSettings(graphicsSettings ?? GraphicsSettings.Default);
        this.width = width;
        this.height = height;
        RefreshReservoirWorkGrid();
        programOutputEnabled = string.Equals(
            Environment.GetEnvironmentVariable(ProgramOutputEnabledEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
        programOutputSharedName = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProgramOutputSharedNameEnvironmentVariable))
            ? DefaultProgramOutputSharedName
            : Environment.GetEnvironmentVariable(ProgramOutputSharedNameEnvironmentVariable)!.Trim();
        programOutputFenceName = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProgramOutputFenceNameEnvironmentVariable))
            ? DefaultProgramOutputFenceName
            : Environment.GetEnvironmentVariable(ProgramOutputFenceNameEnvironmentVariable)!.Trim();
        programOutputConsumerFenceName = Environment.GetEnvironmentVariable(ProgramOutputConsumerFenceNameEnvironmentVariable)?.Trim() ?? string.Empty;
        programOutputRingCount = ResolveProgramOutputRingCount();
        var activeRenderPlan = renderPlan ?? new AquariumRenderPlan();
        renderGraph = D3D12RenderGraphCompiler.Compile(activeRenderPlan);
        hasSceneRenderingPass = renderGraph.Passes.Any(static pass =>
            pass.Kind is AquariumPassKind.Fullscreen or AquariumPassKind.Proxy or AquariumPassKind.Instanced or AquariumPassKind.Compute);
        shaderSourceRoot = ResolveShaderSourceRoot(shaderPath, activeRenderPlan.Shaders);
        shaderPaths = D3D12ShaderPaths.FromManifest(shaderSourceRoot, activeRenderPlan.Shaders);
        sdfProxyPipelineStates = new ID3D12PipelineState?[shaderPaths.SdfShaders.Count];
        ReportStartupProgress(startupProgress, "Creating D3D12 device and swapchain");

        factory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(false);
        device = D3D12.D3D12CreateDevice<ID3D12Device>(IntPtr.Zero, FeatureLevel.Level_11_0);
        device.Name = "Aquarium D3D12 Device";
        commandQueue = device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct));
        commandQueue.Name = "Aquarium D3D12 Direct Queue";
        commandQueue.GetTimestampFrequency(out gpuTimestampFrequency);
        CreateOverlayDevice(out overlayDevice, out overlayContext, out overlayOn12Device);

        var swapChainDescription = new SwapChainDescription1
        {
            BufferCount = BackBufferCount,
            Width = (uint)width,
            Height = (uint)height,
            Format = Format.B8G8R8A8_UNorm,
            BufferUsage = Usage.RenderTargetOutput,
            SwapEffect = SwapEffect.FlipDiscard,
            SampleDescription = new SampleDescription(1, 0),
        };

        using var swapChain1 = factory.CreateSwapChainForHwnd(commandQueue, windowHandle, swapChainDescription);
        swapChain = swapChain1.QueryInterface<IDXGISwapChain3>();
        frameIndex = (int)swapChain.CurrentBackBufferIndex;

        renderTargetViewArena = CreateRenderTargetViewArena();
        depthStencilViewArena = CreateDepthStencilViewArena();
        staticShaderDescriptorArena = CreateStaticShaderDescriptorArena();
        cpuShaderDescriptorArena = CreateCpuShaderDescriptorArena();
        fieldReservoirCandidateCpuUnorderedAccessDescriptor = cpuShaderDescriptorArena.Allocate();
        fieldReservoirLockCpuUnorderedAccessDescriptor = cpuShaderDescriptorArena.Allocate();
        for (var index = 0; index < frames.Length; index++)
        {
            frames[index] = CreateFrameResources(index);
        }

        CreateRenderTargetViews();
        CreateProgramOutputTexture();
        CreateBackBufferOverlays();
        debugUi = CreateDebugUi([]);
        heightFieldRenderTarget = CreateHeightFieldRenderTarget();
        sceneRenderTarget = CreateSceneRenderTarget();
        sceneMetadataRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-metadata-target", "Aquarium D3D12 Scene Metadata Target");
        sceneControlRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-control-target", "Aquarium D3D12 Scene Control Target");
        sceneReservoirGuideRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-reservoir-guide-target", "Aquarium D3D12 Scene Reservoir Guide Target");
        sceneCandidateRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-target", "Aquarium D3D12 Scene Candidate Target");
        sceneCandidateMetadataRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-metadata-target", "Aquarium D3D12 Scene Candidate Metadata Target");
        sceneCandidateControlRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-control-target", "Aquarium D3D12 Scene Candidate Control Target");
        sceneCandidateReservoirGuideRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-reservoir-guide-target", "Aquarium D3D12 Scene Candidate Reservoir Guide Target");
        sceneOverdrawRenderTarget = CreateSceneOverdrawRenderTarget();
        reservoirResolvedRenderTarget = CreateReservoirResolvedRenderTarget();
        sceneDepthStencilView = depthStencilViewArena.Allocate();
        sceneDepthTarget = CreateSceneDepthTarget(sceneDepthStencilView);
        CreateBloomRenderTargets();
        CreateGraphRenderTargets();
        sdfLightBuffer = new D3D12StructuredBuffer(device, MaxSdfLightCount, Marshal.SizeOf<AquariumSdfLight>(), "Aquarium D3D12 Sdf Light Buffer");
        sdfObjectBuffer = new D3D12StructuredBuffer(device, MaxSdfObjectCount, Marshal.SizeOf<AquariumSdfObject>(), "Aquarium D3D12 Sdf Object Buffer");
        gpuSensorCameraBuffer = new D3D12StructuredBuffer(device, MaxGpuSensorCameraCount, Marshal.SizeOf<D3D12GpuSensorCameraPacket>(), "Aquarium D3D12 GPU Sensor Camera Buffer");
        acousticConstraintBuffer = new D3D12StructuredBuffer(device, MaxAcousticConstraintCount, Marshal.SizeOf<D3D12AcousticConstraintPacket>(), "Aquarium D3D12 Acoustic Constraint Buffer");
        gpuFusionSeedBuffer = new D3D12StructuredBuffer(device, MaxTemporalGaussianCount, Marshal.SizeOf<D3D12GpuFusionSeedPacket>(), "Aquarium D3D12 GPU Sensor Fusion Seed Buffer");
        gpuFusionPointBuffer = new D3D12StructuredBuffer(device, MaxTemporalGaussianCount, Marshal.SizeOf<D3D12GpuFusionPointPacket>(), "Aquarium D3D12 GPU Sensor Fusion Native Point Buffer");
        temporalGaussianBuffer = new D3D12StructuredBuffer(device, MaxTemporalGaussianCount, Marshal.SizeOf<D3D12TemporalGaussianPacket>(), "Aquarium D3D12 Temporal Gaussian Buffer", allowUnorderedAccess: true);
        tubeFieldVertexBuffer = new D3D12StructuredBuffer(device, MaxTubeFieldVertices, Marshal.SizeOf<D3D12TubeFieldVertex>(), "Aquarium D3D12 TubeField Vertex Buffer", allowUnorderedAccess: true);
        tubeFieldIndexBuffer = new D3D12StructuredBuffer(device, MaxTubeFieldIndices, Marshal.SizeOf<uint>(), "Aquarium D3D12 TubeField Index Buffer", allowUnorderedAccess: true);
        tubeFieldSegmentBuffer = new D3D12StructuredBuffer(device, MaxTubeFieldSegments, Marshal.SizeOf<D3D12TubeFieldSegment>(), "Aquarium D3D12 TubeField Segment Buffer", allowUnorderedAccess: true);
        tubeFieldStatsBuffer = new D3D12StructuredBuffer(device, 4, Marshal.SizeOf<uint>(), "Aquarium D3D12 TubeField Stats Buffer", allowUnorderedAccess: true);
        tubeFieldDrawArgumentBuffer = new D3D12StructuredBuffer(device, MaxTubeFieldDrawBatches * GeneratedMeshDrawArgumentUIntCount, Marshal.SizeOf<uint>(), "Aquarium D3D12 TubeField Indirect Draw Arguments", allowUnorderedAccess: true);
        tubeFieldReplayManifestBuffer = new D3D12StructuredBuffer(device, MaxTubeFieldReplaySources, Marshal.SizeOf<D3D12TubeFieldReplayManifestEntry>(), "Aquarium D3D12 TubeField Replay Manifest");
        bokushoBrushTraceBuffer = new D3D12StructuredBuffer(device, MaxBokushoBrushValues, sizeof(float), "Aquarium D3D12 Bokusho Brush Trace Field", allowUnorderedAccess: true);
        bokushoBrushCanvasBuffer = new D3D12StructuredBuffer(device, MaxBokushoBrushValues, sizeof(float), "Aquarium D3D12 Bokusho Brush Canvas Field", allowUnorderedAccess: true);
        bokushoBrushStrokeBuffer = new D3D12StructuredBuffer(device, MaxBokushoBrushStrokes, Marshal.SizeOf<D3D12BokushoBrushStrokePacket>(), "Aquarium D3D12 Bokusho Brush Stroke Packet Buffer");
        CreateFieldReservoirBuffers();
        resourceRegistry.Add("sdf-light-buffer", sdfLightBuffer);
        resourceRegistry.Add("sdf-object-buffer", sdfObjectBuffer);
        resourceRegistry.Add("gpu-sensor-camera-buffer", gpuSensorCameraBuffer);
        resourceRegistry.Add("acoustic-constraint-buffer", acousticConstraintBuffer);
        resourceRegistry.Add("gpu-fusion-seed-buffer", gpuFusionSeedBuffer);
        resourceRegistry.Add("gpu-fusion-point-buffer", gpuFusionPointBuffer);
        resourceRegistry.Add("temporal-gaussian-buffer", temporalGaussianBuffer);
        resourceRegistry.Add("tube-field-vertex-buffer", tubeFieldVertexBuffer);
        resourceRegistry.Add("tube-field-index-buffer", tubeFieldIndexBuffer);
        resourceRegistry.Add("tube-field-segment-buffer", tubeFieldSegmentBuffer);
        resourceRegistry.Add("tube-field-stats-buffer", tubeFieldStatsBuffer);
        resourceRegistry.Add("tube-field-indirect-draw-arguments", tubeFieldDrawArgumentBuffer);
        resourceRegistry.Add("tube-field-replay-manifest", tubeFieldReplayManifestBuffer);
        resourceRegistry.Add("bokusho-brush-trace-buffer", bokushoBrushTraceBuffer);
        resourceRegistry.Add("bokusho-brush-canvas-buffer", bokushoBrushCanvasBuffer);
        resourceRegistry.Add("bokusho-brush-stroke-buffer", bokushoBrushStrokeBuffer);
        commandList = device.CreateCommandList<ID3D12GraphicsCommandList>(0, CommandListType.Direct, frames[frameIndex].CommandAllocator, null);
        commandList.Name = "Aquarium D3D12 Graphics Command List";
        commandList.Close();
        fence = device.CreateFence(0);
        fence.Name = "Aquarium D3D12 Frame Fence";
        if (programOutputEnabled)
        {
            programOutputFence = device.CreateFence(0, FenceFlags.Shared);
            programOutputFence.Name = "Aquarium D3D12 Program Output Fence";
        }

        CreateProgramOutputFenceHandle();
        commandQueue.ExecuteCommandList(commandList);
        WaitForGpu();
        ReportStartupProgress(startupProgress, "Loading studio IBL cubemaps");
        studioPmremTexture = LoadStudioPmremTexture();
        resourceRegistry.Add("studio-pmrem-cubemap", studioPmremTexture);
        studioIrradianceTexture = LoadStudioIrradianceTexture();
        resourceRegistry.Add("studio-irradiance-cubemap", studioIrradianceTexture);
        ReportStartupProgress(startupProgress, "Creating renderer blue-noise tile");
        blueNoiseTexture = CreateBlueNoiseTexture();
        resourceRegistry.Add("blue-noise-threshold-tile", blueNoiseTexture);
        ReportStartupProgress(startupProgress, "Creating D3D12 render pipelines");
        fullscreenRootSignature = CreateFullscreenRootSignature();
        fullscreenRootSignature.Name = "Aquarium D3D12 Fullscreen Root Signature";
        gpuSensorFusionRootSignature = CreateGpuSensorFusionRootSignature();
        gpuSensorFusionRootSignature.Name = "Aquarium D3D12 GPU Sensor Fusion Root Signature";
        pointCloudRootSignature = CreatePointCloudRootSignature();
        pointCloudRootSignature.Name = "Aquarium D3D12 Point Cloud Root Signature";
        fractalReservoirRootSignature = CreateFractalReservoirRootSignature();
        fractalReservoirRootSignature.Name = "Aquarium D3D12 Fractal Reservoir Root Signature";
        tubeFieldRootSignature = CreateTubeFieldRootSignature();
        tubeFieldRootSignature.Name = "Aquarium D3D12 TubeField Root Signature";
        tubeFieldRenderRootSignature = CreateTubeFieldRenderRootSignature();
        tubeFieldRenderRootSignature.Name = "Aquarium D3D12 TubeField Render Root Signature";
        bokushoBrushRootSignature = CreateBokushoBrushRootSignature();
        bokushoBrushRootSignature.Name = "Aquarium D3D12 Bokusho Brush Root Signature";
        generatedMeshDrawCommandSignature = CreateGeneratedMeshDrawCommandSignature();
        generatedMeshDrawCommandSignature.Name = "Aquarium D3D12 Generated Mesh Draw Command Signature";
        CaptureShaderWriteTimes();
        StartPipelineBuild("initial");
        viewport = new Viewport(0.0f, 0.0f, width, height);
        scissorRect = new RawRect(0, 0, width, height);
        RefreshReservoirWorkGrid();
        Console.WriteLine($"D3D12 resource registry: {resourceRegistry.Describe()}");
        Console.WriteLine($"Aquarium render graph declared: {renderGraph.Describe()}");
        Console.WriteLine("D3D12 device and swapchain created.");
    }

    public int RenderDebugMode
    {
        get => settings.RenderDebugMode;
        set => settings = (settings with { RenderDebugMode = value }).Normalized();
    }

    public bool DebugUiVisible
    {
        get => debugUi.IsVisible;
        set => debugUi.IsVisible = value;
    }

    public bool HasPresentedReadyFrame => hasPresentedReadyFrame;

    public bool CapturesInput => debugUi.WantsKeyboard || debugUi.WantsMouse || clientUiSurfaceWantsMouse || clientUiPanels.Any(panel => panel.WantsKeyboard || panel.WantsMouse);

    public AquariumSynthDocument DebugSynth => new AquariumSynthDocument
    {
        MasterGain = 1.0f,
        Enabled = true
    }.Patch(
        "aquarium-playground",
        synthPlaygroundScript,
        AquariumSynthTrigger.Manual(synthPlaygroundPlayRevision),
        synthPlaygroundGain,
        0,
        "aquarium_playground",
        status => synthPlaygroundStatus = status);

    public void UpdateUi(InputState input, AquariumUiDocument clientUi)
    {
        if (!ReferenceEquals(currentClientUi, clientUi))
        {
            currentClientUi = clientUi;
            clientCommands = clientUi.Commands;
            clientUiSurfaces = clientUi.Surfaces;
            var debugPanels = clientUi.Panels.Where(panel => !panel.FadeWhenMouseDistant).ToArray();
            var floatingPanels = clientUi.Panels.Where(panel => panel.FadeWhenMouseDistant).ToArray();
            debugTabTitles = ["Aquarium", "Terminal", "Synth", .. debugPanels.Select(panel => panel.Title)];
            activeDebugTab = Math.Clamp(activeDebugTab, 0, debugTabTitles.Length - 1);
            var debugUiVisible = debugUi.IsVisible;
            debugUi = CreateDebugUi(debugPanels);
            debugUi.IsVisible = debugUiVisible;
            clientUiPanels = floatingPanels
                .Select(panel => DebugUi.FromContract(panel with { FadeWhenMouseDistant = false }))
                .ToArray();
        }

        debugUi.Update(input);
        foreach (var panel in clientUiPanels)
        {
            panel.Update(input);
        }

        UpdateSurfaceInteractions(input);
    }

    private void UpdateSurfaceInteractions(InputState input)
    {
        clientUiSurfaceWantsMouse = false;
        if (activePreviewDrag is { } active)
        {
            clientUiSurfaceWantsMouse = true;
            if (!input.LeftMouseDown)
            {
                active.Element.HandlePreviewInteraction?.Invoke(new AquariumUiPreviewInteraction(
                    active.ItemId,
                    active.Handle,
                    "end",
                    Math.Clamp((input.MousePosition.X - active.Canvas.Left) / Math.Max(1.0f, active.Canvas.Width), 0.0f, 1.0f),
                    Math.Clamp((input.MousePosition.Y - active.Canvas.Top) / Math.Max(1.0f, active.Canvas.Height), 0.0f, 1.0f),
                    0.0f,
                    0.0f));
                activePreviewDrag = null;
                return;
            }

            var x = Math.Clamp((input.MousePosition.X - active.Canvas.Left) / Math.Max(1.0f, active.Canvas.Width), 0.0f, 1.0f);
            var y = Math.Clamp((input.MousePosition.Y - active.Canvas.Top) / Math.Max(1.0f, active.Canvas.Height), 0.0f, 1.0f);
            active.Element.HandlePreviewInteraction?.Invoke(new AquariumUiPreviewInteraction(
                active.ItemId,
                active.Handle,
                "drag",
                x,
                y,
                input.MouseDelta.X / Math.Max(1.0f, active.Canvas.Width),
                input.MouseDelta.Y / Math.Max(1.0f, active.Canvas.Height)));
            return;
        }

        if (TryHitPreview(input.MousePosition, out var hit))
        {
            clientUiSurfaceWantsMouse = true;
            hit.Element.HandlePreviewInteraction?.Invoke(new AquariumUiPreviewInteraction(
                hit.ItemId,
                hit.Handle,
                "hover",
                hit.X,
                hit.Y,
                0.0f,
                0.0f));
            if (input.IsMousePressed(MouseButton.Left))
            {
                activePreviewDrag = new AquariumUiPreviewDrag(hit.Element, hit.ItemId, hit.Handle, hit.Canvas);
                hit.Element.HandlePreviewInteraction?.Invoke(new AquariumUiPreviewInteraction(
                    hit.ItemId,
                    hit.Handle,
                    "begin",
                    hit.X,
                    hit.Y,
                    0.0f,
                    0.0f));
            }
        }
    }

    private bool TryHitPreview(Vector2 point, out AquariumUiPreviewHit hit)
    {
        for (var surfaceIndex = clientUiSurfaces.Count - 1; surfaceIndex >= 0; surfaceIndex--)
        {
            var surface = clientUiSurfaces[surfaceIndex];
            var surfaceBounds = SurfaceBounds(surface);
            var hasTitle = !string.IsNullOrWhiteSpace(surface.Title);
            var contentTop = hasTitle ? surfaceBounds.Top + 42.0f : surfaceBounds.Top + surface.ContentPadding;
            var content = RectFromEdges(surfaceBounds.Left + surface.ContentPadding, contentTop, surfaceBounds.Right - surface.ContentPadding, surfaceBounds.Bottom - surface.ContentPadding);
            if (TryHitPreviewChildren(surface.Root.Children ?? [], content, surface.Root.Layout ?? AquariumUiLayout.Vertical(), point, out hit))
            {
                return true;
            }
        }

        hit = default;
        return false;
    }

    private static bool TryHitPreviewChildren(IReadOnlyList<AquariumUiElement> elements, Rect bounds, AquariumUiLayout layout, Vector2 point, out AquariumUiPreviewHit hit)
    {
        var visible = elements.Where(static element => element.Visible).ToArray();
        if (visible.Length == 0)
        {
            hit = default;
            return false;
        }

        var content = RectFromEdges(
            bounds.Left + layout.Padding,
            bounds.Top + layout.Padding,
            bounds.Right - layout.Padding,
            bounds.Bottom - layout.Padding);
        var totalGap = layout.Gap * Math.Max(0, visible.Length - 1);
        var horizontal = string.Equals(layout.Direction, "horizontal", StringComparison.Ordinal);
        var cursor = horizontal ? content.Left : content.Top;
        var available = Math.Max(0.0f, (horizontal ? content.Width : content.Height) - totalGap);
        var preferred = visible.Select(element => PreferredUiExtent(element, horizontal)).ToArray();
        var preferredTotal = preferred.Sum(static value => value ?? 0.0f);
        var preferredScale = preferredTotal > available && preferredTotal > 0.0f ? available / preferredTotal : 1.0f;
        var flexibleWeight = Math.Max(0.001f, visible.Where((_, index) => preferred[index] is null).Sum(static element => Math.Max(0.001f, element.Weight)));
        var flexibleAvailable = Math.Max(0.0f, available - preferredTotal * preferredScale);
        for (var index = 0; index < visible.Length; index++)
        {
            var element = visible[index];
            var extent = preferred[index] is { } fixedExtent
                ? fixedExtent * preferredScale
                : flexibleAvailable * Math.Max(0.001f, element.Weight) / flexibleWeight;
            var childBounds = horizontal
                ? RectFromEdges(cursor, content.Top, Math.Min(content.Right, cursor + extent), content.Bottom)
                : RectFromEdges(content.Left, cursor, content.Right, Math.Min(content.Bottom, cursor + extent));
            cursor += extent + layout.Gap;
            if (!ContainsRect(childBounds, point))
            {
                continue;
            }

            if (TryHitPreviewElement(element, childBounds, point, out hit))
            {
                return true;
            }
        }

        hit = default;
        return false;
    }

    private static bool TryHitPreviewElement(AquariumUiElement element, Rect bounds, Vector2 point, out AquariumUiPreviewHit hit)
    {
        switch (element.Kind)
        {
            case "group":
                return TryHitPreviewChildren(element.Children ?? [], bounds, element.Layout ?? AquariumUiLayout.Vertical(), point, out hit);
            case "pane":
                var paneContent = !string.IsNullOrWhiteSpace(element.Text)
                    ? RectFromEdges(bounds.Left + 6.0f, bounds.Top + 32.0f, bounds.Right - 6.0f, bounds.Bottom - 6.0f)
                    : bounds;
                return TryHitPreviewChildren(element.Children ?? [], paneContent, element.Layout ?? AquariumUiLayout.Vertical(), point, out hit);
            case "card":
                return TryHitPreviewChildren(element.Children ?? [], bounds, element.Layout ?? AquariumUiLayout.Vertical(4.0f, 8.0f), point, out hit);
            case "preview":
                return TryHitPreviewElementCanvas(element, bounds, point, out hit);
            default:
                hit = default;
                return false;
        }
    }

    private static bool TryHitPreviewElementCanvas(AquariumUiElement element, Rect bounds, Vector2 point, out AquariumUiPreviewHit hit)
    {
        hit = default;
        if (element.HandlePreviewInteraction is null || !TryPreviewCanvas(bounds, !string.IsNullOrWhiteSpace(element.Text), out var canvas) || !ContainsRect(canvas, point))
        {
            return false;
        }

        var items = element.ReadPreviewItems?.Invoke() ?? [];
        for (var index = items.Count - 1; index >= 0; index--)
        {
            var item = items[index];
            var itemBounds = RectFromEdges(
                canvas.Left + Math.Clamp(item.X, 0.0f, 1.0f) * canvas.Width,
                canvas.Top + Math.Clamp(item.Y, 0.0f, 1.0f) * canvas.Height,
                canvas.Left + Math.Clamp(item.X + item.Width, 0.0f, 1.0f) * canvas.Width,
                canvas.Top + Math.Clamp(item.Y + item.Height, 0.0f, 1.0f) * canvas.Height);
            if (!ContainsRect(itemBounds, point))
            {
                continue;
            }

            var handle = HitPreviewHandle(itemBounds, point);
            hit = new AquariumUiPreviewHit(
                element,
                item.Id,
                handle,
                canvas,
                Math.Clamp((point.X - canvas.Left) / Math.Max(1.0f, canvas.Width), 0.0f, 1.0f),
                Math.Clamp((point.Y - canvas.Top) / Math.Max(1.0f, canvas.Height), 0.0f, 1.0f));
            return true;
        }

        hit = new AquariumUiPreviewHit(
            element,
            "",
            "canvas",
            canvas,
            Math.Clamp((point.X - canvas.Left) / Math.Max(1.0f, canvas.Width), 0.0f, 1.0f),
            Math.Clamp((point.Y - canvas.Top) / Math.Max(1.0f, canvas.Height), 0.0f, 1.0f));
        return true;
    }

    private static bool TryPreviewCanvas(Rect bounds, bool hasLabel, out Rect canvas)
    {
        var padded = hasLabel
            ? RectFromEdges(bounds.Left + 10.0f, bounds.Top + 30.0f, bounds.Right - 10.0f, bounds.Bottom - 10.0f)
            : RectFromEdges(bounds.Left + 2.0f, bounds.Top + 2.0f, bounds.Right - 2.0f, bounds.Bottom - 2.0f);
        if (padded.Width <= 1.0f || padded.Height <= 1.0f)
        {
            canvas = default;
            return false;
        }

        const float targetAspect = 16.0f / 9.0f;
        var canvasWidth = padded.Width;
        var canvasHeight = canvasWidth / targetAspect;
        if (canvasHeight > padded.Height)
        {
            canvasHeight = padded.Height;
            canvasWidth = canvasHeight * targetAspect;
        }

        canvas = RectFromEdges(
            padded.Left + (padded.Width - canvasWidth) * 0.5f,
            padded.Top + (padded.Height - canvasHeight) * 0.5f,
            padded.Left + (padded.Width + canvasWidth) * 0.5f,
            padded.Top + (padded.Height + canvasHeight) * 0.5f);
        return true;
    }

    private Rect SurfaceBounds(AquariumUiSurface surface)
    {
        var edgePadding = surface.ContentPadding <= 0.0f ? 0.0f : 8.0f;
        return RectFromEdges(
            Math.Clamp(surface.Bounds.Left, edgePadding, Math.Max(edgePadding, width - 80.0f)),
            Math.Clamp(surface.Bounds.Top, edgePadding, Math.Max(edgePadding, height - 48.0f)),
            Math.Clamp(surface.Bounds.Left + surface.Bounds.Width, 80.0f + edgePadding, width - edgePadding),
            Math.Clamp(surface.Bounds.Top + surface.Bounds.Height, 48.0f + edgePadding, height - edgePadding));
    }

    private static string HitPreviewHandle(Rect itemBounds, Vector2 point)
    {
        const float radius = 10.0f;
        if (DistanceSquared(point, itemBounds.Left, itemBounds.Top) <= radius * radius)
        {
            return "nw";
        }

        if (DistanceSquared(point, itemBounds.Right, itemBounds.Top) <= radius * radius)
        {
            return "ne";
        }

        if (DistanceSquared(point, itemBounds.Left, itemBounds.Bottom) <= radius * radius)
        {
            return "sw";
        }

        if (DistanceSquared(point, itemBounds.Right, itemBounds.Bottom) <= radius * radius)
        {
            return "se";
        }

        return "move";
    }

    private static float? PreferredUiExtent(AquariumUiElement element, bool horizontal)
    {
        if (element.FixedExtent is { } fixedExtent)
        {
            return Math.Max(0.0f, fixedExtent);
        }

        if (horizontal)
        {
            return null;
        }

        var weight = Math.Max(0.001f, element.Weight);
        return element.Kind switch
        {
            "group" or "pane" or "card" when HasFlexibleUiChild(element) => null,
            "group" or "pane" or "card" => PreferredUiContainerExtent(element) * weight,
            "text" when element.Role is "mono" => 18.0f * weight,
            "text" when element.Role is "strong" or "title" => 22.0f * weight,
            "text" => 18.0f * weight,
            "toggle" or "select" or "slider" => 24.0f * weight,
            "button" => 30.0f * weight,
            "metric" => 36.0f * weight,
            _ => null,
        };
    }

    private static bool HasFlexibleUiChild(AquariumUiElement element)
    {
        var children = element.Children?.Where(static child => child.Visible).ToArray() ?? [];
        return children.Any(static child => PreferredUiExtent(child, horizontal: false) is null);
    }

    private static float PreferredUiContainerExtent(AquariumUiElement element)
    {
        var layout = element.Layout ?? AquariumUiLayout.Vertical();
        var children = element.Children?.Where(static child => child.Visible).ToArray() ?? [];
        if (children.Length == 0)
        {
            return element.Kind == "pane" ? 72.0f : 24.0f;
        }

        var horizontal = string.Equals(layout.Direction, "horizontal", StringComparison.Ordinal);
        var childExtents = children.Select(static child => PreferredUiExtent(child, horizontal: false) ?? 48.0f * Math.Max(0.001f, child.Weight)).ToArray();
        var contentExtent = horizontal
            ? childExtents.Max()
            : childExtents.Sum() + layout.Gap * Math.Max(0, childExtents.Length - 1);
        contentExtent += layout.Padding * 2.0f;

        return element.Kind switch
        {
            "pane" when !string.IsNullOrWhiteSpace(element.Text) => contentExtent + 38.0f,
            "pane" => contentExtent,
            "card" => contentExtent,
            _ => contentExtent,
        };
    }

    private static bool ContainsRect(Rect rect, Vector2 point) =>
        point.X >= rect.Left && point.X <= rect.Right && point.Y >= rect.Top && point.Y <= rect.Bottom;

    private static float DistanceSquared(Vector2 point, float x, float y)
    {
        var dx = point.X - x;
        var dy = point.Y - y;
        return dx * dx + dy * dy;
    }

    private static Rect RectFromEdges(float left, float top, float right, float bottom) =>
        new(left, top, Math.Max(0.0f, right - left), Math.Max(0.0f, bottom - top));

    private readonly record struct AquariumUiPreviewHit(AquariumUiElement Element, string ItemId, string Handle, Rect Canvas, float X, float Y);

    private readonly record struct AquariumUiPreviewDrag(AquariumUiElement Element, string ItemId, string Handle, Rect Canvas);

    public void CycleRenderDebugMode()
    {
        RenderDebugMode = (RenderDebugMode + 1) % (GraphicsSettings.MaxRenderDebugMode + 1);
    }

    public GraphicsSettings CaptureGraphicsSettings()
    {
        return settings.Normalized();
    }

    public void ApplyGraphicsSettings(GraphicsSettings graphicsSettings)
    {
        settings = graphicsSettings.Normalized();
    }

    private DebugUi CreateDebugUi(IReadOnlyList<AquariumUiPanel> clientDebugPanels)
    {
        var ui = new DebugUi("Debug", 18.0f, 18.0f, 520.0f, debugTabTitles, () => activeDebugTab, SelectDebugTab)
            .Panel(panel =>
            {
                panel
                .Section("View", () => activeDebugTab == 0)
                .Options("Render Debug", () => RenderDebugMode, value => RenderDebugMode = Math.Clamp(value, GraphicsSettings.MinRenderDebugMode, GraphicsSettings.MaxRenderDebugMode), RenderDebugOptions, "Selects the active renderer debug view.", () => activeDebugTab == 0)
                .Button("Reset View", () => RenderDebugMode = 0, "Returns to the final presented frame.", () => activeDebugTab == 0)
                .Options("Reservoir Mode", () => settings.FieldReservoirMode, value => settings = (settings with { FieldReservoirMode = Math.Clamp(value, GraphicsSettings.FieldReservoirModeNativeDomain, GraphicsSettings.FieldReservoirModeTexelBaseline) }).Normalized(), FieldReservoirModeOptions, "Selects native-domain validation or texel-owned baseline reuse.", () => activeDebugTab == 0)
                .Slider("Reservoir Scale", () => settings.FieldReservoirScale, value => settings = (settings with { FieldReservoirScale = Math.Clamp(value, GraphicsSettings.MinFieldReservoirScale, GraphicsSettings.MaxFieldReservoirScale) }).Normalized(), GraphicsSettings.MinFieldReservoirScale, GraphicsSettings.MaxFieldReservoirScale, "0.##", "Controls the internal reservoir work grid; presentation remains full resolution.", () => activeDebugTab == 0)
                .Slider("Spatial Reuse Budget", () => settings.FieldReservoirSpatialReuseBudget, value => settings = (settings with { FieldReservoirSpatialReuseBudget = Math.Clamp(value, GraphicsSettings.MinFieldReservoirSpatialReuseBudget, GraphicsSettings.MaxFieldReservoirSpatialReuseBudget) }).Normalized(), GraphicsSettings.MinFieldReservoirSpatialReuseBudget, GraphicsSettings.MaxFieldReservoirSpatialReuseBudget, "0.##", "Controls what share of reservoir work-grid pixels spend neighbor sampling each frame.", () => activeDebugTab == 0)
                .Readout("Reservoir Grid", () => $"{reservoirWidth}x{reservoirHeight} / present {width}x{height}", "Internal reservoir work grid and final present size.", () => activeDebugTab == 0)
                .Readout("Reservoir Budget", () => $"{settings.FieldReservoirSpatialReuseBudget:P0} spatial reuse / {settings.FieldReservoirScale:P0} work grid", "Current reservoir sampling budget and work-grid scale.", () => activeDebugTab == 0)
                .Section("HDR", () => activeDebugTab == 0)
                .Slider("Exposure", () => settings.SceneExposure, value => settings = (settings with { SceneExposure = Math.Clamp(value, GraphicsSettings.MinSceneExposure, GraphicsSettings.MaxSceneExposure) }).Normalized(), GraphicsSettings.MinSceneExposure, GraphicsSettings.MaxSceneExposure, "0.###", "Manual scene exposure before display transform.", () => activeDebugTab == 0)
                .Slider("Bloom Intensity", () => settings.BloomIntensity, value => settings = (settings with { BloomIntensity = Math.Clamp(value, GraphicsSettings.MinBloomIntensity, GraphicsSettings.MaxBloomIntensity) }).Normalized(), GraphicsSettings.MinBloomIntensity, GraphicsSettings.MaxBloomIntensity, "0.###", "Strength of pre-tonemap bloom energy.", () => activeDebugTab == 0)
                .Slider("Bloom Veil", () => settings.BloomVeilIntensity, value => settings = (settings with { BloomVeilIntensity = Math.Clamp(value, GraphicsSettings.MinBloomVeilIntensity, GraphicsSettings.MaxBloomVeilIntensity) }).Normalized(), GraphicsSettings.MinBloomVeilIntensity, GraphicsSettings.MaxBloomVeilIntensity, "0.###", "Low-frequency veil from bright HDR energy.", () => activeDebugTab == 0)
                .Section("Field Evidence", () => activeDebugTab == 0)
                .Readout("Field Evidence", FieldEvidenceDebugSummary, "Current field-evidence contract counts.", () => activeDebugTab == 0)
                .Section("Terminal", () => activeDebugTab == 1)
                .TextBox("", TerminalDisplay, UpdateTerminalInputFromDisplay, lines: 8, acceptsReturn: false, submit: ExecuteTerminalInput, monospace: true, alignBottom: true, tooltip: "Terminal log with live command prompt.", isVisible: () => activeDebugTab == 1)
                .Section("Synth Playground", () => activeDebugTab == 2)
                .Options("Preset", () => synthPlaygroundPreset, SelectSynthPreset, SynthPresetOptions, "Loads a built-in synth patch.", () => activeDebugTab == 2)
                .Readout("Status", () => $"{synthPlaygroundStatus.State}: {synthPlaygroundStatus.Message}", isVisible: () => activeDebugTab == 2)
                .TextBox("Patch", () => synthPlaygroundScript, value => synthPlaygroundScript = value, lines: 18, acceptsReturn: true, monospace: true, tooltip: "Patch DSL source.", isVisible: () => activeDebugTab == 2)
                .Slider("Gain", () => synthPlaygroundGain, value => synthPlaygroundGain = Math.Clamp(value, 0.0f, 1.0f), 0.0f, 1.0f, "0.###", "Playground patch gain.", () => activeDebugTab == 2)
                .Button("Play", () => synthPlaygroundPlayRevision++, "Triggers the compiled playground patch.", () => activeDebugTab == 2);
            });
        for (var panelIndex = 0; panelIndex < clientDebugPanels.Count; panelIndex++)
        {
            var tabIndex = panelIndex + 3;
            foreach (var control in clientDebugPanels[panelIndex].Controls)
            {
                ui.AddContractControl(control with { IsVisible = ComposeVisibility(control.IsVisible, () => activeDebugTab == tabIndex) });
            }
        }

        return ui;
    }

    private void SelectDebugTab(int index)
    {
        activeDebugTab = Math.Clamp(index, 0, Math.Max(0, debugTabTitles.Length - 1));
    }

    private static Func<bool> ComposeVisibility(Func<bool>? source, Func<bool> tabVisible)
    {
        return () => tabVisible() && (source?.Invoke() ?? true);
    }

    private string TerminalDisplay()
    {
        return string.Join('\n', terminalLines.TakeLast(10).Append($"> {terminalInput}"));
    }

    private string FieldEvidenceDebugSummary()
    {
        if (!activeFieldEvidenceFrame.HasInput)
        {
            return "none";
        }

        var errorCount = activeFieldEvidenceValidation.Issues.Count(issue => issue.Severity == AquariumFieldEvidenceIssueSeverity.Error);
        return
            $"domains {activeFieldEvidenceFrame.Domains.Count} / claims {activeFieldEvidenceFrame.Claims.Count} / " +
            $"resources {activeFieldEvidenceFrame.Resources.Count} resolved {activeFieldResourceStats.Resolved} unsupported {activeFieldResourceStats.Unsupported} / " +
            $"candidates {activeFieldEvidenceFrame.Candidates.Count} / packets {activeFieldEvidenceFrame.BackendPackets.Count} / " +
            $"planned {activeFieldLoweringPlan.Packets.Count} / deferred {activeFieldLoweringPlan.DeferredRequests.Count} / errors {errorCount}" +
            (activeFieldEvidenceFrame.TubeSplineLowerings.Count > 0
                ? $" / tube segments requested {activeTubeFieldRequestedSegments} dispatched {activeTubeFieldDispatchedSegments} truncated {activeTubeFieldTruncatedSegments} invalid {activeTubeFieldInvalidColumns} uploads {activeFieldResourceUploadCount} skipped {activeFieldResourceUploadSkippedCount} unplanned {activeTubeFieldUnplannedLowerings}"
                : string.Empty);
    }

    private void UpdateTerminalInputFromDisplay(string value)
    {
        if (value.EndsWith("\n>", StringComparison.Ordinal) || string.Equals(value, ">", StringComparison.Ordinal))
        {
            terminalInput = string.Empty;
            return;
        }

        var promptIndex = value.LastIndexOf("\n> ", StringComparison.Ordinal);
        if (promptIndex >= 0)
        {
            terminalInput = value[(promptIndex + 3)..].Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
            return;
        }

        if (value.StartsWith("> ", StringComparison.Ordinal))
        {
            terminalInput = value[2..].Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
            return;
        }

        terminalInput = string.Empty;
    }

    private void ExecuteTerminalInput()
    {
        var input = terminalInput.Trim();
        if (input.Length == 0)
        {
            return;
        }

        terminalLines.Add($"> {input}");
        terminalInput = "";
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = parts[0];
        var args = parts.Skip(1).ToArray();
        if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase))
        {
            var names = new[] { "help", "clear", "render" }
                .Concat(clientCommands.Select(item => item.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase);
            terminalLines.Add(string.Join(", ", names));
            return;
        }

        if (string.Equals(command, "clear", StringComparison.OrdinalIgnoreCase))
        {
            terminalLines.Clear();
            return;
        }

        if (string.Equals(command, "render", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length > 0 && int.TryParse(args[0], out var mode))
            {
                RenderDebugMode = Math.Clamp(mode, GraphicsSettings.MinRenderDebugMode, GraphicsSettings.MaxRenderDebugMode);
            }

            terminalLines.Add($"render debug {RenderDebugMode}");
            return;
        }

        var registered = clientCommands.FirstOrDefault(item => string.Equals(item.Name, command, StringComparison.OrdinalIgnoreCase));
        if (registered is not null)
        {
            terminalLines.Add(registered.Execute(args));
            return;
        }

        terminalLines.Add($"unknown command: {command}");
    }

    private void SelectSynthPreset(int index)
    {
        synthPlaygroundPreset = Math.Clamp(index, 0, Math.Max(0, SynthPresets.Length - 1));
        synthPlaygroundScript = SynthPresets[synthPlaygroundPreset].Script;
    }

    public AquariumFieldResourceLease LeaseTexture2D(AquariumTexture2DLeaseRequest request)
    {
        if (!request.IsValid ||
            !D3D12FieldTextureFormat.TryFormat(request.Format, out var format))
        {
            return AquariumFieldResourceLease.Invalid;
        }

        var width = Math.Max(1, request.Width);
        var height = Math.Max(1, request.Height);
        var resourceFlags = request.ProducerAccess == AquariumFieldShaderAccess.UnorderedAccess
            ? Vortice.Direct3D12.ResourceFlags.AllowUnorderedAccess
            : Vortice.Direct3D12.ResourceFlags.None;

        if (sharedTextureLeases.TryGetValue(request.ResourceKey, out var existing) &&
            existing.Width == width &&
            existing.Height == height &&
            existing.Format == format &&
            existing.ProducerAccess == request.ProducerAccess)
        {
            sharedTextureLeases[request.ResourceKey] = existing with { Version = request.Version };
            return CreateLease(request, existing);
        }

        if (existing is not null)
        {
            existing.Dispose();
            sharedTextureLeases.Remove(request.ResourceKey);
        }

        try
        {
            var resource = device.CreateCommittedResource(
                HeapType.Default,
                ResourceDescription.Texture2D(
                    format,
                    (uint)width,
                    (uint)height,
                    1,
                    1,
                    1,
                    0,
                    resourceFlags),
                ResourceStates.PixelShaderResource,
                null);
            var texture = D3D12FieldTexture2D.WrapOwned(
                resource,
                width,
                height,
                format,
                resourceFlags.HasFlag(Vortice.Direct3D12.ResourceFlags.AllowUnorderedAccess),
                ResourceStates.PixelShaderResource,
                $"Aquarium D3D12 Field Texture2D Lease {request.ResourceKey}");
            var producerFence = device.CreateFence(0);
            producerFence.Name = $"Aquarium D3D12 Field Texture2D Producer Fence {request.ResourceKey}";
            var slot = new SharedTextureLeaseSlot(
                texture,
                producerFence,
                width,
                height,
                format,
                request.ProducerAccess,
                request.Version);
            sharedTextureLeases[request.ResourceKey] = slot;
            return CreateLease(request, slot);
        }
        catch (SharpGenException ex)
        {
            LogTextureLeaseFailure(request, ex);
            return AquariumFieldResourceLease.Invalid;
        }
        catch (InvalidOperationException ex)
        {
            LogTextureLeaseFailure(request, ex);
            return AquariumFieldResourceLease.Invalid;
        }
    }

    private static void LogTextureLeaseFailure(AquariumTexture2DLeaseRequest request, Exception exception)
    {
        if (Environment.GetEnvironmentVariable("AQUARIUM_TEXTURE_LEASE_DIAG") == "1")
        {
            Console.WriteLine(
                $"aquarium-texture-lease-failed resource={request.ResourceKey} {request.Width}x{request.Height} format={request.Format} access={request.ProducerAccess} error={exception.GetType().Name}: {exception.Message}");
        }
    }

    public bool CommitLeaseVersion(string resourceKey, ulong version, ulong producerFenceValue)
    {
        if (string.IsNullOrWhiteSpace(resourceKey) ||
            !sharedTextureLeases.TryGetValue(resourceKey, out var slot))
        {
            return false;
        }

        sharedTextureLeases[resourceKey] = slot with
        {
            Version = version,
            CommittedProducerFenceValue = 0,
            WaitedProducerFenceValue = 0,
        };
        return true;
    }

    public bool UploadTexture2D(AquariumTexture2DUpload upload)
    {
        if (!upload.IsValid ||
            !sharedTextureLeases.TryGetValue(upload.ResourceKey, out var slot) ||
            slot.Width != upload.Width ||
            slot.Height != upload.Height ||
            !D3D12FieldTextureFormat.TryFormat(upload.Format, out var format) ||
            slot.Format != format)
        {
            return false;
        }

        lock (sharedTextureUploadLock)
        {
            return UploadTexture2DLocked(upload, slot);
        }
    }

    public void Render(AquariumFrame frame, int width, int height)
    {
        var frameCpuStart = Stopwatch.GetTimestamp();
        UpdateVisibleFrameRate(frameCpuStart);
        ResizeIfNeeded(width, height);
        ResizeReservoirWorkGridIfNeeded();
        ApplyCompletedPipelineBuild();
        TryHotReloadShaders();
        var frameResources = frames[frameIndex];
        WaitForFrame(frameResources);
        AccumulateCompletedGpuTimings(frameResources);
        frameResources.UploadRing.Reset();
        frameResources.TransientShaderDescriptors.Reset();

        if (!PipelinesReady)
        {
            RenderPipelineLoadingFrame(frame, frameResources, frameCpuStart);
            return;
        }

        var cameraFrustum = frame.View.Frustum.Normalized();
        var farDistance = cameraFrustum.Far;
        var jitterPixels = TemporalJitterPixels(temporalFrameIndex);
        if (temporalFrameIndex == 0)
        {
            previousCameraPosition = frame.CameraPosition;
            previousCameraTarget = frame.CameraTarget;
            previousViewCenter = frame.View.Center;
            previousCursorWorld = frame.CursorWorld;
            previousViewRadius = frame.View.Radius;
            previousTimeSeconds = frame.TimeSeconds;
            previousJitterPixels = jitterPixels;
        }

        WaitForFieldResourceProducerFences(frame.Scene.FieldEvidenceFrame);
        CopySceneState(frame.Scene);
        activeCameraPosition = frame.CameraPosition;
        activeCameraTarget = frame.CameraTarget;
        EnsureFractalReservoirBuffers(activeFractalReservoirField);
        EnsureFractalProgramTransformBuffer();
        EnsureBufferFieldProgramBuffers();
        var activeGpuSensorInput = frame.Scene.GpuSensorFrame.HasInput;
        var activeAccumulationWindow = activeGpuSensorInput
            ? frame.Scene.GpuSensorFrame.AccumulationWindowSeconds
            : frame.Scene.GpuFusionField.Seeds.Count > 0
                || frame.Scene.GpuFusionField.PointBuffer.HasInput
            ? frame.Scene.GpuFusionField.AccumulationWindowSeconds
            : frame.Scene.TemporalGaussianField.AccumulationWindowSeconds;
        var activePresentationDelay = activeGpuSensorInput
            ? frame.Scene.GpuSensorFrame.PresentationDelaySeconds
            : frame.Scene.GpuFusionField.Seeds.Count > 0
                || frame.Scene.GpuFusionField.PointBuffer.HasInput
            ? frame.Scene.GpuFusionField.PresentationDelaySeconds
            : frame.Scene.TemporalGaussianField.PresentationDelaySeconds;
        var frameConstants = frameResources.UploadRing.WriteConstant(new FrameConstants(
            new Vector2(reservoirWidth, reservoirHeight),
            frame.TimeSeconds,
            frame.View.Radius,
            frame.CameraPosition,
            farDistance,
            frame.CameraTarget,
            SceneFlags(frame.Scene),
            frame.View.Center,
            temporalFrameIndex,
            previousTimeSeconds,
            previousCameraPosition,
            previousViewRadius,
            previousCameraTarget,
            0.0f,
            previousViewCenter,
            jitterPixels,
            previousJitterPixels,
            RenderDebugMode,
            settings.SceneExposure,
            settings.BloomIntensity,
            settings.BloomVeilIntensity,
            Vector2.Zero,
            new Vector4(frame.CursorWorld.X, frame.CursorWorld.Y, previousCursorWorld.X, previousCursorWorld.Y),
            new Vector4(
                temporalGaussianCount,
                gpuSensorCameraCount,
                gpuSensorTextureCount,
                gpuFusionSeedCount),
            new Vector4(
                cameraFrustum.Left / cameraFrustum.Near,
                cameraFrustum.Right / cameraFrustum.Near,
                cameraFrustum.Bottom / cameraFrustum.Near,
                cameraFrustum.Top / cameraFrustum.Near),
            new Vector4(cameraFrustum.Near, cameraFrustum.Far, settings.FieldReservoirMode, 0.0f),
            new Vector4(
                acousticConstraintCount,
                gpuFusionPointCount,
                activePresentationDelay,
                frame.Scene.AcousticFieldFrame.HasInput ? 1.0f : 0.0f),
            new Vector4(
                activeFractalReservoirField.HasInput ? activeFractalReservoirField.SplatCount : 0,
                visibleFractalSplatCount,
                activeFractalReservoirField.HasInput ? activeFractalReservoirField.ReservoirUpdatesPerPass : 0,
                activeFractalReservoirField.HasInput ? activeFractalReservoirField.CandidatesPerReservoirUpdate : 0),
            activeFractalReservoirField.HasInput ? activeFractalReservoirField.WorldCenterRadius : Vector4.Zero,
            new Vector4(settings.FieldReservoirSpatialReuseBudget, 0.0f, 0.0f, 0.0f)));
        frameResources.FrameConstantsDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        device.CreateConstantBufferView(
            new ConstantBufferViewDescription(frameConstants.GpuVirtualAddress, frameConstants.SizeInBytes),
            frameResources.FrameConstantsDescriptor.Cpu);
        var heightFieldBrushConstantBuffer = frameResources.UploadRing.WriteConstant(heightFieldBrushConstants);
        frameResources.HeightFieldBrushConstantsDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        device.CreateConstantBufferView(
            new ConstantBufferViewDescription(heightFieldBrushConstantBuffer.GpuVirtualAddress, heightFieldBrushConstantBuffer.SizeInBytes),
            frameResources.HeightFieldBrushConstantsDescriptor.Cpu);

        frameResources.SdfLightDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sdfLightBuffer.CreateShaderResourceView(device, frameResources.SdfLightDescriptor);
        frameResources.SdfObjectDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sdfObjectBuffer.CreateShaderResourceView(device, frameResources.SdfObjectDescriptor);
        frameResources.GpuSensorCameraDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        gpuSensorCameraBuffer.CreateShaderResourceView(device, frameResources.GpuSensorCameraDescriptor);
        frameResources.GpuSensorTextureDescriptor = frameResources.TransientShaderDescriptors.AllocateRange(MaxGpuSensorTextureCount);
        CreateGpuSensorTextureViews(frame.Scene.GpuSensorFrame, frameResources.GpuSensorTextureDescriptor);
        frameResources.AcousticConstraintDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        acousticConstraintBuffer.CreateShaderResourceView(device, frameResources.AcousticConstraintDescriptor);
        frameResources.GpuFusionSeedDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        gpuFusionSeedBuffer.CreateShaderResourceView(device, frameResources.GpuFusionSeedDescriptor);
        frameResources.GpuFusionPointDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        gpuFusionPointBuffer.CreateShaderResourceView(device, frameResources.GpuFusionPointDescriptor);
        frameResources.TemporalGaussianDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        temporalGaussianBuffer.CreateShaderResourceView(device, frameResources.TemporalGaussianDescriptor);
        frameResources.TemporalGaussianUnorderedAccessDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        temporalGaussianBuffer.CreateUnorderedAccessView(device, frameResources.TemporalGaussianUnorderedAccessDescriptor);
        frameResources.BlueNoiseDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        blueNoiseTexture.CreateShaderResourceView(device, frameResources.BlueNoiseDescriptor);
        frameResources.StudioPmremDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        studioPmremTexture.CreateShaderResourceView(device, frameResources.StudioPmremDescriptor);
        frameResources.StudioIrradianceDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        studioIrradianceTexture.CreateShaderResourceView(device, frameResources.StudioIrradianceDescriptor);
        frameResources.HeightFieldDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        heightFieldRenderTarget.CreateShaderResourceView(device, frameResources.HeightFieldDescriptor);
        frameResources.SceneDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneRenderTarget.CreateShaderResourceView(device, frameResources.SceneDescriptor);
        frameResources.SceneMetadataDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneMetadataRenderTarget.CreateShaderResourceView(device, frameResources.SceneMetadataDescriptor);
        frameResources.SceneControlDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneControlRenderTarget.CreateShaderResourceView(device, frameResources.SceneControlDescriptor);
        frameResources.SceneReservoirGuideDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneReservoirGuideRenderTarget.CreateShaderResourceView(device, frameResources.SceneReservoirGuideDescriptor);
        frameResources.SceneCandidateDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneCandidateRenderTarget.CreateShaderResourceView(device, frameResources.SceneCandidateDescriptor);
        frameResources.SceneCandidateMetadataDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneCandidateMetadataRenderTarget.CreateShaderResourceView(device, frameResources.SceneCandidateMetadataDescriptor);
        frameResources.SceneCandidateControlDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneCandidateControlRenderTarget.CreateShaderResourceView(device, frameResources.SceneCandidateControlDescriptor);
        frameResources.SceneCandidateReservoirGuideDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneCandidateReservoirGuideRenderTarget.CreateShaderResourceView(device, frameResources.SceneCandidateReservoirGuideDescriptor);
        frameResources.SceneOverdrawDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        sceneOverdrawRenderTarget.CreateShaderResourceView(device, frameResources.SceneOverdrawDescriptor);
        frameResources.ReservoirResolvedDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        reservoirResolvedRenderTarget.CreateShaderResourceView(device, frameResources.ReservoirResolvedDescriptor);
        frameResources.ReservoirResolvedUnorderedAccessDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        reservoirResolvedRenderTarget.CreateUnorderedAccessView(device, frameResources.ReservoirResolvedUnorderedAccessDescriptor);
        frameResources.FieldReservoirCandidateUnorderedAccessDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        fieldReservoirCandidateBuffer.CreateUnorderedAccessView(device, frameResources.FieldReservoirCandidateUnorderedAccessDescriptor);
        frameResources.FieldReservoirLockUnorderedAccessDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        fieldReservoirLockBuffer.CreateUnorderedAccessView(device, frameResources.FieldReservoirLockUnorderedAccessDescriptor);
        for (var level = 0; level < BloomLevelCount; level++)
        {
            frameResources.BloomDescriptors[level] = frameResources.TransientShaderDescriptors.Allocate();
            bloomRenderTargets[level].CreateShaderResourceView(device, frameResources.BloomDescriptors[level]);
            frameResources.BloomScratchDescriptors[level] = frameResources.TransientShaderDescriptors.Allocate();
            bloomScratchTargets[level].CreateShaderResourceView(device, frameResources.BloomScratchDescriptors[level]);
        }

        frameResources.BloomPresentationDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        bloomRenderTargets[0].CreateShaderResourceView(device, frameResources.BloomPresentationDescriptor);
        for (var level = 1; level < BloomLevelCount; level++)
        {
            var descriptor = frameResources.TransientShaderDescriptors.Allocate();
            bloomRenderTargets[level].CreateShaderResourceView(device, descriptor);
        }

        frameResources.CommandAllocator.Reset();
        commandList.Reset(frameResources.CommandAllocator, null);

        var recordCpuStart = Stopwatch.GetTimestamp();
        commandList.BeginEvent("Aquarium D3D12 Frame");
        BeginGpuTiming(commandList, frameResources, D3D12GpuTimingPass.FrameRecord);
        commandList.SetDescriptorHeaps(frameResources.TransientShaderDescriptors.Heap);
        var passContext = new D3D12PassContext(commandList, frameResources.BackBuffer, frameResources.BackBufferRenderTargetView.Cpu);
        if (hasSceneRenderingPass)
        {
            UploadSceneStructuredResources(commandList, frameResources);
            DispatchGpuSensorFusion(commandList, frameResources);
            DispatchStereoDepth(commandList, frameResources);
            DispatchPointClouds(commandList, frameResources);
            DispatchFractalReservoirs(commandList, frameResources);
            DispatchTubeFields(commandList, frameResources);
            RenderHeightField(commandList, frameResources);
            RenderSceneAndPresent(passContext, frameResources);
        }
        else
        {
            ClearEmptyBackBuffer(passContext);
        }
        EndGpuTiming(commandList, frameResources, D3D12GpuTimingPass.FrameRecord);
        ResolveGpuTimings(commandList, frameResources, temporalFrameIndex > 4);
        commandList.EndEvent();
        commandList.Close();
        var recordCpuMilliseconds = ElapsedMilliseconds(recordCpuStart);

        commandQueue.ExecuteCommandList(commandList);
        SignalProgramOutputPublication();
        var overlayCpuStart = Stopwatch.GetTimestamp();
        RenderOverlay(frame, frameResources);
        var overlayCpuMilliseconds = ElapsedMilliseconds(overlayCpuStart);
        swapChain.Present(1, PresentFlags.None);
        var frameCpuMilliseconds = ElapsedMilliseconds(frameCpuStart);
        if (temporalFrameIndex > 4)
        {
            accumulatedRecordCpuMilliseconds += recordCpuMilliseconds;
            accumulatedOverlayCpuMilliseconds += overlayCpuMilliseconds;
            accumulatedFrameCpuMilliseconds += frameCpuMilliseconds;
            accumulatedTimingFrames++;
        }
        SignalFrame(frameResources);
        ReportCapacityOncePerSecond(frame.TimeSeconds, frameResources);
        previousCameraPosition = frame.CameraPosition;
        previousCameraTarget = frame.CameraTarget;
        previousViewCenter = frame.View.Center;
        previousCursorWorld = frame.CursorWorld;
        previousViewRadius = frame.View.Radius;
        previousTimeSeconds = frame.TimeSeconds;
        previousJitterPixels = jitterPixels;
        hasPresentedReadyFrame = true;
        temporalFrameIndex++;
        frameIndex = (int)swapChain.CurrentBackBufferIndex;
    }

    public unsafe void SaveFramePng(string path)
    {
        if (!hasPresentedReadyFrame)
        {
            throw new InvalidOperationException("No completed frame is available to save.");
        }

        WaitForGpu();

        var sourceIndex = (frameIndex + BackBufferCount - 1) % BackBufferCount;
        var source = frames[sourceIndex].BackBuffer;
        var rowBytes = checked(width * 4);
        var rowPitch = AlignTo(rowBytes, D3D12.TextureDataPitchAlignment);
        var bufferBytes = checked((long)rowPitch * height);
        using var readback = device.CreateCommittedResource(
            HeapType.Readback,
            ResourceDescription.Buffer((ulong)bufferBytes),
            ResourceStates.CopyDest,
            null);
        readback.Name = "Aquarium D3D12 Frame PNG Readback";

        using var captureAllocator = device.CreateCommandAllocator(CommandListType.Direct);
        using var captureList = device.CreateCommandList<ID3D12GraphicsCommandList>(0, CommandListType.Direct, captureAllocator, null);
        source.Transition(captureList, ResourceStates.CopySource);
        var footprint = new PlacedSubresourceFootPrint
        {
            Offset = 0,
            Footprint = new SubresourceFootPrint(
                Format.B8G8R8A8_UNorm,
                (uint)width,
                (uint)height,
                1,
                (uint)rowPitch),
        };
        var destination = new TextureCopyLocation(readback, footprint);
        var textureSource = new TextureCopyLocation(source.Resource, 0);
        captureList.CopyTextureRegion(destination, 0, 0, 0, textureSource, null);
        source.Transition(captureList, ResourceStates.Present);
        captureList.Close();
        commandQueue.ExecuteCommandList(captureList);
        WaitForGpu();

        var rgba = new byte[checked(rowBytes * height)];
        var mapped = readback.Map<byte>(0);
        try
        {
            for (var y = 0; y < height; y++)
            {
                var sourceRow = mapped + ((long)y * rowPitch);
                var destinationRow = y * rowBytes;
                for (var x = 0; x < width; x++)
                {
                    var sourcePixel = sourceRow + (x * 4);
                    var destinationPixel = destinationRow + (x * 4);
                    rgba[destinationPixel] = sourcePixel[2];
                    rgba[destinationPixel + 1] = sourcePixel[1];
                    rgba[destinationPixel + 2] = sourcePixel[0];
                    rgba[destinationPixel + 3] = sourcePixel[3];
                }
            }
        }
        finally
        {
            readback.Unmap(0, null);
        }

        PngImageWriter.WriteRgba(path, width, height, rgba);
    }

    private static Vector2 TemporalJitterPixels(int index)
    {
        const float scale = 0.42f;
        return new Vector2(
            (Halton(index + 1, 2) - 0.5f) * scale,
            (Halton(index + 1, 3) - 0.5f) * scale);
    }

    private static float Halton(int index, int basis)
    {
        var result = 0.0f;
        var fraction = 1.0f / basis;
        while (index > 0)
        {
            result += fraction * (index % basis);
            index /= basis;
            fraction /= basis;
        }

        return result;
    }

    private bool PipelinesReady =>
        heightFieldBasePipelineState is not null
        && heightFieldBrushPipelineState is not null
        && scenePipelineState is not null
        && splinePipelineState is not null
        && temporalGaussianPipelineState is not null
        && gpuSensorFusionPipelineState is not null
        && stereoDepthPipelineState is not null
        && pointCloudComputePipelineState is not null
        && pointCloudRenderPipelineState is not null
        && fractalSurfaceSplatRenderPipelineState is not null
        && fractalTransparentSplatRenderPipelineState is not null
        && fractalSplatPipelineState is not null
        && fractalSdfReservoirPipelineState is not null
        && fractalPbrReservoirPipelineState is not null
        && fractalRadiosityReservoirPipelineState is not null
        && tubeFieldComputePipelineState is not null
        && tubeFieldRenderPipelineState is not null
        && bokushoBrushPipelineState is not null
        && fieldReservoirResolvePipelineState is not null
        && sdfProxyPipelineStates.All(pipeline => pipeline is not null)
        && bloomPrefilterPipelineState is not null
        && bloomDownsamplePipelineState is not null
        && bloomBlurHorizontalPipelineState is not null
        && bloomBlurVerticalPipelineState is not null
        && resolvePipelineState is not null;

    private void RenderPipelineLoadingFrame(AquariumFrame frame, FrameResources frameResources, long frameCpuStart)
    {
        var frameCpuMilliseconds = ElapsedMilliseconds(frameCpuStart);
        accumulatedFrameCpuMilliseconds += frameCpuMilliseconds;
        accumulatedTimingFrames++;
        ReportCapacityOncePerSecond(frame.TimeSeconds, frameResources);
        Thread.Sleep(8);
    }

    public void Dispose()
    {
        WaitForGpu();
        try
        {
            pipelineBuildTask?.Wait(TimeSpan.FromMilliseconds(50));
        }
        catch
        {
            // A failed background compile has already been reported or will be discarded with the renderer.
        }

        fenceEvent.Dispose();
        fence.Dispose();
        commandList.Dispose();
        DisposeBackBufferOverlays();
        overlayOn12Device.Dispose();
        overlayContext.Dispose();
        overlayDevice.Dispose();
        DisposePipelineStates();
        fullscreenRootSignature.Dispose();
        gpuSensorFusionRootSignature.Dispose();
        pointCloudRootSignature.Dispose();
        fractalReservoirRootSignature.Dispose();
        tubeFieldRootSignature.Dispose();
        tubeFieldRenderRootSignature.Dispose();
        bokushoBrushRootSignature.Dispose();
        generatedMeshDrawCommandSignature.Dispose();
        blueNoiseTexture.Dispose();
        tubeFieldFallbackRampTexture?.Dispose();
        studioIrradianceTexture.Dispose();
        studioPmremTexture.Dispose();
        DisposeSharedTextureLeases();
        DisposeExternalProducerFences();
        fieldResourceRegistry.Dispose();
        DisposeFractalReservoirBuffers();
        bufferFieldTextureSplineProgramBuffer?.Dispose();
        bufferFieldTextureSampleBuffer?.Dispose();
        tubeFieldStatsBuffer.Dispose();
        tubeFieldDrawArgumentBuffer.Dispose();
        tubeFieldReplayManifestBuffer.Dispose();
        bokushoBrushCanvasBuffer.Dispose();
        bokushoBrushTraceBuffer.Dispose();
        bokushoBrushStrokeBuffer.Dispose();
        tubeFieldSegmentBuffer.Dispose();
        tubeFieldIndexBuffer.Dispose();
        tubeFieldVertexBuffer.Dispose();
        fieldReservoirLockBuffer.Dispose();
        fieldReservoirCandidateBuffer.Dispose();
        DisposeFieldReservoirHistoryBuffers();
        temporalGaussianBuffer.Dispose();
        gpuFusionPointBuffer.Dispose();
        gpuFusionSeedBuffer.Dispose();
        acousticConstraintBuffer.Dispose();
        gpuSensorCameraBuffer.Dispose();
        foreach (var texture in externalSensorTextures.Values)
        {
            texture.Dispose();
        }

        externalSensorTextures.Clear();
        sdfObjectBuffer.Dispose();
        sdfLightBuffer.Dispose();
        DisposeBloomRenderTargets();
        DisposeGraphRenderTargets();
        DisposeProgramOutputTexture();
        DisposeProgramOutputFence();
        sceneRenderTarget.Dispose();
        sceneMetadataRenderTarget.Dispose();
        sceneControlRenderTarget.Dispose();
        sceneReservoirGuideRenderTarget.Dispose();
        sceneCandidateReservoirGuideRenderTarget.Dispose();
        sceneCandidateControlRenderTarget.Dispose();
        sceneCandidateMetadataRenderTarget.Dispose();
        sceneCandidateRenderTarget.Dispose();
        sceneOverdrawRenderTarget.Dispose();
        reservoirResolvedRenderTarget.Dispose();
        sceneDepthTarget.Dispose();
        heightFieldRenderTarget.Dispose();
        for (var index = 0; index < frames.Length; index++)
        {
            frames[index].Dispose();
        }
        staticShaderDescriptorArena.Dispose();
        cpuShaderDescriptorArena.Dispose();
        depthStencilViewArena.Dispose();
        renderTargetViewArena.Dispose();
        swapChain.Dispose();
        commandQueue.Dispose();
        device.Dispose();
        factory.Dispose();
    }

    private void CreateRenderTargetViews()
    {
        for (var index = 0; index < frames.Length; index++)
        {
            var backBuffer = swapChain.GetBuffer<ID3D12Resource>((uint)index);
            var backBufferResource = new D3D12TrackedResource(backBuffer, ResourceStates.Present, $"Aquarium D3D12 Backbuffer {index}", ownsResource: true);
            frames[index].BackBuffer = backBufferResource;
            resourceRegistry.Add($"backbuffer-{index}", backBufferResource);
            frames[index].BackBufferRenderTargetView = renderTargetViewArena.Allocate();
            device.CreateRenderTargetView(frames[index].BackBuffer.Resource, null, frames[index].BackBufferRenderTargetView.Cpu);
        }
    }

    private static AquariumFieldResourceLease CreateLease(
        AquariumTexture2DLeaseRequest request,
        SharedTextureLeaseSlot slot)
    {
        var declaration = new AquariumFieldResourceDeclaration(
            ResourceKey: request.ResourceKey,
            Kind: AquariumFieldResourceKind.Texture2D,
            Residency: AquariumFieldResourceResidency.SharedGpu,
            Access: AquariumFieldShaderAccess.ShaderResource,
            Format: request.Format,
            Width: slot.Width,
            Height: slot.Height,
            DepthOrCount: 1,
            StrideBytes: AquariumFieldResourceDeclaration.FormatStrideBytes(request.Format),
            ValidFromNs: request.ValidFromNs,
            ValidUntilNs: request.ValidUntilNs,
            Version: request.Version,
            NativeHandle: slot.NativeHandle,
            NativeHandleKind: "fensalir-owned-d3d12-texture2d");
        return new AquariumFieldResourceLease(
            declaration,
            slot.NativeHandle,
            "fensalir-owned-d3d12-texture2d",
            IntPtr.Zero,
            request.Version,
            true);
    }

    private bool UploadTexture2DLocked(AquariumTexture2DUpload upload, SharedTextureLeaseSlot slot)
    {
        ID3D12CommandAllocator? uploadAllocator = null;
        ID3D12GraphicsCommandList? uploadList = null;
        ID3D12Resource? uploadResource = null;
        try
        {
            if (string.Equals(upload.Format.Trim(), "NV12", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rowBytes = RowBytes(upload.Format, upload.Width);
            if (rowBytes <= 0 || upload.SourceStrideBytes < rowBytes)
            {
                return false;
            }

            var rowPitch = AlignTo(rowBytes, D3D12.TextureDataPitchAlignment);
            var uploadBytes = checked(rowPitch * upload.Height);
            uploadResource = device.CreateCommittedResource(
                HeapType.Upload,
                ResourceDescription.Buffer((ulong)uploadBytes),
                ResourceStates.GenericRead,
                null);
            uploadResource.Name = $"Aquarium D3D12 Texture2D Lease Upload {upload.ResourceKey}";

            unsafe
            {
                var mapped = uploadResource.Map<byte>(0);
                try
                {
                    var sourceBytes = upload.Data.Span;
                    for (var row = 0; row < upload.Height; row++)
                    {
                        sourceBytes.Slice(row * upload.SourceStrideBytes, rowBytes)
                            .CopyTo(new Span<byte>(mapped + (row * rowPitch), rowBytes));
                    }
                }
                finally
                {
                    uploadResource.Unmap(0, null);
                }
            }

            uploadAllocator = device.CreateCommandAllocator(CommandListType.Direct);
            uploadList = device.CreateCommandList<ID3D12GraphicsCommandList>(0, CommandListType.Direct, uploadAllocator, null);
            var source = new TextureCopyLocation(
                uploadResource,
                new PlacedSubresourceFootPrint
                {
                    Offset = 0,
                    Footprint = new SubresourceFootPrint(slot.Format, (uint)upload.Width, (uint)upload.Height, 1, (uint)rowPitch),
                });
            var destination = new TextureCopyLocation(slot.Texture.Resource, 0);
            uploadList.ResourceBarrier(ResourceBarrier.BarrierTransition(
                slot.Texture.Resource,
                ResourceStates.PixelShaderResource,
                ResourceStates.CopyDest));
            uploadList.CopyTextureRegion(destination, 0, 0, 0, source, null);
            uploadList.ResourceBarrier(ResourceBarrier.BarrierTransition(
                slot.Texture.Resource,
                ResourceStates.CopyDest,
                ResourceStates.PixelShaderResource));
            uploadList.Close();
            commandQueue.ExecuteCommandList(uploadList);
            var signalValue = ++fenceValue;
            commandQueue.Signal(fence, signalValue).CheckError();
            if (fence.CompletedValue < signalValue)
            {
                fence.SetEventOnCompletion(signalValue, fenceEvent.SafeWaitHandle.DangerousGetHandle()).CheckError();
                fenceEvent.WaitOne();
            }

            slot.CommittedProducerFenceValue = 0;
            slot.WaitedProducerFenceValue = 0;
            return true;
        }
        catch (SharpGenException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        finally
        {
            uploadList?.Dispose();
            uploadAllocator?.Dispose();
            uploadResource?.Dispose();
        }
    }

    private static int RowBytes(string format, int width)
    {
        var stride = AquariumFieldResourceDeclaration.FormatStrideBytes(format);
        return format.Trim().ToUpperInvariant() switch
        {
            "NV12" => width,
            _ => checked(Math.Max(1, width) * Math.Max(1, stride)),
        };
    }

    private void WaitForFieldResourceProducerFences(AquariumFieldEvidenceFrame fieldEvidenceFrame)
    {
        if (!fieldEvidenceFrame.HasInput)
        {
            return;
        }

        foreach (var resource in fieldEvidenceFrame.Resources)
        {
            if (sharedTextureLeases.TryGetValue(resource.ResourceKey, out var slot) &&
                slot.CommittedProducerFenceValue > 0 &&
                slot.WaitedProducerFenceValue < slot.CommittedProducerFenceValue)
            {
                commandQueue.Wait(slot.ProducerFence, slot.CommittedProducerFenceValue).CheckError();
                slot.WaitedProducerFenceValue = slot.CommittedProducerFenceValue;
            }

            WaitForExternalProducerFence(resource);
        }
    }

    private void WaitForExternalProducerFence(AquariumFieldResourceDeclaration resource)
    {
        if (resource.ProducerFenceHandle == IntPtr.Zero ||
            resource.ProducerFenceValue == 0)
        {
            return;
        }

        if (externalProducerFences.TryGetValue(resource.ResourceKey, out var slot) &&
            slot.Handle != resource.ProducerFenceHandle)
        {
            slot.Dispose();
            externalProducerFences.Remove(resource.ResourceKey);
            slot = null;
        }

        if (slot is null)
        {
            try
            {
                slot = new ExternalProducerFenceSlot(
                    device.OpenSharedHandle<ID3D12Fence>(resource.ProducerFenceHandle),
                    resource.ProducerFenceHandle);
                externalProducerFences[resource.ResourceKey] = slot;
            }
            catch (SharpGenException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
        }

        if (slot.WaitedValue >= resource.ProducerFenceValue)
        {
            return;
        }

        commandQueue.Wait(slot.Fence, resource.ProducerFenceValue).CheckError();
        slot.WaitedValue = resource.ProducerFenceValue;
    }

    private void DisposeSharedTextureLeases()
    {
        foreach (var slot in sharedTextureLeases.Values)
        {
            slot.Dispose();
        }

        sharedTextureLeases.Clear();
    }

    private void DisposeExternalProducerFences()
    {
        foreach (var slot in externalProducerFences.Values)
        {
            slot.Dispose();
        }

        externalProducerFences.Clear();
    }

    private int ResolveProgramOutputRingCount()
    {
        var value = Environment.GetEnvironmentVariable(ProgramOutputRingCountEnvironmentVariable);
        if (!int.TryParse(value, CultureInfo.InvariantCulture, out var count))
        {
            return 1;
        }

        return Math.Clamp(count, 1, MaxProgramOutputRingCount);
    }

    private string GetProgramOutputTextureName(int slot)
    {
        return programOutputRingCount <= 1
            ? programOutputSharedName
            : string.Create(CultureInfo.InvariantCulture, $"{programOutputSharedName}.{slot}");
    }

    private void TryOpenProgramOutputConsumerFence()
    {
        if (programOutputConsumerFence is not null || string.IsNullOrWhiteSpace(programOutputConsumerFenceName))
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        if (now < nextProgramOutputConsumerFenceRetryTimestamp)
        {
            return;
        }

        nextProgramOutputConsumerFenceRetryTimestamp = now + Stopwatch.Frequency;
        try
        {
            var sharedHandle = device.OpenSharedHandleByName(programOutputConsumerFenceName);
            try
            {
                programOutputConsumerFence = device.OpenSharedHandle<ID3D12Fence>(sharedHandle);
                programOutputConsumerFence.Name = "Aquarium D3D12 Program Output Consumer Fence";
                Console.WriteLine($"D3D12 program output consumer fence opened: name={programOutputConsumerFenceName}");
            }
            finally
            {
                CloseHandle(sharedHandle);
            }
        }
        catch (SharpGenException)
        {
            // OBS may start after Fensalir. Retry quietly; the fence is an optional
            // overwrite-prevention contract, not a startup dependency.
        }
    }

    private bool TrySelectProgramOutputSlot(ulong publishedFenceValue, out int slot)
    {
        slot = (int)(publishedFenceValue % (ulong)programOutputTextures.Count);
        if (programOutputConsumerFence is null)
        {
            return true;
        }

        var consumerCompletedValue = programOutputConsumerFence.CompletedValue;
        if (programOutputTextureFenceValues[slot] <= consumerCompletedValue)
        {
            return true;
        }

        for (var candidate = 0; candidate < programOutputTextureFenceValues.Count; candidate++)
        {
            if (programOutputTextureFenceValues[candidate] <= consumerCompletedValue)
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private void CreateProgramOutputTexture()
    {
        if (!programOutputEnabled)
        {
            return;
        }

        DisposeProgramOutputTexture();
        for (var slot = 0; slot < programOutputRingCount; slot++)
        {
            var slotName = GetProgramOutputTextureName(slot);
            var resource = device.CreateCommittedResource(
                HeapType.Default,
                HeapFlags.Shared,
                ResourceDescription.Texture2D(
                    Format.B8G8R8A8_UNorm,
                    (uint)width,
                    (uint)height,
                    1,
                    1,
                    1,
                    0,
                    Vortice.Direct3D12.ResourceFlags.None),
                ResourceStates.CopyDest,
                null);
            var tracked = new D3D12TrackedResource(
                resource,
                ResourceStates.CopyDest,
                $"Aquarium D3D12 Shared Program Output {slot}",
                ownsResource: true);
            var sharedHandle = device.CreateSharedHandle(
                resource,
                null,
                slotName);
            programOutputTextures.Add(tracked);
            programOutputSharedHandles.Add(sharedHandle);
            programOutputTextureFenceValues.Add(0);
            Console.WriteLine($"D3D12 program output shared texture: name={slotName} slot={slot}/{programOutputRingCount} size={width}x{height}");
        }
    }

    private void CreateProgramOutputFenceHandle()
    {
        if (!programOutputEnabled)
        {
            return;
        }

        DisposeProgramOutputFenceHandle();
        if (programOutputFence is null)
        {
            return;
        }

        programOutputFenceSharedHandle = device.CreateSharedHandle(
            programOutputFence,
            null,
            programOutputFenceName);
        Console.WriteLine($"D3D12 program output shared fence: name={programOutputFenceName}");
    }

    private void CopyProgramOutputBackBuffer(ID3D12GraphicsCommandList activeCommandList, D3D12TrackedResource backBuffer)
    {
        if (programOutputTextures.Count == 0)
        {
            return;
        }

        var publishedFenceValue = programOutputFenceValue + 1;
        TryOpenProgramOutputConsumerFence();
        if (!TrySelectProgramOutputSlot(publishedFenceValue, out var slot))
        {
            return;
        }

        var programOutputTexture = programOutputTextures[slot];
        backBuffer.Transition(activeCommandList, ResourceStates.CopySource);
        programOutputTexture.Transition(activeCommandList, ResourceStates.CopyDest);
        activeCommandList.CopyResource(programOutputTexture.Resource, backBuffer.Resource);
        programOutputTexture.Transition(activeCommandList, ResourceStates.Common);
        pendingProgramOutputFenceValue = publishedFenceValue;
        programOutputTextureFenceValues[slot] = publishedFenceValue;
    }

    private void SignalProgramOutputPublication()
    {
        if (programOutputFence is null || pendingProgramOutputFenceValue == 0)
        {
            return;
        }

        commandQueue.Signal(programOutputFence, pendingProgramOutputFenceValue).CheckError();
        programOutputFenceValue = pendingProgramOutputFenceValue;
        pendingProgramOutputFenceValue = 0;
    }

    private void DisposeProgramOutputTexture()
    {
        foreach (var sharedHandle in programOutputSharedHandles)
        {
            if (sharedHandle != IntPtr.Zero)
            {
                CloseHandle(sharedHandle);
            }
        }

        programOutputSharedHandles.Clear();
        foreach (var texture in programOutputTextures)
        {
            texture.Dispose();
        }

        programOutputTextures.Clear();
        programOutputTextureFenceValues.Clear();
    }

    private void DisposeProgramOutputFenceHandle()
    {
        if (programOutputFenceSharedHandle != IntPtr.Zero)
        {
            CloseHandle(programOutputFenceSharedHandle);
            programOutputFenceSharedHandle = IntPtr.Zero;
        }
    }

    private void DisposeProgramOutputFence()
    {
        DisposeProgramOutputFenceHandle();
        programOutputFence?.Dispose();
        programOutputFence = null;
        programOutputConsumerFence?.Dispose();
        programOutputConsumerFence = null;
        programOutputFenceValue = 0;
        pendingProgramOutputFenceValue = 0;
        nextProgramOutputConsumerFenceRetryTimestamp = 0;
    }

    private D3D12CubeTexture LoadStudioPmremTexture()
    {
        return LoadStudioCubeTexture(StudioPmremRelativePath, "Studio PMREM", "Aquarium D3D12 Studio PMREM Cubemap");
    }

    private D3D12CubeTexture LoadStudioIrradianceTexture()
    {
        return LoadStudioCubeTexture(StudioIrradianceRelativePath, "Studio irradiance", "Aquarium D3D12 Studio Irradiance Cubemap");
    }

    private D3D12BlueNoiseTexture CreateBlueNoiseTexture()
    {
        var frameResources = frames[frameIndex];
        frameResources.CommandAllocator.Reset();
        commandList.Reset(frameResources.CommandAllocator, null);
        var texture = D3D12BlueNoiseTexture.Create(device, commandList, "Aquarium D3D12 Blue Noise Threshold Tile", out var uploadResource);
        commandList.Close();
        commandQueue.ExecuteCommandList(commandList);
        WaitForGpu();
        uploadResource.Dispose();
        return texture;
    }

    private D3D12CubeTexture LoadStudioCubeTexture(string relativePath, string label, string resourceName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{label} cubemap asset was not copied to the runtime asset directory.", path);
        }

        var frameResources = frames[frameIndex];
        frameResources.CommandAllocator.Reset();
        commandList.Reset(frameResources.CommandAllocator, null);
        var texture = D3D12CubeTexture.LoadRgba16FloatDds(device, commandList, path, resourceName, out var uploadResource);
        commandList.Close();
        commandQueue.ExecuteCommandList(commandList);
        WaitForGpu();
        uploadResource.Dispose();
        return texture;
    }

    private void CreateOverlayDevice(out ID3D11Device createdDevice, out ID3D11DeviceContext createdContext, out ID3D11On12Device createdOn12Device)
    {
        var featureLevels = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };
        var commandQueues = new IUnknown[] { commandQueue };
        Apis.D3D11On12CreateDevice(
            device,
            D3D11DeviceCreationFlags.BgraSupport,
            featureLevels,
            commandQueues,
            0,
            out createdDevice,
            out createdContext,
            out _).CheckError();
        createdOn12Device = createdDevice.QueryInterface<ID3D11On12Device>();
    }

    private void CreateBackBufferOverlays()
    {
        overlayWrappedBackBuffers = new ID3D11Resource[frames.Length];
        overlays = new DirectWriteOverlay[frames.Length];
        var flags = new Vortice.Direct3D11on12.ResourceFlags
        {
            BindFlags = D3D11BindFlags.RenderTarget,
        };

        for (var index = 0; index < frames.Length; index++)
        {
            var wrapped = overlayOn12Device.CreateWrappedResource<ID3D11Resource>(
                frames[index].BackBuffer.Resource,
                flags,
                ResourceStates.RenderTarget,
                ResourceStates.Present);
            overlayWrappedBackBuffers[index] = wrapped;
            using var surface = wrapped.QueryInterface<IDXGISurface>();
            overlays[index] = new DirectWriteOverlay(surface, width, height);
        }
    }

    private void RenderOverlay(AquariumFrame frame, FrameResources frameResources)
    {
        var wrappedBackBuffer = overlayWrappedBackBuffers[frameIndex];
        overlayOn12Device.AcquireWrappedResources([wrappedBackBuffer]);
        overlays[frameIndex].Render(frame, RenderDebugMode, debugUi, clientUiPanels, clientUiSurfaces, visibleFrameRateText);
        overlayOn12Device.ReleaseWrappedResources([wrappedBackBuffer]);
        overlayContext.Flush();
        frameResources.BackBuffer.MarkState(ResourceStates.Present);
    }

    private void DisposeBackBufferOverlays()
    {
        foreach (var overlay in overlays)
        {
            overlay?.Dispose();
        }

        foreach (var wrappedBackBuffer in overlayWrappedBackBuffers)
        {
            wrappedBackBuffer?.Dispose();
        }

        overlays = [];
        overlayWrappedBackBuffers = [];
        overlayContext.ClearState();
        overlayContext.Flush();
    }

    private D3D12PipelineSet CreatePipelineSet(D3D12ShaderPaths paths)
    {
        static void Step(string name)
        {
            if (Environment.GetEnvironmentVariable("AQUARIUM_PIPELINE_DIAG") == "1")
            {
                Console.WriteLine($"D3D12 pipeline build step: {name}");
            }
        }

        Step("height-field-base");
        var heightFieldBase = CreateHeightFieldBasePipelineState(paths.HeightField);
        heightFieldBase.Name = "Aquarium D3D12 Height Field Base Pipeline";
        Step("height-field-brush");
        var heightFieldBrush = CreateHeightFieldBrushPipelineState(paths.HeightField);
        heightFieldBrush.Name = "Aquarium D3D12 Height Field Brush Pipeline";
        Step("scene");
        var scene = CreateScenePipelineState(paths.Scene);
        scene.Name = "Aquarium D3D12 Scene Pipeline";
        Step("spline");
        var spline = CreateSplinePipelineState(paths.Spline);
        spline.Name = "Aquarium D3D12 Spline Surface Claim Pipeline";
        Step("temporal-gaussian");
        var temporalGaussian = CreateTemporalGaussianPipelineState(paths.TemporalGaussian);
        temporalGaussian.Name = "Aquarium D3D12 Temporal Gaussian Pipeline";
        Step("gpu-sensor-fusion");
        var gpuSensorFusion = CreateGpuSensorFusionPipelineState(paths.GpuSensorFusion);
        gpuSensorFusion.Name = "Aquarium D3D12 GPU Sensor Fusion Compute Pipeline";
        Step("stereo-depth");
        var stereoDepth = CreateStereoDepthPipelineState(paths.StereoDepth);
        stereoDepth.Name = "Aquarium D3D12 Packed Stereo Depth Compute Pipeline";
        Step("point-cloud-compute");
        var pointCloudCompute = CreatePointCloudComputePipelineState(paths.PointCloud);
        pointCloudCompute.Name = "Aquarium D3D12 Point Cloud Projection Compute Pipeline";
        Step("point-cloud-render");
        var pointCloudRender = CreatePointCloudRenderPipelineState(paths.PointCloud);
        pointCloudRender.Name = "Aquarium D3D12 Point Cloud Render Pipeline";
        Step("fractal-surface-splat");
        var fractalSurfaceSplatRender = CreateFractalSurfaceSplatRenderPipelineState(paths.FractalSplatRender);
        fractalSurfaceSplatRender.Name = "Aquarium D3D12 Fractal Surface Splat Render Pipeline";
        Step("fractal-transparent-splat");
        var fractalTransparentSplatRender = CreateFractalTransparentSplatRenderPipelineState(paths.FractalSplatRender);
        fractalTransparentSplatRender.Name = "Aquarium D3D12 Fractal Transparent Splat Render Pipeline";
        Step("fractal-splat");
        var fractalSplat = CreateFractalReservoirPipelineState(paths.FractalReservoir, "D3D12FractalSplatReceiptCS");
        fractalSplat.Name = "Aquarium D3D12 Fractal Splat Compute Pipeline";
        Step("fractal-sdf-reservoir");
        var fractalSdfReservoir = CreateFractalReservoirPipelineState(paths.FractalReservoir, "D3D12SdfEnvelopeReservoirCS");
        fractalSdfReservoir.Name = "Aquarium D3D12 Fractal SDF Reservoir Compute Pipeline";
        Step("fractal-pbr-reservoir");
        var fractalPbrReservoir = CreateFractalReservoirPipelineState(paths.FractalReservoir, "D3D12PbrMaterialReservoirCS");
        fractalPbrReservoir.Name = "Aquarium D3D12 Fractal PBR Reservoir Compute Pipeline";
        Step("fractal-radiosity-reservoir");
        var fractalRadiosityReservoir = CreateFractalReservoirPipelineState(paths.FractalReservoir, "D3D12RadiosityReservoirCS");
        fractalRadiosityReservoir.Name = "Aquarium D3D12 Fractal Radiosity Reservoir Compute Pipeline";
        Step("tube-field-compute");
        var tubeFieldCompute = CreateTubeFieldComputePipelineState(paths.TubeField);
        tubeFieldCompute.Name = "Aquarium D3D12 TubeField Compute Pipeline";
        Step("tube-field-render");
        var tubeFieldRender = CreateTubeFieldRenderPipelineState(paths.TubeField);
        tubeFieldRender.Name = "Aquarium D3D12 TubeField Render Pipeline";
        Step("bokusho-brush");
        var bokushoBrush = CreateBokushoBrushPipelineState(paths.BokushoBrush);
        bokushoBrush.Name = "Aquarium D3D12 Bokusho Brush Compute Pipeline";
        Step("field-reservoir-resolve");
        var fieldReservoirResolve = CreateFieldReservoirResolvePipelineState(paths.Post);
        fieldReservoirResolve.Name = "Aquarium D3D12 Field Reservoir Resolve Pipeline";
        var sdfProxies = new ID3D12PipelineState[paths.SdfShaders.Count];
        for (var index = 0; index < paths.SdfShaders.Count; index++)
        {
            Step($"sdf-proxy-{index}");
            sdfProxies[index] = CreateSdfObjectProxyPipelineState(paths.SdfShaders[index]);
            sdfProxies[index].Name = $"Aquarium D3D12 Sdf Proxy Pipeline {index}";
        }

        Step("bloom-prefilter");
        var bloomPrefilter = CreateBloomPrefilterPipelineState(paths.Post);
        bloomPrefilter.Name = "Aquarium D3D12 Bloom Prefilter Pipeline";
        Step("bloom-downsample");
        var bloomDownsample = CreateBloomDownsamplePipelineState(paths.Post);
        bloomDownsample.Name = "Aquarium D3D12 Bloom Downsample Pipeline";
        Step("bloom-blur-horizontal");
        var bloomBlurHorizontal = CreateBloomBlurHorizontalPipelineState(paths.Post);
        bloomBlurHorizontal.Name = "Aquarium D3D12 Bloom Blur Horizontal Pipeline";
        Step("bloom-blur-vertical");
        var bloomBlurVertical = CreateBloomBlurVerticalPipelineState(paths.Post);
        bloomBlurVertical.Name = "Aquarium D3D12 Bloom Blur Vertical Pipeline";
        ID3D12PipelineState? reservoirHistoryUpdate = null;
        if (Environment.GetEnvironmentVariable("AQUARIUM_DISABLE_RESERVOIR_HISTORY") == "1")
        {
            Step("reservoir-history-update disabled");
        }
        else
        {
            Step("reservoir-history-update");
            reservoirHistoryUpdate = CreateReservoirHistoryUpdatePipelineState(paths.ReservoirHistoryUpdate);
            reservoirHistoryUpdate.Name = "Aquarium D3D12 Reservoir History Update Pipeline";
        }
        Step("resolve");
        var resolve = CreateResolvePipelineState(paths.Post);
        resolve.Name = "Aquarium D3D12 Resolve Pipeline";

        return new D3D12PipelineSet(
            heightFieldBase,
            heightFieldBrush,
            scene,
            spline,
            temporalGaussian,
            gpuSensorFusion,
            stereoDepth,
            pointCloudCompute,
            pointCloudRender,
            fractalSurfaceSplatRender,
            fractalTransparentSplatRender,
            fractalSplat,
            fractalSdfReservoir,
            fractalPbrReservoir,
            fractalRadiosityReservoir,
            tubeFieldCompute,
            tubeFieldRender,
            bokushoBrush,
            fieldReservoirResolve,
            sdfProxies,
            bloomPrefilter,
            bloomDownsample,
            bloomBlurHorizontal,
            bloomBlurVertical,
            reservoirHistoryUpdate,
            resolve);
    }

    private void ResizeIfNeeded(int newWidth, int newHeight)
    {
        newWidth = Math.Max(1, newWidth);
        newHeight = Math.Max(1, newHeight);
        if (newWidth == width && newHeight == height)
        {
            return;
        }

        WaitForGpu();
        DisposeBackBufferOverlays();
        DisposeProgramOutputTexture();
        RemoveBloomRenderTargets();
        RemoveGraphRenderTargets();
        resourceRegistry.RemoveRenderTarget("scene-hdr-target");
        resourceRegistry.RemoveRenderTarget("scene-metadata-target");
        resourceRegistry.RemoveRenderTarget("scene-control-target");
        resourceRegistry.RemoveRenderTarget("scene-reservoir-guide-target");
        resourceRegistry.RemoveRenderTarget("scene-candidate-target");
        resourceRegistry.RemoveRenderTarget("scene-candidate-metadata-target");
        resourceRegistry.RemoveRenderTarget("scene-candidate-control-target");
        resourceRegistry.RemoveRenderTarget("scene-candidate-reservoir-guide-target");
        resourceRegistry.RemoveRenderTarget("scene-overdraw-target");
        resourceRegistry.RemoveRenderTarget("reservoir-resolved-target");
        resourceRegistry.RemoveResource("scene-depth-target");
        resourceRegistry.RemoveRenderTarget("height-field-target");
        DisposeBloomRenderTargets();
        DisposeGraphRenderTargets();
        sceneRenderTarget.Dispose();
        sceneMetadataRenderTarget.Dispose();
        sceneControlRenderTarget.Dispose();
        sceneReservoirGuideRenderTarget.Dispose();
        sceneCandidateRenderTarget.Dispose();
        sceneCandidateMetadataRenderTarget.Dispose();
        sceneCandidateControlRenderTarget.Dispose();
        sceneCandidateReservoirGuideRenderTarget.Dispose();
        sceneOverdrawRenderTarget.Dispose();
        reservoirResolvedRenderTarget.Dispose();
        fieldReservoirCandidateBuffer.Dispose();
        fieldReservoirLockBuffer.Dispose();
        DisposeFieldReservoirHistoryBuffers();
        sceneDepthTarget.Dispose();
        heightFieldRenderTarget.Dispose();
        for (var index = 0; index < frames.Length; index++)
        {
            resourceRegistry.RemoveResource($"backbuffer-{index}");
            frames[index].BackBuffer.Dispose();
        }

        width = newWidth;
        height = newHeight;
        RefreshReservoirWorkGrid();
        renderTargetViewArena.Dispose();
        depthStencilViewArena.Dispose();
        staticShaderDescriptorArena.Dispose();
        renderTargetViewArena = CreateRenderTargetViewArena();
        depthStencilViewArena = CreateDepthStencilViewArena();
        staticShaderDescriptorArena = CreateStaticShaderDescriptorArena();

        swapChain.ResizeBuffers(BackBufferCount, (uint)width, (uint)height, Format.B8G8R8A8_UNorm, SwapChainFlags.None).CheckError();
        frameIndex = (int)swapChain.CurrentBackBufferIndex;
        CreateRenderTargetViews();
        CreateProgramOutputTexture();
        CreateBackBufferOverlays();
        heightFieldRenderTarget = CreateHeightFieldRenderTarget();
        sceneRenderTarget = CreateSceneRenderTarget();
        sceneMetadataRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-metadata-target", "Aquarium D3D12 Scene Metadata Target");
        sceneControlRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-control-target", "Aquarium D3D12 Scene Control Target");
        sceneReservoirGuideRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-reservoir-guide-target", "Aquarium D3D12 Scene Reservoir Guide Target");
        sceneCandidateRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-target", "Aquarium D3D12 Scene Candidate Target");
        sceneCandidateMetadataRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-metadata-target", "Aquarium D3D12 Scene Candidate Metadata Target");
        sceneCandidateControlRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-control-target", "Aquarium D3D12 Scene Candidate Control Target");
        sceneCandidateReservoirGuideRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-reservoir-guide-target", "Aquarium D3D12 Scene Candidate Reservoir Guide Target");
        sceneOverdrawRenderTarget = CreateSceneOverdrawRenderTarget();
        reservoirResolvedRenderTarget = CreateReservoirResolvedRenderTarget();
        CreateFieldReservoirBuffers();
        sceneDepthStencilView = depthStencilViewArena.Allocate();
        sceneDepthTarget = CreateSceneDepthTarget(sceneDepthStencilView);
        CreateBloomRenderTargets();
        CreateGraphRenderTargets();
        viewport = new Viewport(0.0f, 0.0f, width, height);
        scissorRect = new RawRect(0, 0, width, height);
        RefreshReservoirWorkGrid();
        Console.WriteLine($"D3D12 resized: {width}x{height}; {resourceRegistry.Describe()}");
    }

    private void RefreshReservoirWorkGrid()
    {
        var (workWidth, workHeight) = ResolveReservoirWorkGrid();
        reservoirWidth = workWidth;
        reservoirHeight = workHeight;
        reservoirViewport = new Viewport(0.0f, 0.0f, reservoirWidth, reservoirHeight);
        reservoirScissorRect = new RawRect(0, 0, reservoirWidth, reservoirHeight);
    }

    private void ResizeReservoirWorkGridIfNeeded()
    {
        var (workWidth, workHeight) = ResolveReservoirWorkGrid();
        if (workWidth == reservoirWidth && workHeight == reservoirHeight)
        {
            return;
        }

        WaitForGpu();
        RemoveBloomRenderTargets();
        resourceRegistry.RemoveRenderTarget("scene-hdr-target");
        resourceRegistry.RemoveRenderTarget("scene-metadata-target");
        resourceRegistry.RemoveRenderTarget("scene-control-target");
        resourceRegistry.RemoveRenderTarget("scene-reservoir-guide-target");
        resourceRegistry.RemoveRenderTarget("scene-candidate-target");
        resourceRegistry.RemoveRenderTarget("scene-candidate-metadata-target");
        resourceRegistry.RemoveRenderTarget("scene-candidate-control-target");
        resourceRegistry.RemoveRenderTarget("scene-candidate-reservoir-guide-target");
        resourceRegistry.RemoveRenderTarget("scene-overdraw-target");
        resourceRegistry.RemoveRenderTarget("reservoir-resolved-target");
        resourceRegistry.RemoveResource("scene-depth-target");
        DisposeBloomRenderTargets();
        sceneRenderTarget.Dispose();
        sceneMetadataRenderTarget.Dispose();
        sceneControlRenderTarget.Dispose();
        sceneReservoirGuideRenderTarget.Dispose();
        sceneCandidateRenderTarget.Dispose();
        sceneCandidateMetadataRenderTarget.Dispose();
        sceneCandidateControlRenderTarget.Dispose();
        sceneCandidateReservoirGuideRenderTarget.Dispose();
        sceneOverdrawRenderTarget.Dispose();
        reservoirResolvedRenderTarget.Dispose();
        fieldReservoirCandidateBuffer.Dispose();
        fieldReservoirLockBuffer.Dispose();
        DisposeFieldReservoirHistoryBuffers();
        sceneDepthTarget.Dispose();

        reservoirWidth = workWidth;
        reservoirHeight = workHeight;
        reservoirViewport = new Viewport(0.0f, 0.0f, reservoirWidth, reservoirHeight);
        reservoirScissorRect = new RawRect(0, 0, reservoirWidth, reservoirHeight);
        sceneRenderTarget = CreateSceneRenderTarget();
        sceneMetadataRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-metadata-target", "Aquarium D3D12 Scene Metadata Target");
        sceneControlRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-control-target", "Aquarium D3D12 Scene Control Target");
        sceneReservoirGuideRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-reservoir-guide-target", "Aquarium D3D12 Scene Reservoir Guide Target");
        sceneCandidateRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-target", "Aquarium D3D12 Scene Candidate Target");
        sceneCandidateMetadataRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-metadata-target", "Aquarium D3D12 Scene Candidate Metadata Target");
        sceneCandidateControlRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-control-target", "Aquarium D3D12 Scene Candidate Control Target");
        sceneCandidateReservoirGuideRenderTarget = CreateSceneAuxiliaryRenderTarget("scene-candidate-reservoir-guide-target", "Aquarium D3D12 Scene Candidate Reservoir Guide Target");
        sceneOverdrawRenderTarget = CreateSceneOverdrawRenderTarget();
        reservoirResolvedRenderTarget = CreateReservoirResolvedRenderTarget();
        CreateFieldReservoirBuffers();
        sceneDepthTarget = CreateSceneDepthTarget(sceneDepthStencilView);
        CreateBloomRenderTargets();
    }

    private (int Width, int Height) ResolveReservoirWorkGrid()
    {
        var scale = Math.Clamp(settings.FieldReservoirScale, GraphicsSettings.MinFieldReservoirScale, GraphicsSettings.MaxFieldReservoirScale);
        return (
            Math.Max(1, (int)MathF.Ceiling(Math.Max(1, width) * scale)),
            Math.Max(1, (int)MathF.Ceiling(Math.Max(1, height) * scale)));
    }

    private D3D12RenderTarget CreateSceneRenderTarget()
    {
        var target = new D3D12RenderTarget(
            device,
            reservoirWidth,
            reservoirHeight,
            SceneHdrFormat,
            renderTargetViewArena.Allocate(),
            staticShaderDescriptorArena.Allocate(),
            null,
            false,
            new Color4(0.0f, 0.0f, 0.0f, 1.0f),
            "Aquarium D3D12 Scene HDR Target");
        resourceRegistry.Add("scene-hdr-target", target);
        return target;
    }

    private D3D12RenderTarget CreateSceneAuxiliaryRenderTarget(string registryName, string resourceName)
    {
        var target = new D3D12RenderTarget(
            device,
            reservoirWidth,
            reservoirHeight,
            SceneHdrFormat,
            renderTargetViewArena.Allocate(),
            staticShaderDescriptorArena.Allocate(),
            null,
            false,
            new Color4(0.0f, 0.0f, 0.0f, 1.0f),
            resourceName);
        resourceRegistry.Add(registryName, target);
        return target;
    }

    private D3D12RenderTarget CreateSceneOverdrawRenderTarget()
    {
        var target = new D3D12RenderTarget(
            device,
            reservoirWidth,
            reservoirHeight,
            SceneOverdrawFormat,
            renderTargetViewArena.Allocate(),
            staticShaderDescriptorArena.Allocate(),
            null,
            false,
            new Color4(0.0f, 0.0f, 0.0f, 1.0f),
            "Aquarium D3D12 Scene Overdraw Target");
        resourceRegistry.Add("scene-overdraw-target", target);
        return target;
    }

    private D3D12RenderTarget CreateReservoirResolvedRenderTarget()
    {
        var target = new D3D12RenderTarget(
            device,
            reservoirWidth,
            reservoirHeight,
            SceneHdrFormat,
            renderTargetViewArena.Allocate(),
            null,
            null,
            true,
            new Color4(0.0f, 0.0f, 0.0f, 1.0f),
            "Aquarium D3D12 Reservoir Resolved Target");
        resourceRegistry.Add("reservoir-resolved-target", target);
        return target;
    }

    private void CreateFieldReservoirBuffers()
    {
        fieldReservoirCandidateBuffer = CreateFieldReservoirCandidateBuffer();
        fieldReservoirLockBuffer = CreateFieldReservoirLockBuffer();
        var historyElementCount = FieldReservoirElementCount();
        for (var index = 0; index < fieldReservoirHistoryBuffers.Length; index++)
        {
            fieldReservoirHistoryBuffers[index] = new D3D12StructuredBuffer(
                device,
                historyElementCount,
                FieldReservoirHistoryStrideBytes,
                $"Aquarium D3D12 Field Reservoir Structured History {index}",
                allowUnorderedAccess: true);
        }

        fieldReservoirCandidateBuffer.CreateUnorderedAccessView(device, fieldReservoirCandidateCpuUnorderedAccessDescriptor);
        fieldReservoirLockBuffer.CreateUnorderedAccessView(device, fieldReservoirLockCpuUnorderedAccessDescriptor);
    }

    private int FieldReservoirElementCount()
    {
        return checked(Math.Max(1, reservoirWidth) * Math.Max(1, reservoirHeight) * FieldReservoirSlotsPerPixel);
    }

    private D3D12StructuredBuffer CreateFieldReservoirCandidateBuffer()
    {
        var elementCount = FieldReservoirElementCount();
        return new D3D12StructuredBuffer(
            device,
            elementCount,
            FieldReservoirCandidateStrideBytes,
            "Aquarium D3D12 Field Reservoir Candidate Buffer",
            allowUnorderedAccess: true);
    }

    private D3D12StructuredBuffer CreateFieldReservoirLockBuffer()
    {
        var elementCount = checked(Math.Max(1, reservoirWidth) * Math.Max(1, reservoirHeight));
        return new D3D12StructuredBuffer(
            device,
            elementCount,
            sizeof(uint),
            "Aquarium D3D12 Field Reservoir Lock Buffer",
            allowUnorderedAccess: true);
    }

    private D3D12TrackedResource CreateSceneDepthTarget(D3D12DescriptorSlot depthStencilView)
    {
        var clearValue = new ClearValue
        {
            Format = SceneDepthFormat,
            DepthStencil = new DepthStencilValue(1.0f, 0),
        };
        var resource = device.CreateCommittedResource(
            HeapType.Default,
            ResourceDescription.Texture2D(
                SceneDepthFormat,
                (uint)width,
                (uint)height,
                1,
                1,
                1,
                0,
                Vortice.Direct3D12.ResourceFlags.AllowDepthStencil),
            ResourceStates.DepthWrite,
            clearValue);
        device.CreateDepthStencilView(resource, new DepthStencilViewDescription
        {
            Format = SceneDepthFormat,
            ViewDimension = DepthStencilViewDimension.Texture2D,
            Texture2D = new Texture2DDepthStencilView(),
        }, depthStencilView.Cpu);
        var target = new D3D12TrackedResource(resource, ResourceStates.DepthWrite, "Aquarium D3D12 Scene Depth Target", true);
        resourceRegistry.Add("scene-depth-target", target);
        return target;
    }

    private void CreateBloomRenderTargets()
    {
        for (var level = 0; level < BloomLevelCount; level++)
        {
            bloomRenderTargets[level] = CreateBloomRenderTarget(level, false);
            bloomScratchTargets[level] = CreateBloomRenderTarget(level, true);
        }
    }

    private void DisposeFieldReservoirHistoryBuffers()
    {
        for (var index = 0; index < fieldReservoirHistoryBuffers.Length; index++)
        {
            fieldReservoirHistoryBuffers[index]?.Dispose();
            fieldReservoirHistoryBuffers[index] = null!;
        }
    }

    private D3D12RenderTarget CreateBloomRenderTarget(int level, bool scratch)
    {
        var target = new D3D12RenderTarget(
            device,
            Math.Max(1, reservoirWidth >> (level + 1)),
            Math.Max(1, reservoirHeight >> (level + 1)),
            SceneHdrFormat,
            renderTargetViewArena.Allocate(),
            staticShaderDescriptorArena.Allocate(),
            null,
            false,
            new Color4(0.0f, 0.0f, 0.0f, 1.0f),
            scratch
                ? $"Aquarium D3D12 Bloom Scratch L{level}"
                : $"Aquarium D3D12 Bloom L{level}");
        resourceRegistry.Add(scratch ? $"bloom-scratch-{level}" : $"bloom-{level}", target);
        return target;
    }

    private void RemoveBloomRenderTargets()
    {
        for (var level = 0; level < BloomLevelCount; level++)
        {
            resourceRegistry.RemoveRenderTarget($"bloom-{level}");
            resourceRegistry.RemoveRenderTarget($"bloom-scratch-{level}");
        }
    }

    private void DisposeBloomRenderTargets()
    {
        for (var level = 0; level < BloomLevelCount; level++)
        {
            bloomRenderTargets[level]?.Dispose();
            bloomScratchTargets[level]?.Dispose();
        }
    }

    private D3D12RenderTarget CreateHeightFieldRenderTarget()
    {
        var target = new D3D12RenderTarget(
            device,
            HeightFieldTextureSize,
            HeightFieldTextureSize,
            HeightFieldFormat,
            renderTargetViewArena.Allocate(),
            staticShaderDescriptorArena.Allocate(),
            null,
            false,
            new Color4(0.0f, 0.0f, 0.0f, 1.0f),
            "Aquarium D3D12 Height Field Target");
        resourceRegistry.Add("height-field-target", target);
        return target;
    }

    private void CreateGraphRenderTargets()
    {
        foreach (var targetDescription in renderGraph.RenderTargets)
        {
            if (IsLegacyGraphTarget(targetDescription.Handle.Name) || graphRenderTargets.ContainsKey(targetDescription.Handle.Name))
            {
                continue;
            }

            var targetSize = ResolveGraphTargetSize(targetDescription.Size);
            var target = new D3D12RenderTarget(
                device,
                targetSize.Width,
                targetSize.Height,
                ToDxgiFormat(targetDescription.Format),
                renderTargetViewArena.Allocate(),
                null,
                null,
                allowUnorderedAccess: false,
                new Color4(0.0f, 0.0f, 0.0f, 1.0f),
                $"Aquarium D3D12 Graph Target {targetDescription.Handle.Name}");
            graphRenderTargets.Add(targetDescription.Handle.Name, target);
            resourceRegistry.Add($"graph-target:{targetDescription.Handle.Name}", target);
        }
    }

    private void RemoveGraphRenderTargets()
    {
        foreach (var name in graphRenderTargets.Keys)
        {
            resourceRegistry.RemoveRenderTarget($"graph-target:{name}");
        }
    }

    private void DisposeGraphRenderTargets()
    {
        foreach (var target in graphRenderTargets.Values)
        {
            target.Dispose();
        }

        graphRenderTargets.Clear();
    }

    private void EnsureFractalReservoirBuffers(AquariumFractalReservoirField field)
    {
        if (!field.HasInput)
        {
            return;
        }

        if (fractalSplatBuffer is not null && fractalFlameStateBuffer is not null && fractalSplatBuffer.ElementCount >= field.SplatCount && fractalFlameStateBuffer.ElementCount >= field.SplatCount)
        {
            return;
        }

        WaitForGpu();
        DisposeFractalReservoirBuffers();
        fractalSplatBuffer = CreateFractalReservoirBuffer<AquariumPackedFractalSdfSplat3D>(
            field.SplatCount,
            "fractal-sdf-splat-buffer",
            "Aquarium D3D12 Fractal SDF Splat Buffer");
        fractalSdfReservoirBuffer = CreateFractalReservoirBuffer<AquariumPackedSdfEnvelopeReservoir>(
            field.SplatCount,
            "fractal-sdf-envelope-reservoir-buffer",
            "Aquarium D3D12 Fractal SDF Envelope Reservoir Buffer");
        fractalPbrReservoirBuffer = CreateFractalReservoirBuffer<AquariumPackedPbrMaterialReservoir>(
            field.SplatCount,
            "fractal-pbr-material-reservoir-buffer",
            "Aquarium D3D12 Fractal PBR Material Reservoir Buffer");
        fractalRadiosityReservoirBuffer = CreateFractalReservoirBuffer<AquariumPackedRadiosityReservoir>(
            field.SplatCount,
            "fractal-radiosity-reservoir-buffer",
            "Aquarium D3D12 Fractal Radiosity Reservoir Buffer");
        fractalFlameStateBuffer = CreateFractalReservoirBuffer<AquariumPackedFractalFlameState>(
            field.SplatCount,
            "fractal-flame-state-buffer",
            "Aquarium D3D12 Fractal Flame Iteration State Buffer");
    }

    private D3D12StructuredBuffer CreateFractalReservoirBuffer<T>(int elementCount, string registryName, string resourceName)
        where T : unmanaged
    {
        var buffer = new D3D12StructuredBuffer(
            device,
            elementCount,
            Marshal.SizeOf<T>(),
            resourceName,
            allowUnorderedAccess: true);
        resourceRegistry.Add(registryName, buffer);
        return buffer;
    }

    private void EnsureFractalProgramTransformBuffer()
    {
        if (!activeFractalReservoirField.HasInput)
        {
            return;
        }

        var requiredCount = Math.Max(activeFractalProgramTransforms.Length, 1);
        if (fractalProgramTransformBuffer is not null && fractalProgramTransformBuffer.ElementCount >= requiredCount)
        {
            return;
        }

        WaitForGpu();
        resourceRegistry.RemoveStructuredBuffer("fractal-program-transform-buffer");
        fractalProgramTransformBuffer?.Dispose();
        fractalProgramTransformBuffer = new D3D12StructuredBuffer(
            device,
            requiredCount,
            Marshal.SizeOf<AquariumPackedFractalIfsTransform>(),
            "Aquarium D3D12 Fractal IFS Program Transform Buffer");
        resourceRegistry.Add("fractal-program-transform-buffer", fractalProgramTransformBuffer);
    }

    private void EnsureBufferFieldProgramBuffers()
    {
        if (!activeFractalReservoirField.HasInput || !activeBufferFieldFrame.HasInput)
        {
            return;
        }

        var requiredProgramCount = Math.Max(activeTextureSplinePrograms.Length, 1);
        if (bufferFieldTextureSplineProgramBuffer is null || bufferFieldTextureSplineProgramBuffer.ElementCount < requiredProgramCount)
        {
            WaitForGpu();
            resourceRegistry.RemoveStructuredBuffer("buffer-field-texture-spline-program-buffer");
            bufferFieldTextureSplineProgramBuffer?.Dispose();
            bufferFieldTextureSplineProgramBuffer = new D3D12StructuredBuffer(
                device,
                requiredProgramCount,
                Marshal.SizeOf<AquariumPackedTextureSplineFieldProgram>(),
                "Aquarium D3D12 Buffer Field Texture Spline Program Buffer");
            resourceRegistry.Add("buffer-field-texture-spline-program-buffer", bufferFieldTextureSplineProgramBuffer);
        }

        var requiredSampleCount = Math.Max(activeTextureFieldSamples.Length, 1);
        if (bufferFieldTextureSampleBuffer is null || bufferFieldTextureSampleBuffer.ElementCount < requiredSampleCount)
        {
            WaitForGpu();
            resourceRegistry.RemoveStructuredBuffer("buffer-field-texture-sample-buffer");
            bufferFieldTextureSampleBuffer?.Dispose();
            bufferFieldTextureSampleBuffer = new D3D12StructuredBuffer(
                device,
                requiredSampleCount,
                Marshal.SizeOf<float>(),
                "Aquarium D3D12 Buffer Field Texture Sample Buffer");
            resourceRegistry.Add("buffer-field-texture-sample-buffer", bufferFieldTextureSampleBuffer);
        }
    }

    private void DisposeFractalReservoirBuffers()
    {
        resourceRegistry.RemoveStructuredBuffer("buffer-field-texture-sample-buffer");
        resourceRegistry.RemoveStructuredBuffer("buffer-field-texture-spline-program-buffer");
        resourceRegistry.RemoveStructuredBuffer("fractal-program-transform-buffer");
        resourceRegistry.RemoveStructuredBuffer("fractal-flame-state-buffer");
        resourceRegistry.RemoveStructuredBuffer("fractal-radiosity-reservoir-buffer");
        resourceRegistry.RemoveStructuredBuffer("fractal-pbr-material-reservoir-buffer");
        resourceRegistry.RemoveStructuredBuffer("fractal-sdf-envelope-reservoir-buffer");
        resourceRegistry.RemoveStructuredBuffer("fractal-sdf-splat-buffer");
        fractalProgramTransformBuffer?.Dispose();
        fractalFlameStateBuffer?.Dispose();
        fractalRadiosityReservoirBuffer?.Dispose();
        fractalPbrReservoirBuffer?.Dispose();
        fractalSdfReservoirBuffer?.Dispose();
        fractalSplatBuffer?.Dispose();
        bufferFieldTextureSampleBuffer?.Dispose();
        bufferFieldTextureSplineProgramBuffer?.Dispose();
        fractalProgramTransformBuffer = null;
        fractalFlameStateBuffer = null;
        fractalRadiosityReservoirBuffer = null;
        fractalPbrReservoirBuffer = null;
        fractalSdfReservoirBuffer = null;
        fractalSplatBuffer = null;
        bufferFieldTextureSampleBuffer = null;
        bufferFieldTextureSplineProgramBuffer = null;
    }

    private (int Width, int Height) ResolveGraphTargetSize(AquariumTargetSize size)
    {
        return size.Kind switch
        {
            AquariumTargetSizeKind.Fixed => (Math.Max(1, size.Width), Math.Max(1, size.Height)),
            AquariumTargetSizeKind.MatchWindow => (
                Math.Max(1, (int)MathF.Ceiling(reservoirWidth * MathF.Max(size.Scale, 0.001f))),
                Math.Max(1, (int)MathF.Ceiling(reservoirHeight * MathF.Max(size.Scale, 0.001f)))),
            _ => throw new ArgumentOutOfRangeException(nameof(size), size.Kind, "Unsupported render target size policy."),
        };
    }

    private static Format ToDxgiFormat(RenderFormat format)
    {
        return format switch
        {
            RenderFormat.R16Float => Format.R16_Float,
            RenderFormat.Rgba16Float => Format.R16G16B16A16_Float,
            RenderFormat.Bgra8Unorm => Format.B8G8R8A8_UNorm,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported render target format."),
        };
    }

    private static bool IsLegacyGraphTarget(string name)
    {
        return string.Equals(name, "height-field", StringComparison.Ordinal)
            || string.Equals(name, "scene", StringComparison.Ordinal)
            || string.Equals(name, "scene-metadata", StringComparison.Ordinal)
            || string.Equals(name, "scene-control", StringComparison.Ordinal);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private void RenderSceneAndPresent(D3D12PassContext context, FrameResources frameResources)
    {
        context.CommandList.BeginEvent("Scene Candidate Pass");
        BeginGpuTiming(context.CommandList, frameResources, D3D12GpuTimingPass.SceneCandidate);
        try
        {
            heightFieldRenderTarget.Transition(context.CommandList, ResourceStates.PixelShaderResource);
            sceneCandidateRenderTarget.Transition(context.CommandList, ResourceStates.RenderTarget);
            sceneCandidateMetadataRenderTarget.Transition(context.CommandList, ResourceStates.RenderTarget);
            sceneCandidateControlRenderTarget.Transition(context.CommandList, ResourceStates.RenderTarget);
            sceneCandidateReservoirGuideRenderTarget.Transition(context.CommandList, ResourceStates.RenderTarget);
            sceneOverdrawRenderTarget.Transition(context.CommandList, ResourceStates.RenderTarget);
            sceneDepthTarget.Transition(context.CommandList, ResourceStates.DepthWrite);
            context.CommandList.ClearRenderTargetView(sceneCandidateRenderTarget.RenderTargetView.Cpu, new Color4(0.0f, 0.0f, 0.0f, 1.0f));
            context.CommandList.ClearRenderTargetView(sceneCandidateMetadataRenderTarget.RenderTargetView.Cpu, new Color4(0.0f, 0.0f, 0.0f, 1.0f));
            context.CommandList.ClearRenderTargetView(sceneCandidateControlRenderTarget.RenderTargetView.Cpu, new Color4(0.0f, 0.0f, 0.0f, 1.0f));
            context.CommandList.ClearRenderTargetView(sceneCandidateReservoirGuideRenderTarget.RenderTargetView.Cpu, new Color4(1.0f, 0.0f, 1.0f, 0.0f));
            context.CommandList.ClearRenderTargetView(sceneOverdrawRenderTarget.RenderTargetView.Cpu, new Color4(0.0f, 0.0f, 0.0f, 1.0f));
            context.CommandList.ClearDepthStencilView(sceneDepthStencilView.Cpu, ClearFlags.Depth, 1.0f, 0);
            context.CommandList.SetDescriptorHeaps(frameResources.TransientShaderDescriptors.Heap);
            ClearFieldReservoir(context.CommandList, frameResources);
            context.CommandList.SetPipelineState(scenePipelineState!);
            context.CommandList.SetGraphicsRootSignature(fullscreenRootSignature);
            context.CommandList.SetGraphicsRootDescriptorTable(RootFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootSourceTexture, frameResources.HeightFieldDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootHeightFieldBrushes, frameResources.HeightFieldBrushConstantsDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootSdfLights, frameResources.SdfLightDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootStudioPmrem, frameResources.StudioPmremDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootStudioIrradiance, frameResources.StudioIrradianceDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootSdfObjects, frameResources.SdfObjectDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootTemporalGaussians, frameResources.TemporalGaussianDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootBlueNoise, frameResources.BlueNoiseDescriptor.Gpu);
            context.CommandList.RSSetViewports(reservoirViewport);
            context.CommandList.RSSetScissorRects(reservoirScissorRect);
            context.CommandList.OMSetRenderTargets(
            [
                sceneCandidateRenderTarget.RenderTargetView.Cpu,
                sceneCandidateMetadataRenderTarget.RenderTargetView.Cpu,
                sceneCandidateControlRenderTarget.RenderTargetView.Cpu,
                sceneCandidateReservoirGuideRenderTarget.RenderTargetView.Cpu,
                sceneOverdrawRenderTarget.RenderTargetView.Cpu,
            ],
            sceneDepthStencilView.Cpu);
            context.CommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.CommandList.DrawInstanced(3, 1, 0, 0);

            for (var sdfIndex = 0; sdfIndex < sdfProxyPipelineStates.Length; sdfIndex++)
            {
                context.CommandList.SetPipelineState(sdfProxyPipelineStates[sdfIndex]!);
                context.CommandList.DrawInstanced(6, 1, 0, 0);
            }

            if (temporalGaussianCount > 0)
            {
                context.CommandList.SetPipelineState(temporalGaussianPipelineState!);
                context.CommandList.DrawInstanced(6, (uint)temporalGaussianCount, 0, 0);
            }

            if (visibleFractalSplatCount > 0 && fractalSplatBuffer is not null && fractalSdfReservoirBuffer is not null && fractalPbrReservoirBuffer is not null && fractalRadiosityReservoirBuffer is not null)
            {
                fractalSplatBuffer.Transition(context.CommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                fractalSdfReservoirBuffer.Transition(context.CommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                fractalPbrReservoirBuffer.Transition(context.CommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                fractalRadiosityReservoirBuffer.Transition(context.CommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                context.CommandList.SetGraphicsRootShaderResourceView(RootFractalSplatSrv, fractalSplatBuffer.Resource.GPUVirtualAddress);
                context.CommandList.SetGraphicsRootShaderResourceView(RootFractalSdfReservoirSrv, fractalSdfReservoirBuffer.Resource.GPUVirtualAddress);
                context.CommandList.SetGraphicsRootShaderResourceView(RootFractalPbrReservoirSrv, fractalPbrReservoirBuffer.Resource.GPUVirtualAddress);
                context.CommandList.SetGraphicsRootShaderResourceView(RootFractalRadiosityReservoirSrv, fractalRadiosityReservoirBuffer.Resource.GPUVirtualAddress);
                context.CommandList.SetPipelineState(fractalSurfaceSplatRenderPipelineState!);
                context.CommandList.DrawInstanced(6, (uint)visibleFractalSplatCount, 0, 0);
                context.CommandList.SetPipelineState(fractalTransparentSplatRenderPipelineState!);
                context.CommandList.DrawInstanced(6, (uint)visibleFractalSplatCount, 0, 0);
            }

            RenderPointCloudMeshes(context.CommandList, frameResources);
            RenderTubeFields(context.CommandList, frameResources);
            RenderSplineSurfaceClaims(context.CommandList, frameResources);
        }
        finally
        {
            EndGpuTiming(context.CommandList, frameResources, D3D12GpuTimingPass.SceneCandidate);
            context.CommandList.EndEvent();
        }

        ResolveFieldReservoir(context.CommandList, frameResources);
        var historyUpdated = ShouldUseReservoirHistoryPresentation()
            && UpdateReservoirHistory(context.CommandList, frameResources);
        var presentationSourceTarget = historyUpdated ? reservoirResolvedRenderTarget : sceneRenderTarget;
        var presentationSourceDescriptor = historyUpdated ? frameResources.ReservoirResolvedDescriptor : frameResources.SceneDescriptor;
        RenderBloom(context.CommandList, frameResources, presentationSourceTarget, presentationSourceDescriptor);
        PresentBackBuffer(context, frameResources, presentationSourceTarget, presentationSourceDescriptor);
    }

    private bool ShouldUseReservoirHistoryPresentation()
    {
        return activeFractalReservoirField.HasInput ||
            activeBufferFieldFrame.UseReservoirLowering ||
            activeTubeFieldDrawIndexCount > 0 ||
            temporalGaussianCount > 0 ||
            gpuFusionSeedCount > 0 ||
            gpuFusionPointCount > 0;
    }

    private void RenderSplineSurfaceClaims(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        if (!activeSplineFrame.HasInput || splinePipelineState is null)
        {
            return;
        }

        var vertices = new List<D3D12SplineVertex>(Math.Min(
            262144,
            activeSplineFrame.Splines.Sum(spline => Math.Max(0, spline.Vertices.Count - 1) * Math.Max(1, spline.CatmullRomSubdivisions) * 6)));

        foreach (var spline in activeSplineFrame.Splines)
        {
            if (spline.Vertices.Count < 2)
            {
                continue;
            }

            AppendSplineSurfaceEnvelopeGeometry(vertices, spline);
        }

        if (vertices.Count == 0)
        {
            return;
        }

        var upload = frameResources.UploadRing.WriteArray(CollectionsMarshal.AsSpan(vertices));
        var view = new VertexBufferView(upload.GpuVirtualAddress, (uint)upload.DataBytes, (uint)Marshal.SizeOf<D3D12SplineVertex>());
        activeCommandList.SetPipelineState(splinePipelineState);
        activeCommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        activeCommandList.IASetVertexBuffers(0, [view]);
        activeCommandList.DrawInstanced((uint)vertices.Count, 1, 0, 0);
    }

    private unsafe void ClearFieldReservoir(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        var clearValues = new Int4(0, 0, 0, 0);
        fieldReservoirCandidateBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        fieldReservoirLockBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        activeCommandList.ClearUnorderedAccessViewUint(
            frameResources.FieldReservoirCandidateUnorderedAccessDescriptor.Gpu,
            fieldReservoirCandidateCpuUnorderedAccessDescriptor.Cpu,
            fieldReservoirCandidateBuffer.Resource,
            clearValues,
            []);
        activeCommandList.ClearUnorderedAccessViewUint(
            frameResources.FieldReservoirLockUnorderedAccessDescriptor.Gpu,
            fieldReservoirLockCpuUnorderedAccessDescriptor.Cpu,
            fieldReservoirLockBuffer.Resource,
            clearValues,
            []);
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fieldReservoirCandidateBuffer.Resource));
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fieldReservoirLockBuffer.Resource));
    }

    private void ResolveFieldReservoir(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        if (fieldReservoirResolvePipelineState is null)
        {
            return;
        }

        activeCommandList.BeginEvent("Field Reservoir Resolve");
        BeginGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.FieldReservoirResolve);
        try
        {
            sceneCandidateRenderTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource);
            sceneCandidateMetadataRenderTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource);
            sceneCandidateControlRenderTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource);
            sceneCandidateReservoirGuideRenderTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource);
            fieldReservoirCandidateBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            sceneRenderTarget.Transition(activeCommandList, ResourceStates.RenderTarget);
            sceneMetadataRenderTarget.Transition(activeCommandList, ResourceStates.RenderTarget);
            sceneControlRenderTarget.Transition(activeCommandList, ResourceStates.RenderTarget);
            sceneReservoirGuideRenderTarget.Transition(activeCommandList, ResourceStates.RenderTarget);
            activeCommandList.SetPipelineState(fieldReservoirResolvePipelineState);
            activeCommandList.SetGraphicsRootSignature(fullscreenRootSignature);
            activeCommandList.SetGraphicsRootDescriptorTable(RootFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
            activeCommandList.SetGraphicsRootDescriptorTable(RootSourceTexture, frameResources.SceneCandidateDescriptor.Gpu);
            activeCommandList.SetGraphicsRootDescriptorTable(RootCurrentSceneMetadata, frameResources.SceneCandidateMetadataDescriptor.Gpu);
            activeCommandList.SetGraphicsRootDescriptorTable(RootCurrentSceneControl, frameResources.SceneCandidateControlDescriptor.Gpu);
            activeCommandList.SetGraphicsRootDescriptorTable(RootCurrentReservoirGuide, frameResources.SceneCandidateReservoirGuideDescriptor.Gpu);
            activeCommandList.SetGraphicsRootShaderResourceView(RootFieldReservoirCandidates, fieldReservoirCandidateBuffer.Resource.GPUVirtualAddress);
            activeCommandList.RSSetViewports(reservoirViewport);
            activeCommandList.RSSetScissorRects(reservoirScissorRect);
            activeCommandList.OMSetRenderTargets(
            [
                sceneRenderTarget.RenderTargetView.Cpu,
                sceneMetadataRenderTarget.RenderTargetView.Cpu,
                sceneControlRenderTarget.RenderTargetView.Cpu,
                sceneReservoirGuideRenderTarget.RenderTargetView.Cpu,
            ],
            null);
            activeCommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            activeCommandList.DrawInstanced(3, 1, 0, 0);
        }
        finally
        {
            EndGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.FieldReservoirResolve);
            activeCommandList.EndEvent();
        }
    }

    private bool UpdateReservoirHistory(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        if (reservoirHistoryUpdatePipelineState is null)
        {
            return false;
        }

        activeCommandList.BeginEvent("Reservoir History Update");
        BeginGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.ReservoirHistoryUpdate);
        try
        {
            var historyReadIndex = temporalFrameIndex & 1;
            var historyWriteIndex = 1 - historyReadIndex;
            var historyReadBuffer = fieldReservoirHistoryBuffers[historyReadIndex];
            var historyWriteBuffer = fieldReservoirHistoryBuffers[historyWriteIndex];

            sceneRenderTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            sceneMetadataRenderTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            sceneControlRenderTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            sceneReservoirGuideRenderTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            fieldReservoirCandidateBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            historyReadBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            historyWriteBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
            reservoirResolvedRenderTarget.Transition(activeCommandList, ResourceStates.UnorderedAccess);
            var replayManifest = new D3D12TubeFieldReplayManifestEntry[MaxTubeFieldReplaySources];
            Array.Fill(replayManifest, D3D12TubeFieldReplayManifestEntry.Disabled);
            var replaySourceCount = Math.Min(tubeFieldDrawBatches.Count, MaxTubeFieldReplaySources);
            for (var index = 0; index < replaySourceCount; index++)
            {
                var batch = tubeFieldDrawBatches[index];
                replayManifest[index] = D3D12TubeFieldReplayManifestEntry.FromConstants(batch.Constants, index);
                batch.Source.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            }

            tubeFieldStatsBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            tubeFieldReplayManifestBuffer.Upload(activeCommandList, frameResources.UploadRing, replayManifest);

            activeCommandList.SetDescriptorHeaps(frameResources.TransientShaderDescriptors.Heap);
            var replaySourceDescriptors = frameResources.TransientShaderDescriptors.AllocateRange(MaxTubeFieldReplaySources);
            for (var index = 0; index < MaxTubeFieldReplaySources; index++)
            {
                var descriptor = frameResources.TransientShaderDescriptors.Offset(replaySourceDescriptors, index);
                var source = index < replaySourceCount ? tubeFieldDrawBatches[index].Source : tubeFieldStatsBuffer;
                source.CreateRawShaderResourceView(device, descriptor);
            }

            activeCommandList.SetPipelineState(reservoirHistoryUpdatePipelineState);
            activeCommandList.SetComputeRootSignature(fullscreenRootSignature);
            activeCommandList.SetComputeRootDescriptorTable(RootFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootSourceTexture, frameResources.SceneDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootCurrentSceneMetadata, frameResources.SceneMetadataDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootCurrentSceneControl, frameResources.SceneControlDescriptor.Gpu);
            activeCommandList.SetComputeRootShaderResourceView(RootReservoirHistoryRead, historyReadBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootUnorderedAccessView(RootReservoirHistoryWrite, historyWriteBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootDescriptorTable(RootCurrentReservoirGuide, frameResources.SceneReservoirGuideDescriptor.Gpu);
            activeCommandList.SetComputeRootShaderResourceView(RootFieldReservoirCandidates, fieldReservoirCandidateBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootDescriptorTable(RootReservoirResolvedOutput, frameResources.ReservoirResolvedUnorderedAccessDescriptor.Gpu);
            activeCommandList.SetComputeRootShaderResourceView(RootTubeFieldReplayManifest, tubeFieldReplayManifestBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootDescriptorTable(RootTubeFieldReplaySources, replaySourceDescriptors.Gpu);
            activeCommandList.Dispatch((uint)((reservoirWidth + 7) / 8), (uint)((reservoirHeight + 7) / 8), 1);
            activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(historyWriteBuffer.Resource));
            activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(reservoirResolvedRenderTarget.Resource));
        }
        finally
        {
            EndGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.ReservoirHistoryUpdate);
            activeCommandList.EndEvent();
        }

        return true;
    }

    private void RenderTubeFields(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        if (activeTubeFieldDrawIndexCount <= 0 || tubeFieldRenderPipelineState is null || tubeFieldDrawBatches.Count == 0)
        {
            return;
        }

        var generatedMesh = new D3D12PipelinePrivateGeneratedMesh(
            tubeFieldVertexBuffer,
            tubeFieldIndexBuffer,
            tubeFieldDrawArgumentBuffer,
            new VertexBufferView(
                tubeFieldVertexBuffer.Resource.GPUVirtualAddress,
                (uint)(MaxTubeFieldVertices * Marshal.SizeOf<D3D12TubeFieldVertex>()),
                (uint)Marshal.SizeOf<D3D12TubeFieldVertex>()),
            new IndexBufferView(
                tubeFieldIndexBuffer.Resource.GPUVirtualAddress,
                (uint)(MaxTubeFieldIndices * Marshal.SizeOf<uint>()),
                Format.R32_UInt),
            PrimitiveTopology.TriangleList);
        activeCommandList.SetPipelineState(tubeFieldRenderPipelineState);
        activeCommandList.SetGraphicsRootSignature(tubeFieldRenderRootSignature);
        activeCommandList.SetGraphicsRootDescriptorTable(RootTubeFieldRenderFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
        activeCommandList.SetGraphicsRootDescriptorTable(RootTubeFieldRenderBlueNoise, frameResources.BlueNoiseDescriptor.Gpu);
        fieldReservoirCandidateBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        fieldReservoirLockBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        activeCommandList.SetGraphicsRootUnorderedAccessView(RootTubeFieldRenderReservoirCandidates, fieldReservoirCandidateBuffer.Resource.GPUVirtualAddress);
        activeCommandList.SetGraphicsRootUnorderedAccessView(RootTubeFieldRenderReservoirLocks, fieldReservoirLockBuffer.Resource.GPUVirtualAddress);
        BindPipelinePrivateGeneratedMesh(activeCommandList, generatedMesh);
        foreach (var batch in tubeFieldDrawBatches)
        {
            batch.Source.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            var ramp = ResolveTubeFieldRamp(activeCommandList, batch.RampResourceKey);
            ramp.Transition(activeCommandList, ResourceStates.PixelShaderResource);
            var rampDescriptor = frameResources.TransientShaderDescriptors.Allocate();
            ramp.CreateShaderResourceView(device, rampDescriptor);
            activeCommandList.SetGraphicsRootConstantBufferView(RootTubeFieldRenderConstants, batch.ConstantsGpuVirtualAddress);
            activeCommandList.SetGraphicsRootShaderResourceView(RootTubeFieldRenderSource, batch.Source.Resource.GPUVirtualAddress);
            activeCommandList.SetGraphicsRootDescriptorTable(RootTubeFieldRenderRamp, rampDescriptor.Gpu);
            DrawPipelinePrivateGeneratedMesh(activeCommandList, generatedMesh, batch.DrawArgumentOffsetBytes);
        }

        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fieldReservoirCandidateBuffer.Resource));
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fieldReservoirLockBuffer.Resource));
    }

    private void RenderPointCloudMeshes(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        activePointCloudRenderedMeshes = 0;
        if (pointCloudRenderPipelineState is null ||
            Environment.GetEnvironmentVariable("AQUARIUM_RENDER_POINT_CLOUD_DIAG") != "1")
        {
            return;
        }

        var plannedMeshes = PlannedMeshPayloadsByClaim();
        if (plannedMeshes.Count == 0)
        {
            return;
        }

        activeCommandList.BeginEvent("Point Cloud Render");
        try
        {
            activeCommandList.SetPipelineState(pointCloudRenderPipelineState);
            activeCommandList.SetGraphicsRootSignature(fullscreenRootSignature);
            activeCommandList.SetGraphicsRootDescriptorTable(RootFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
            foreach (var packet in activeFieldLoweringPlan.Packets)
            {
                if (packet.Backend != AquariumFieldBackendKind.Mesh ||
                    packet.Encoding != AquariumFieldEncoding.Mesh ||
                    !plannedMeshes.TryGetValue(packet.ClaimKey, out var resourceKey))
                {
                    continue;
                }

                if (!fieldResourceRegistry.TryGetMesh(resourceKey, out var mesh) ||
                    mesh.Topology != AquariumFieldMeshTopology.PointList ||
                    !mesh.HasStandardImportedLayout ||
                    !mesh.IsGeneratedFromResource)
                {
                    continue;
                }

                mesh.Vertices.Transition(activeCommandList, ResourceStates.VertexAndConstantBuffer);
                mesh.Indices.Transition(activeCommandList, ResourceStates.IndexBuffer);
                activeCommandList.IASetPrimitiveTopology(mesh.D3DTopology);
                activeCommandList.IASetVertexBuffers(0, [mesh.VertexBufferView]);
                activeCommandList.IASetIndexBuffer(mesh.IndexBufferView);
                activeCommandList.DrawIndexedInstanced((uint)mesh.Indices.ElementCount, 1, 0, 0, 0);
                activePointCloudRenderedMeshes++;
            }
        }
        finally
        {
            activeCommandList.EndEvent();
        }
    }

    private void BindPipelinePrivateGeneratedMesh(
        ID3D12GraphicsCommandList activeCommandList,
        D3D12PipelinePrivateGeneratedMesh generatedMesh)
    {
        generatedMesh.Vertices.Transition(activeCommandList, ResourceStates.VertexAndConstantBuffer);
        generatedMesh.Indices.Transition(activeCommandList, ResourceStates.IndexBuffer);
        generatedMesh.DrawArguments.Transition(activeCommandList, ResourceStates.IndirectArgument);
        activeCommandList.IASetPrimitiveTopology(generatedMesh.Topology);
        activeCommandList.IASetVertexBuffers(0, [generatedMesh.VertexView]);
        activeCommandList.IASetIndexBuffer(generatedMesh.IndexView);
    }

    private void DrawPipelinePrivateGeneratedMesh(
        ID3D12GraphicsCommandList activeCommandList,
        D3D12PipelinePrivateGeneratedMesh generatedMesh,
        int drawArgumentOffsetBytes)
    {
        activeCommandList.ExecuteIndirect(
            generatedMeshDrawCommandSignature,
            1,
            generatedMesh.DrawArguments.Resource,
            (ulong)drawArgumentOffsetBytes,
            null,
            0);
    }

    private D3D12FieldTexture2D ResolveTubeFieldRamp(ID3D12GraphicsCommandList activeCommandList, string rampResourceKey)
    {
        if (!string.IsNullOrWhiteSpace(rampResourceKey) &&
            fieldResourceRegistry.TryGetTexture2D(rampResourceKey, out var resolvedRamp))
        {
            return resolvedRamp;
        }

        if (!string.IsNullOrWhiteSpace(rampResourceKey) &&
            TryFindActiveFieldResource(rampResourceKey, out var declaration) &&
            fieldResourceRegistry.TryResolveTexture2D(device, activeCommandList, declaration, out var loadedRamp))
        {
            return loadedRamp;
        }

        tubeFieldFallbackRampTexture ??= D3D12FieldTexture2D.CreateFallbackRamp(
            device,
            activeCommandList,
            "Aquarium D3D12 TubeField Fallback Ramp");
        return tubeFieldFallbackRampTexture;
    }

    private bool TryFindActiveFieldResource(string resourceKey, out AquariumFieldResourceDeclaration declaration)
    {
        foreach (var resource in activeFieldEvidenceFrame.Resources)
        {
            if (string.Equals(resource.ResourceKey, resourceKey, StringComparison.Ordinal))
            {
                declaration = resource;
                return true;
            }
        }

        declaration = default;
        return false;
    }

    private static void AppendSplineSurfaceEnvelopeGeometry(List<D3D12SplineVertex> output, AquariumSpline3D spline)
    {
        var style = spline.Style.Normalized();
        var controls = spline.Vertices;
        var subdivisions = Math.Clamp(spline.CatmullRomSubdivisions, 1, 16);
        var sampled = new List<AquariumSplineVertex>(Math.Max(2, (controls.Count - 1) * subdivisions + 1))
        {
            controls[0],
        };

        for (var segment = 0; segment < controls.Count - 1; segment++)
        {
            for (var step = segment == 0 ? 1 : 0; step <= subdivisions; step++)
            {
                var t = step / (float)subdivisions;
                sampled.Add(CatmullRom(controls, segment, t));
            }
        }

        for (var index = 0; index < sampled.Count - 1; index++)
        {
            var previous = index > 0 ? sampled[index - 1] : sampled[index];
            var start = sampled[index];
            var end = sampled[index + 1];
            var next = index + 2 < sampled.Count ? sampled[index + 2] : end;
            AppendSegmentGeometry(output, previous, start, end, next, style);
        }
    }

    private static AquariumSplineVertex CatmullRom(IReadOnlyList<AquariumSplineVertex> points, int segment, float t)
    {
        var p0 = points[Math.Max(0, segment - 1)];
        var p1 = points[segment];
        var p2 = points[Math.Min(points.Count - 1, segment + 1)];
        var p3 = points[Math.Min(points.Count - 1, segment + 2)];
        var position = (Vector3)math.catmullrom(
            (float3)p0.Position,
            (float3)p1.Position,
            (float3)p2.Position,
            (float3)p3.Position,
            t);
        var color = (Vector4)math.catmullrom(
            (float4)p0.Color,
            (float4)p1.Color,
            (float4)p2.Color,
            (float4)p3.Color,
            t);
        return new AquariumSplineVertex(position, Vector4.Clamp(color, Vector4.Zero, new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, 1.0f)));
    }

    private static void AppendSegmentGeometry(
        List<D3D12SplineVertex> output,
        AquariumSplineVertex previous,
        AquariumSplineVertex start,
        AquariumSplineVertex end,
        AquariumSplineVertex next,
        AquariumSplineStyle style)
    {
        var delta = end.Position - start.Position;
        if (delta.LengthSquared() < 0.000001f)
        {
            return;
        }

        var radius = style.Radius;
        var material = new Vector4(style.Emission, style.Alpha, style.GlowNormalExponent, style.AlphaNormalExponent);
        var color0 = start.Color with { W = start.Color.W * style.Alpha };
        var color1 = end.Color with { W = end.Color.W * style.Alpha };
        var shapeStartLeft = new Vector4(-1.0f, 0.0f, -1.0f, 0.0f);
        var shapeStartRight = new Vector4(1.0f, 0.0f, -1.0f, 0.0f);
        var shapeEndLeft = new Vector4(-1.0f, 1.0f, 1.0f, 0.0f);
        var shapeEndRight = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
        var radiusData = new Vector4(radius, radius, style.Feather, 0.0f);
        var v0 = new D3D12SplineVertex(
            start.Position,
            start.Position,
            end.Position,
            previous.Position,
            next.Position,
            shapeStartLeft,
            radiusData,
            color0,
            material);
        var v1 = new D3D12SplineVertex(
            start.Position,
            start.Position,
            end.Position,
            previous.Position,
            next.Position,
            shapeStartRight,
            radiusData,
            color0,
            material);
        var v2 = new D3D12SplineVertex(
            end.Position,
            start.Position,
            end.Position,
            previous.Position,
            next.Position,
            shapeEndLeft,
            radiusData,
            color1,
            material);
        var v3 = new D3D12SplineVertex(
            end.Position,
            start.Position,
            end.Position,
            previous.Position,
            next.Position,
            shapeEndRight,
            radiusData,
            color1,
            material);
        output.Add(v0);
        output.Add(v1);
        output.Add(v2);
        output.Add(v2);
        output.Add(v1);
        output.Add(v3);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private void RenderBloom(
        ID3D12GraphicsCommandList activeCommandList,
        FrameResources frameResources,
        D3D12RenderTarget sourceTarget,
        D3D12DescriptorSlot initialSourceDescriptor)
    {
        activeCommandList.BeginEvent("Bloom Pyramid");
        BeginGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.Bloom);
        try
        {
            activeCommandList.SetDescriptorHeaps(frameResources.TransientShaderDescriptors.Heap);
            activeCommandList.SetGraphicsRootSignature(fullscreenRootSignature);
            activeCommandList.SetGraphicsRootDescriptorTable(RootFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
            sourceTarget.Transition(activeCommandList, ResourceStates.PixelShaderResource);

            for (var level = 0; level < BloomLevelCount; level++)
            {
                var sourceDescriptor = level == 0
                    ? initialSourceDescriptor
                    : frameResources.BloomDescriptors[level - 1];
                var pipelineState = level == 0
                    ? bloomPrefilterPipelineState!
                    : bloomDownsamplePipelineState!;

                DrawPostToTarget(
                    activeCommandList,
                    bloomRenderTargets[level],
                    sourceDescriptor,
                    pipelineState);
                bloomRenderTargets[level].Transition(activeCommandList, ResourceStates.PixelShaderResource);

                DrawPostToTarget(
                    activeCommandList,
                    bloomScratchTargets[level],
                    frameResources.BloomDescriptors[level],
                    bloomBlurHorizontalPipelineState!);
                bloomScratchTargets[level].Transition(activeCommandList, ResourceStates.PixelShaderResource);

                DrawPostToTarget(
                    activeCommandList,
                    bloomRenderTargets[level],
                    frameResources.BloomScratchDescriptors[level],
                    bloomBlurVerticalPipelineState!);
                bloomRenderTargets[level].Transition(activeCommandList, ResourceStates.PixelShaderResource);
            }
        }
        finally
        {
            EndGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.Bloom);
            activeCommandList.EndEvent();
        }
    }

    private void DrawPostToTarget(
        ID3D12GraphicsCommandList activeCommandList,
        D3D12RenderTarget target,
        D3D12DescriptorSlot sourceDescriptor,
        ID3D12PipelineState pipelineState)
    {
        var targetViewport = new Viewport(0.0f, 0.0f, target.Width, target.Height);
        var targetScissorRect = new RawRect(0, 0, target.Width, target.Height);
        target.Transition(activeCommandList, ResourceStates.RenderTarget);
        activeCommandList.ClearRenderTargetView(target.RenderTargetView.Cpu, new Color4(0.0f, 0.0f, 0.0f, 1.0f));
        activeCommandList.SetPipelineState(pipelineState);
        activeCommandList.SetGraphicsRootDescriptorTable(RootSourceTexture, sourceDescriptor.Gpu);
        activeCommandList.RSSetViewports(targetViewport);
        activeCommandList.RSSetScissorRects(targetScissorRect);
        activeCommandList.OMSetRenderTargets(target.RenderTargetView.Cpu, null);
        activeCommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        activeCommandList.DrawInstanced(3, 1, 0, 0);
    }

    private void PresentBackBuffer(
        D3D12PassContext context,
        FrameResources frameResources,
        D3D12RenderTarget sourceTarget,
        D3D12DescriptorSlot sourceDescriptor)
    {
        context.CommandList.BeginEvent("Reservoir Presentation Resolve");
        BeginGpuTiming(context.CommandList, frameResources, D3D12GpuTimingPass.PresentationResolve);
        try
        {
            sourceTarget.Transition(context.CommandList, ResourceStates.PixelShaderResource);
            sceneMetadataRenderTarget.Transition(context.CommandList, ResourceStates.PixelShaderResource);
            sceneControlRenderTarget.Transition(context.CommandList, ResourceStates.PixelShaderResource);
            sceneReservoirGuideRenderTarget.Transition(context.CommandList, ResourceStates.PixelShaderResource);
            sceneOverdrawRenderTarget.Transition(context.CommandList, ResourceStates.PixelShaderResource);

            context.BackBuffer.Transition(context.CommandList, ResourceStates.RenderTarget);
            context.CommandList.SetDescriptorHeaps(frameResources.TransientShaderDescriptors.Heap);
            context.CommandList.SetPipelineState(resolvePipelineState!);
            context.CommandList.SetGraphicsRootSignature(fullscreenRootSignature);
            context.CommandList.SetGraphicsRootDescriptorTable(RootFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootSourceTexture, sourceDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootBloom, frameResources.BloomPresentationDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootCurrentSceneMetadata, frameResources.SceneMetadataDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootCurrentSceneControl, frameResources.SceneControlDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootSdfObjects, frameResources.SdfObjectDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootTemporalGaussians, frameResources.TemporalGaussianDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootCurrentReservoirGuide, frameResources.SceneReservoirGuideDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootBlueNoise, frameResources.BlueNoiseDescriptor.Gpu);
            context.CommandList.SetGraphicsRootDescriptorTable(RootSceneOverdraw, frameResources.SceneOverdrawDescriptor.Gpu);

            context.CommandList.RSSetViewports(viewport);
            context.CommandList.RSSetScissorRects(scissorRect);
            context.CommandList.OMSetRenderTargets(
            [
                context.RenderTargetView,
            ],
            null);
            context.CommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.CommandList.DrawInstanced(3, 1, 0, 0);
            CopyProgramOutputBackBuffer(context.CommandList, context.BackBuffer);
        }
        finally
        {
            EndGpuTiming(context.CommandList, frameResources, D3D12GpuTimingPass.PresentationResolve);
            context.CommandList.EndEvent();
        }
    }

    private void ClearEmptyBackBuffer(D3D12PassContext context)
    {
        context.CommandList.BeginEvent("Empty Presentation Clear");
        try
        {
            context.BackBuffer.Transition(context.CommandList, ResourceStates.RenderTarget);
            context.CommandList.RSSetViewports(viewport);
            context.CommandList.RSSetScissorRects(scissorRect);
            context.CommandList.OMSetRenderTargets(
            [
                context.RenderTargetView,
            ],
            null);
            context.CommandList.ClearRenderTargetView(context.RenderTargetView, new Color4(0.0f, 0.0f, 0.0f, 1.0f));
        }
        finally
        {
            context.CommandList.EndEvent();
        }
    }

    private void UploadSceneStructuredResources(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        activeCommandList.BeginEvent("SDF State Upload");
        try
        {
            sdfLightBuffer.Upload(activeCommandList, frameResources.UploadRing, sdfLights);
            sdfObjectBuffer.Upload(activeCommandList, frameResources.UploadRing, sdfObjects);
            gpuSensorCameraBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, gpuSensorCameras.AsSpan(0, gpuSensorCameraCount));
            acousticConstraintBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, acousticConstraints.AsSpan(0, acousticConstraintCount));
            if (temporalGaussiansGpuGenerated)
            {
                if (gpuFusionPointCount > 0)
                {
                    gpuFusionPointBuffer.UploadPartialBytes(
                        activeCommandList,
                        frameResources.UploadRing,
                        gpuFusionPointSource.Buffer,
                        gpuFusionPointCount,
                        gpuFusionPointSource.StrideBytes);
                }
                else
                {
                    gpuFusionSeedBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, gpuFusionSeeds.AsSpan(0, gpuFusionSeedCount));
                }
            }
            else
            {
                temporalGaussianBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, temporalGaussians.AsSpan(0, temporalGaussianCount));
            }
        }
        finally
        {
            activeCommandList.EndEvent();
        }
    }

    private void DispatchGpuSensorFusion(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        if (!temporalGaussiansGpuGenerated || temporalGaussianCount <= 0)
        {
            return;
        }

        activeCommandList.BeginEvent("GPU Sensor Fusion");
        BeginGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.GpuSensorFusion);
        try
        {
            gpuFusionSeedBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            gpuFusionPointBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            for (var index = 0; index < gpuSensorFieldTextureCount; index++)
            {
                if (TryGetFieldTexture2D(gpuSensorFieldTextureKeys[index], out var fieldTexture))
                {
                    fieldTexture.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                }
            }

            temporalGaussianBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
            activeCommandList.SetDescriptorHeaps(frameResources.TransientShaderDescriptors.Heap);
            activeCommandList.SetComputeRootSignature(gpuSensorFusionRootSignature);
            activeCommandList.SetPipelineState(gpuSensorFusionPipelineState!);
            activeCommandList.SetComputeRootDescriptorTable(RootFusionFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootFusionSeeds, frameResources.GpuFusionSeedDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootFusionSensorCameras, frameResources.GpuSensorCameraDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootFusionSensorTextures, frameResources.GpuSensorTextureDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootFusionAcousticConstraints, frameResources.AcousticConstraintDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootFusionOutput, frameResources.TemporalGaussianUnorderedAccessDescriptor.Gpu);
            activeCommandList.SetComputeRootDescriptorTable(RootFusionNativePoints, frameResources.GpuFusionPointDescriptor.Gpu);
            activeCommandList.Dispatch((uint)((temporalGaussianCount + 127) / 128), 1, 1);
            temporalGaussianBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
        }
        finally
        {
            EndGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.GpuSensorFusion);
            activeCommandList.EndEvent();
        }
    }

    private void DispatchFractalReservoirs(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        if (activePointCloudSurfaceReservoir)
        {
            return;
        }

        if (!activeFractalReservoirField.HasInput ||
            fractalSplatBuffer is null ||
            fractalSdfReservoirBuffer is null ||
            fractalPbrReservoirBuffer is null ||
            fractalRadiosityReservoirBuffer is null ||
            fractalFlameStateBuffer is null ||
            fractalProgramTransformBuffer is null)
        {
            return;
        }

        activeCommandList.BeginEvent("Fractal Reservoir GPU Update");
        BeginGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.FractalReservoirUpdate);
        try
        {
            if (activeFractalProgramTransforms.Length > 0)
            {
                fractalProgramTransformBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, activeFractalProgramTransforms);
            }
            else
            {
                ReadOnlySpan<AquariumPackedFractalIfsTransform> emptyProgram = [default];
                fractalProgramTransformBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, emptyProgram);
            }

            if (activeTextureSplinePrograms.Length > 0 && bufferFieldTextureSplineProgramBuffer is not null)
            {
                bufferFieldTextureSplineProgramBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, activeTextureSplinePrograms);
            }

            if (activeTextureFieldSamples.Length > 0 && bufferFieldTextureSampleBuffer is not null)
            {
                bufferFieldTextureSampleBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, activeTextureFieldSamples);
            }

            fractalSplatBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
            fractalSdfReservoirBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
            fractalPbrReservoirBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
            fractalRadiosityReservoirBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
            fractalFlameStateBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
            activeCommandList.SetComputeRootSignature(fractalReservoirRootSignature);
            var splatDispatchCount = temporalFrameIndex == 0
                ? activeFractalReservoirField.SplatCount
                : activeFractalReservoirField.SplatUpdatesPerFrame;
            BindFractalReservoirConstants(activeCommandList, splatDispatchCount);
            activeCommandList.SetComputeRootUnorderedAccessView(RootFractalSplats, fractalSplatBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootUnorderedAccessView(RootFractalSdfReservoirs, fractalSdfReservoirBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootUnorderedAccessView(RootFractalPbrReservoirs, fractalPbrReservoirBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootUnorderedAccessView(RootFractalRadiosityReservoirs, fractalRadiosityReservoirBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootUnorderedAccessView(RootFractalFlameStates, fractalFlameStateBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootShaderResourceView(RootFractalProgramTransforms, fractalProgramTransformBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootShaderResourceView(RootFractalTextureSplinePrograms, bufferFieldTextureSplineProgramBuffer?.Resource.GPUVirtualAddress ?? fractalProgramTransformBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetComputeRootShaderResourceView(RootFractalTextureSamples, bufferFieldTextureSampleBuffer?.Resource.GPUVirtualAddress ?? fractalProgramTransformBuffer.Resource.GPUVirtualAddress);
            Dispatch(fractalSplatPipelineState!, splatDispatchCount);
            activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fractalSplatBuffer.Resource));
            Dispatch(fractalSdfReservoirPipelineState!, activeFractalReservoirField.ReservoirUpdatesPerPass);
            Dispatch(fractalPbrReservoirPipelineState!, activeFractalReservoirField.ReservoirUpdatesPerPass);
            Dispatch(fractalRadiosityReservoirPipelineState!, activeFractalReservoirField.ReservoirUpdatesPerPass);
        }
        finally
        {
            EndGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.FractalReservoirUpdate);
            activeCommandList.EndEvent();
        }

        void Dispatch(ID3D12PipelineState pipelineState, int elementCount)
        {
            activeCommandList.SetPipelineState(pipelineState);
            activeCommandList.Dispatch((uint)((elementCount + 255) / 256), 1, 1);
        }
    }

    private void DispatchTubeFields(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        activeTubeFieldDrawIndexCount = 0;
        activeTubeFieldDispatchedSegments = 0;
        activeTubeFieldRequestedSegments = 0;
        activeTubeFieldTruncatedSegments = 0;
        activeTubeFieldIndirectDrawCount = 0;
        activeTubeFieldSkippedDrawBatches = 0;
        activeTubeFieldUnplannedLowerings = 0;
        activeTubeFieldInvalidColumns = 0;
        activeFieldResourceUploadCount = 0;
        activeFieldResourceUploadSkippedCount = 0;
        tubeFieldDrawBatches.Clear();
        if (tubeFieldComputePipelineState is null)
        {
            return;
        }

        activeCommandList.BeginEvent("Tube Field GPU Update");
        BeginGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.TubeFieldUpdate);
        try
        {
            UploadFieldResourceData(activeCommandList, frameResources);

            var segmentBase = 0;
            DispatchBokushoBrushField(activeCommandList, frameResources, ref segmentBase);

            if (activeFieldEvidenceFrame.TubeSplineLowerings.Count == 0)
            {
                activeTubeFieldDrawIndexCount = activeTubeFieldDispatchedSegments * 6;
                return;
            }

            var plannedTubeFieldClaims = PlannedTubeFieldClaimKeys();
            if (plannedTubeFieldClaims.Count == 0)
            {
                activeTubeFieldUnplannedLowerings = activeFieldEvidenceFrame.TubeSplineLowerings.Count;
                activeTubeFieldDrawIndexCount = activeTubeFieldDispatchedSegments * 6;
                return;
            }

            foreach (var lowering in activeFieldEvidenceFrame.TubeSplineLowerings)
            {
                if (!plannedTubeFieldClaims.Contains(lowering.ClaimKey))
                {
                    activeTubeFieldUnplannedLowerings++;
                    continue;
                }

                if (!lowering.IsValid ||
                    !fieldResourceRegistry.TryGetStructuredBuffer(lowering.ResourceKey, out var sourceBuffer))
                {
                    continue;
                }

                if (tubeFieldDrawBatches.Count >= MaxTubeFieldDrawBatches)
                {
                    activeTubeFieldSkippedDrawBatches++;
                    continue;
                }

                var normalized = lowering.Normalized();
                var validColumnCount = fieldResourceRegistry.CountContiguousValidColumns(normalized);
                activeTubeFieldInvalidColumns += Math.Max(0, normalized.ColumnCount - validColumnCount);
                if (validColumnCount <= 0)
                {
                    continue;
                }

                normalized = normalized with { ColumnCount = validColumnCount };
                var subdivisions = Math.Max(1, normalized.CatmullRomSubdivisions);
                var piecesPerColumn = checked(Math.Max(1, normalized.Width - 1) * subdivisions);
                var requestedSegments = checked(piecesPerColumn * Math.Max(1, normalized.ColumnCount));
                activeTubeFieldRequestedSegments += requestedSegments;
                var remainingSegments = MaxTubeFieldSegments - segmentBase;
                if (remainingSegments <= 0)
                {
                    activeTubeFieldTruncatedSegments += requestedSegments;
                    continue;
                }

                var dispatchSegments = Math.Min(requestedSegments, remainingSegments);
                if (dispatchSegments <= 0)
                {
                    continue;
                }

                activeTubeFieldTruncatedSegments += requestedSegments - dispatchSegments;
                var startIndex = segmentBase * 6;
                var fieldId = StableFieldId(lowering.ClaimKey, 5100.0f, 4096);
                var previousRollingOffset = previousTubeFieldRollingOffsets.TryGetValue(lowering.ClaimKey, out var storedRollingOffset)
                    ? storedRollingOffset
                    : normalized.RollingOffset;
                var constants = new D3D12TubeFieldConstants(
                    new Vector4(normalized.Width, normalized.Height, normalized.StrideBytes, normalized.FirstColumn),
                    new Vector4(normalized.ColumnCount, normalized.ColumnStride, normalized.RollingModulo, normalized.RollingOffset),
                    new Vector4(normalized.AmplitudePower, normalized.AmplitudeScale, normalized.NormalizeMin, normalized.NormalizeMax),
                    new Vector4(normalized.BaseRadius, normalized.RadiusScale, normalized.Alpha, normalized.Feather),
                    new Vector4(normalized.EmissionScale, subdivisions, segmentBase, dispatchSegments),
                    new Vector4(tubeFieldDrawBatches.Count * GeneratedMeshDrawArgumentUIntCount, startIndex, fieldId, previousRollingOffset),
                    normalized.Origin,
                    0.0f,
                    normalized.AxisStep,
                    0.0f,
                    normalized.ColumnStep,
                    0.0f);
                var constantsUpload = frameResources.UploadRing.WriteConstant(constants);

                sourceBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                tubeFieldVertexBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                tubeFieldIndexBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                tubeFieldStatsBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                tubeFieldDrawArgumentBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                activeCommandList.SetComputeRootSignature(tubeFieldRootSignature);
                activeCommandList.SetPipelineState(tubeFieldComputePipelineState);
                activeCommandList.SetComputeRootConstantBufferView(RootTubeFieldConstants, constantsUpload.GpuVirtualAddress);
                activeCommandList.SetComputeRootShaderResourceView(RootTubeFieldSource, sourceBuffer.Resource.GPUVirtualAddress);
                activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldVertices, tubeFieldVertexBuffer.Resource.GPUVirtualAddress);
                activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldIndices, tubeFieldIndexBuffer.Resource.GPUVirtualAddress);
                activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldStats, tubeFieldStatsBuffer.Resource.GPUVirtualAddress);
                activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldDrawArguments, tubeFieldDrawArgumentBuffer.Resource.GPUVirtualAddress);
                tubeFieldSegmentBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldSegments, tubeFieldSegmentBuffer.Resource.GPUVirtualAddress);
                var ramp = ResolveTubeFieldRamp(activeCommandList, normalized.RampResourceKey);
                ramp.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                var rampDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                ramp.CreateShaderResourceView(device, rampDescriptor);
                activeCommandList.SetComputeRootDescriptorTable(RootTubeFieldRamp, rampDescriptor.Gpu);
                activeCommandList.Dispatch((uint)((dispatchSegments + 255) / 256), 1, 1);
                activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldVertexBuffer.Resource));
                activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldIndexBuffer.Resource));
                activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldSegmentBuffer.Resource));
                activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldStatsBuffer.Resource));
                activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldDrawArgumentBuffer.Resource));

                segmentBase += dispatchSegments;
                activeTubeFieldDispatchedSegments += dispatchSegments;
                activeTubeFieldIndirectDrawCount++;
                tubeFieldDrawBatches.Add(new D3D12TubeFieldDrawBatch(
                    sourceBuffer,
                    constants,
                    constantsUpload.GpuVirtualAddress,
                    normalized.RampResourceKey,
                    tubeFieldDrawBatches.Count * GeneratedMeshDrawArgumentBytes));
                previousTubeFieldRollingOffsets[lowering.ClaimKey] = normalized.RollingOffset;
            }

            activeTubeFieldDrawIndexCount = activeTubeFieldDispatchedSegments * 6;
        }
        finally
        {
            EndGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.TubeFieldUpdate);
            activeCommandList.EndEvent();
        }
    }

    private HashSet<string> PlannedTubeFieldClaimKeys()
    {
        var planned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var packet in activeFieldLoweringPlan.Packets)
        {
            if (packet.Backend == AquariumFieldBackendKind.TubeField &&
                packet.Encoding == AquariumFieldEncoding.Tube)
            {
                planned.Add(packet.ClaimKey);
            }
        }

        return planned;
    }

    private void DispatchBokushoBrushField(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources, ref int segmentBase)
    {
        if (!activeBokushoBrushFrame.HasInput ||
            bokushoBrushPipelineState is null ||
            tubeFieldComputePipelineState is null ||
            tubeFieldDrawBatches.Count >= MaxTubeFieldDrawBatches)
        {
            return;
        }

        var frame = activeBokushoBrushFrame.Normalized();
        var sampleCount = Math.Clamp(frame.SampleCount, 2, MaxBokushoBrushSamples);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, MaxBokushoBrushTufts);
        var strokes = PackBokushoBrushStrokes(frame);
        var maxStrokeCountForBuffer = Math.Max(1, MaxBokushoBrushValues / checked(sampleCount * tuftCount));
        var strokeCount = Math.Min(strokes.Length, Math.Min(MaxBokushoBrushStrokes, maxStrokeCountForBuffer));
        var valueCount = checked(sampleCount * tuftCount * strokeCount);
        var constants = new D3D12BokushoBrushConstants(
            new Vector4(sampleCount, tuftCount, frame.PhysicsHz, strokeCount),
            new Vector4(frame.BrushRadius, frame.Pressure, frame.InkLoad, frame.Wetness),
            new Vector4(frame.Splay, frame.Bend, frame.Friction, 0.0f),
            new Vector4(frame.RadiusScale, frame.PressureScale, frame.NormalScale, frame.TangentScale),
            frame.StrokeP0,
            frame.StrokeP1,
            frame.StrokeP2,
            frame.StrokeP3);
        var constantsUpload = frameResources.UploadRing.WriteConstant(constants);
        bokushoBrushStrokeBuffer.UploadPartial(activeCommandList, frameResources.UploadRing, strokes.AsSpan(0, strokeCount));

        bokushoBrushTraceBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        bokushoBrushCanvasBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        bokushoBrushStrokeBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
        activeCommandList.SetComputeRootSignature(bokushoBrushRootSignature);
        activeCommandList.SetPipelineState(bokushoBrushPipelineState);
        activeCommandList.SetComputeRootConstantBufferView(RootBokushoBrushConstants, constantsUpload.GpuVirtualAddress);
        activeCommandList.SetComputeRootUnorderedAccessView(RootBokushoBrushTrace, bokushoBrushTraceBuffer.Resource.GPUVirtualAddress);
        activeCommandList.SetComputeRootUnorderedAccessView(RootBokushoBrushCanvas, bokushoBrushCanvasBuffer.Resource.GPUVirtualAddress);
        activeCommandList.SetComputeRootShaderResourceView(RootBokushoBrushStrokes, bokushoBrushStrokeBuffer.Resource.GPUVirtualAddress);
        activeCommandList.Dispatch((uint)(((tuftCount * strokeCount) + 127) / 128), 1, 1);
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(bokushoBrushTraceBuffer.Resource));
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(bokushoBrushCanvasBuffer.Resource));

        var subdivisions = 2;
        var requestedSegments = checked((sampleCount - 1) * subdivisions * tuftCount * strokeCount);
        activeTubeFieldRequestedSegments += requestedSegments;
        var remainingSegments = MaxTubeFieldSegments - segmentBase;
        if (remainingSegments <= 0)
        {
            activeTubeFieldTruncatedSegments += requestedSegments;
            return;
        }

        var dispatchSegments = Math.Min(requestedSegments, remainingSegments);
        activeTubeFieldTruncatedSegments += requestedSegments - dispatchSegments;
        var startIndex = segmentBase * 6;
        var fieldId = StableFieldId("bokusho.brush.direct-compute", 5600.0f, 1024);
        var tubeConstants = new D3D12TubeFieldConstants(
            new Vector4(sampleCount, tuftCount, sizeof(float), 0.0f),
            new Vector4(tuftCount * strokeCount, 1.0f, 0.0f, 0.0f),
            new Vector4(1.0f, 2.8f, 0.0f, 1.0f),
            new Vector4(0.010f, 0.030f, 0.92f, 0.14f),
            new Vector4(2.8f, subdivisions, segmentBase, dispatchSegments),
            new Vector4(tubeFieldDrawBatches.Count * GeneratedMeshDrawArgumentUIntCount, startIndex, fieldId, 0.0f),
            new Vector3(-7.0f, -2.2f, -0.45f),
            0.0f,
            new Vector3(14.0f / Math.Max(1, sampleCount - 1), 0.0f, 0.0f),
            0.0f,
            new Vector3(0.0f, 0.0f, 0.9f / Math.Max(1, tuftCount * strokeCount - 1)),
            0.0f);
        var tubeConstantsUpload = frameResources.UploadRing.WriteConstant(tubeConstants);

        bokushoBrushTraceBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
        bokushoBrushCanvasBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
        tubeFieldVertexBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        tubeFieldIndexBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        tubeFieldStatsBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        tubeFieldDrawArgumentBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        tubeFieldSegmentBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
        activeCommandList.SetComputeRootSignature(tubeFieldRootSignature);
        activeCommandList.SetPipelineState(tubeFieldComputePipelineState);
        activeCommandList.SetComputeRootConstantBufferView(RootTubeFieldConstants, tubeConstantsUpload.GpuVirtualAddress);
        activeCommandList.SetComputeRootShaderResourceView(RootTubeFieldSource, bokushoBrushTraceBuffer.Resource.GPUVirtualAddress);
        activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldVertices, tubeFieldVertexBuffer.Resource.GPUVirtualAddress);
        activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldIndices, tubeFieldIndexBuffer.Resource.GPUVirtualAddress);
        activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldStats, tubeFieldStatsBuffer.Resource.GPUVirtualAddress);
        activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldDrawArguments, tubeFieldDrawArgumentBuffer.Resource.GPUVirtualAddress);
        activeCommandList.SetComputeRootUnorderedAccessView(RootTubeFieldSegments, tubeFieldSegmentBuffer.Resource.GPUVirtualAddress);
        var ramp = ResolveTubeFieldRamp(activeCommandList, "");
        ramp.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
        var rampDescriptor = frameResources.TransientShaderDescriptors.Allocate();
        ramp.CreateShaderResourceView(device, rampDescriptor);
        activeCommandList.SetComputeRootDescriptorTable(RootTubeFieldRamp, rampDescriptor.Gpu);
        activeCommandList.Dispatch((uint)((dispatchSegments + 255) / 256), 1, 1);
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldVertexBuffer.Resource));
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldIndexBuffer.Resource));
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldSegmentBuffer.Resource));
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldStatsBuffer.Resource));
        activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(tubeFieldDrawArgumentBuffer.Resource));

        segmentBase += dispatchSegments;
        activeTubeFieldDispatchedSegments += dispatchSegments;
        activeTubeFieldIndirectDrawCount++;
        tubeFieldDrawBatches.Add(new D3D12TubeFieldDrawBatch(
            bokushoBrushTraceBuffer,
            tubeConstants,
            tubeConstantsUpload.GpuVirtualAddress,
            "",
            tubeFieldDrawBatches.Count * GeneratedMeshDrawArgumentBytes));
        _ = valueCount;
    }

    private void EvaluateStereoDepthLowerings()
    {
        activeStereoDepthDispatchReadyLowerings = 0;
        activeStereoDepthDispatchedLowerings = 0;
        activeStereoDepthUnplannedLowerings = 0;
        activeStereoDepthUnresolvedLowerings = 0;
        if (activeFieldEvidenceFrame.StereoDepthLowerings.Count == 0)
        {
            return;
        }

        var plannedClaims = PlannedStereoDepthClaimKeys();
        foreach (var lowering in activeFieldEvidenceFrame.StereoDepthLowerings)
        {
            if (!plannedClaims.Contains(lowering.ClaimKey))
            {
                activeStereoDepthUnplannedLowerings++;
                continue;
            }

            if (!lowering.IsValid ||
                !TryGetFieldTexture2D(lowering.LeftResourceKey, out _) ||
                !TryGetFieldTexture2D(lowering.RightResourceKey, out _) ||
                !fieldResourceRegistry.TryGetSurfacePage(lowering.DisparityResourceKey, out var disparity) ||
                !disparity.AllowsUnorderedAccess)
            {
                activeStereoDepthUnresolvedLowerings++;
                continue;
            }

            activeStereoDepthDispatchReadyLowerings++;
        }
    }

    private HashSet<string> PlannedStereoDepthClaimKeys()
    {
        var planned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var packet in activeFieldLoweringPlan.Packets)
        {
            if (packet.Backend == AquariumFieldBackendKind.SurfacePage &&
                packet.Encoding == AquariumFieldEncoding.Height)
            {
                planned.Add(packet.ClaimKey);
            }
        }

        return planned;
    }

    private void DispatchStereoDepth(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        activeStereoDepthDispatchedLowerings = 0;
        if (stereoDepthPipelineState is null || activeFieldEvidenceFrame.StereoDepthLowerings.Count == 0)
        {
            return;
        }

        activeCommandList.BeginEvent("Stereo Depth GPU Update");
        BeginGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.StereoDepthUpdate);
        try
        {
            var plannedClaims = PlannedStereoDepthClaimKeys();
            foreach (var lowering in activeFieldEvidenceFrame.StereoDepthLowerings)
            {
                var normalized = lowering.Normalized();
                if (!plannedClaims.Contains(normalized.ClaimKey) ||
                    !string.Equals(normalized.LeftResourceKey, normalized.RightResourceKey, StringComparison.Ordinal) ||
                    !TryGetFieldTexture2D(normalized.LeftResourceKey, out var packedInput) ||
                    !fieldResourceRegistry.TryGetSurfacePage(normalized.DisparityResourceKey, out var disparityOutput) ||
                    !disparityOutput.AllowsUnorderedAccess)
                {
                    continue;
                }

                packedInput.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                disparityOutput.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                var inputDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                packedInput.CreateShaderResourceView(device, inputDescriptor);
                var outputDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                if (!disparityOutput.TryCreateUnorderedAccessView(device, outputDescriptor))
                {
                    continue;
                }

                var constants = new D3D12StereoDepthConstants(
                    new Vector4(normalized.Width, normalized.Height, normalized.MinDisparity, normalized.DisparityLevels),
                    new Vector4(normalized.CensusRadius, normalized.AggregationPathCount, normalized.SmoothnessPenaltySmall, normalized.SmoothnessPenaltyLarge),
                    new Vector4(1.0f / MathF.Max(1.0f, normalized.DisparityLevels), 0.0f, 0.0f, 0.0f));
                var constantsUpload = frameResources.UploadRing.WriteConstant(constants);
                activeCommandList.SetComputeRootSignature(gpuSensorFusionRootSignature);
                activeCommandList.SetPipelineState(stereoDepthPipelineState);
                activeCommandList.SetComputeRootConstantBufferView(RootFusionFrameConstants, constantsUpload.GpuVirtualAddress);
                activeCommandList.SetComputeRootDescriptorTable(RootFusionSensorTextures, inputDescriptor.Gpu);
                activeCommandList.SetComputeRootDescriptorTable(RootFusionOutput, outputDescriptor.Gpu);
                activeCommandList.Dispatch((uint)((normalized.Width + 7) / 8), (uint)((normalized.Height + 7) / 8), 1);
                activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(disparityOutput.Resource));
                activeStereoDepthDispatchedLowerings++;
            }
        }
        finally
        {
            EndGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.StereoDepthUpdate);
            activeCommandList.EndEvent();
        }
    }

    private bool TryGetFieldTexture2D(string resourceKey, out D3D12FieldTexture2D texture)
    {
        if (fieldResourceRegistry.TryGetTexture2D(resourceKey, out texture))
        {
            return true;
        }

        if (sharedTextureLeases.TryGetValue(resourceKey, out var slot))
        {
            texture = slot.Texture;
            return true;
        }

        texture = null!;
        return false;
    }

    private void DispatchPointClouds(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        activePointCloudGeneratedMeshes = 0;
        activePointCloudUnresolvedMeshes = 0;
        activePointCloudUnplannedMeshes = 0;
        if (pointCloudComputePipelineState is null ||
            !activePointCloudSurfaceReservoir ||
            fractalSplatBuffer is null ||
            fractalSdfReservoirBuffer is null ||
            fractalPbrReservoirBuffer is null ||
            fractalRadiosityReservoirBuffer is null)
        {
            return;
        }

        var plannedMeshes = PlannedMeshPayloadsByClaim();
        if (plannedMeshes.Count == 0)
        {
            return;
        }

        activeCommandList.BeginEvent("Point Cloud GPU Update");
        try
        {
            foreach (var claim in activeFieldEvidenceFrame.Claims)
            {
                if (claim.Encoding != AquariumFieldEncoding.Mesh ||
                    !plannedMeshes.TryGetValue(claim.ClaimKey, out var meshResourceKey))
                {
                    continue;
                }

                if (!fieldResourceRegistry.TryGetMesh(meshResourceKey, out var mesh) ||
                    mesh.Topology != AquariumFieldMeshTopology.PointList ||
                    !mesh.HasStandardImportedLayout ||
                    !TryGetDerivedResourceKey(mesh.SourceUri, out var disparityResourceKey))
                {
                    activePointCloudUnresolvedMeshes++;
                    continue;
                }

                if (!fieldResourceRegistry.TryGetSurfacePage(disparityResourceKey, out var disparity))
                {
                    activePointCloudUnresolvedMeshes++;
                    continue;
                }

                var pointCount = Math.Max(1, Math.Min(mesh.Vertices.ElementCount, mesh.Indices.ElementCount));
                var sampledWidth = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(pointCount * Math.Max(1.0f, disparity.Width / Math.Max(1.0f, disparity.Height)))));
                var sampleStride = Math.Max(1, (int)MathF.Ceiling(disparity.Width / (float)sampledWidth));
                var constants = new D3D12PointCloudConstants(
                    new Vector4(disparity.Width, disparity.Height, pointCount, sampleStride),
                    new Vector4(10.0f, 250.0f, disparity.Width * 0.5f, disparity.Height * 0.5f),
                    new Vector4(0.20f, 4.0f, 5.0f, 1.2f));
                var constantsUpload = frameResources.UploadRing.WriteConstant(constants);

                disparity.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
                mesh.Vertices.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                mesh.Indices.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                if (activePointCloudSurfaceReservoir &&
                    fractalSplatBuffer is not null &&
                    fractalSdfReservoirBuffer is not null &&
                    fractalPbrReservoirBuffer is not null &&
                    fractalRadiosityReservoirBuffer is not null)
                {
                    fractalSplatBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                    fractalSdfReservoirBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                    fractalPbrReservoirBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                    fractalRadiosityReservoirBuffer.Transition(activeCommandList, ResourceStates.UnorderedAccess);
                }

                var disparityDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                disparity.CreateShaderResourceView(device, disparityDescriptor);
                var vertexDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                mesh.Vertices.CreateUnorderedAccessView(device, vertexDescriptor);
                var indexDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                mesh.Indices.CreateUnorderedAccessView(device, indexDescriptor);
                D3D12DescriptorSlot splatDescriptor = default;
                D3D12DescriptorSlot sdfDescriptor = default;
                D3D12DescriptorSlot pbrDescriptor = default;
                D3D12DescriptorSlot radiosityDescriptor = default;
                if (activePointCloudSurfaceReservoir &&
                    fractalSplatBuffer is not null &&
                    fractalSdfReservoirBuffer is not null &&
                    fractalPbrReservoirBuffer is not null &&
                    fractalRadiosityReservoirBuffer is not null)
                {
                    splatDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                    fractalSplatBuffer.CreateUnorderedAccessView(device, splatDescriptor);
                    sdfDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                    fractalSdfReservoirBuffer.CreateUnorderedAccessView(device, sdfDescriptor);
                    pbrDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                    fractalPbrReservoirBuffer.CreateUnorderedAccessView(device, pbrDescriptor);
                    radiosityDescriptor = frameResources.TransientShaderDescriptors.Allocate();
                    fractalRadiosityReservoirBuffer.CreateUnorderedAccessView(device, radiosityDescriptor);
                }

                activeCommandList.SetComputeRootSignature(pointCloudRootSignature);
                activeCommandList.SetPipelineState(pointCloudComputePipelineState);
                activeCommandList.SetComputeRootDescriptorTable(RootPointCloudFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
                activeCommandList.SetComputeRootConstantBufferView(RootPointCloudConstants, constantsUpload.GpuVirtualAddress);
                activeCommandList.SetComputeRootDescriptorTable(RootPointCloudDisparity, disparityDescriptor.Gpu);
                activeCommandList.SetComputeRootDescriptorTable(RootPointCloudVertices, vertexDescriptor.Gpu);
                activeCommandList.SetComputeRootDescriptorTable(RootPointCloudIndices, indexDescriptor.Gpu);
                if (activePointCloudSurfaceReservoir &&
                    fractalSplatBuffer is not null &&
                    fractalSdfReservoirBuffer is not null &&
                    fractalPbrReservoirBuffer is not null &&
                    fractalRadiosityReservoirBuffer is not null)
                {
                    activeCommandList.SetComputeRootDescriptorTable(RootPointCloudFractalSplats, splatDescriptor.Gpu);
                    activeCommandList.SetComputeRootDescriptorTable(RootPointCloudSdfReservoirs, sdfDescriptor.Gpu);
                    activeCommandList.SetComputeRootDescriptorTable(RootPointCloudPbrReservoirs, pbrDescriptor.Gpu);
                    activeCommandList.SetComputeRootDescriptorTable(RootPointCloudRadiosityReservoirs, radiosityDescriptor.Gpu);
                }

                activeCommandList.Dispatch((uint)((pointCount + 127) / 128), 1, 1);
                activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(mesh.Vertices.Resource));
                activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(mesh.Indices.Resource));
                if (activePointCloudSurfaceReservoir &&
                    fractalSplatBuffer is not null &&
                    fractalSdfReservoirBuffer is not null &&
                    fractalPbrReservoirBuffer is not null &&
                    fractalRadiosityReservoirBuffer is not null)
                {
                    activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fractalSplatBuffer.Resource));
                    activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fractalSdfReservoirBuffer.Resource));
                    activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fractalPbrReservoirBuffer.Resource));
                    activeCommandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(fractalRadiosityReservoirBuffer.Resource));
                }

                activePointCloudGeneratedMeshes++;
            }
        }
        finally
        {
            activeCommandList.EndEvent();
        }
    }

    private Dictionary<string, string> PlannedMeshPayloadsByClaim()
    {
        var planned = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var packet in activeFieldLoweringPlan.Packets)
        {
            if (packet.Backend == AquariumFieldBackendKind.Mesh &&
                packet.Encoding == AquariumFieldEncoding.Mesh &&
                LooksLikeFieldResourceKey(packet.PayloadHandle))
            {
                planned[packet.ClaimKey] = packet.PayloadHandle;
            }
        }

        return planned;
    }

    private static bool TryGetDerivedResourceKey(string sourceUri, out string resourceKey)
    {
        const string prefix = "derived-from:";
        resourceKey = "";
        if (!sourceUri.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        resourceKey = sourceUri[prefix.Length..];
        return !string.IsNullOrWhiteSpace(resourceKey);
    }

    private static bool LooksLikeFieldResourceKey(string payloadHandle) =>
        payloadHandle.StartsWith("resource:", StringComparison.Ordinal) ||
        payloadHandle.StartsWith("mimir:resource:", StringComparison.Ordinal) ||
        payloadHandle.StartsWith("aquarium:resource:", StringComparison.Ordinal);

    private void UploadFieldResourceData(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        foreach (var upload in activeFieldEvidenceFrame.ResourceUploads)
        {
            if (!upload.IsValid ||
                !fieldResourceRegistry.TryGetStructuredBuffer(upload.ResourceKey, out var buffer) ||
                buffer.StrideBytes != sizeof(float) ||
                upload.Float32Data.Count > buffer.ElementCount)
            {
                activeFieldResourceUploadSkippedCount++;
                continue;
            }

            if (upload.Float32Data is float[] array)
            {
                buffer.UploadPartial<float>(activeCommandList, frameResources.UploadRing, array, upload.ElementOffset);
            }
            else
            {
                buffer.UploadPartial<float>(activeCommandList, frameResources.UploadRing, upload.Float32Data.ToArray(), upload.ElementOffset);
            }

            activeFieldResourceUploadCount++;
            fieldResourceRegistry.MarkStructuredBufferUpload(upload.ResourceKey, upload.ElementOffset, upload.Float32Data.Count);
        }
    }

    private void BindFractalReservoirConstants(ID3D12GraphicsCommandList activeCommandList, int splatDispatchCount)
    {
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, (uint)activeFractalReservoirField.SplatCount, 0);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, (uint)temporalFrameIndex, 1);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, (uint)activeFractalReservoirField.Depth, 2);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, activeFractalReservoirField.Seed, 3);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, (uint)activeFractalReservoirField.CandidatesPerReservoirUpdate, 4);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, (uint)activeFractalReservoirField.ReservoirUpdatesPerPass, 5);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, (uint)Math.Max(activeFractalProgramTransforms.Length, activeTextureSplinePrograms.Length), 6);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, activeBufferFieldFrame.TextureSplineFields.Count > 0 ? 3u : activeFractalReservoirField.ProgramMode, 7);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, (uint)splatDispatchCount, 8);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, BitConverter.SingleToUInt32Bits(activeFractalReservoirField.PriorityFocus.X), 9);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, BitConverter.SingleToUInt32Bits(activeFractalReservoirField.PriorityFocus.Y), 10);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, BitConverter.SingleToUInt32Bits(activeFractalReservoirField.PriorityFocus.Z), 11);
        activeCommandList.SetComputeRoot32BitConstant(RootFractalConstants, BitConverter.SingleToUInt32Bits(activeFractalReservoirField.PriorityFocus.W), 12);
    }

    private void CreateGpuSensorTextureViews(AquariumGpuSensorFrame sensorFrame, D3D12DescriptorSlot firstDescriptor)
    {
        var activeHandles = new HashSet<string>(StringComparer.Ordinal);
        gpuSensorExternalTextureCount = Math.Min(sensorFrame.ExternalTextures.Count, MaxGpuSensorTextureCount);

        for (var index = 0; index < MaxGpuSensorTextureCount; index++)
        {
            var descriptor = frames[frameIndex].TransientShaderDescriptors.Offset(firstDescriptor, index);
            if (index >= gpuSensorTextureCount)
            {
                CreateNullSensorTextureView(descriptor);
                continue;
            }

            if (index >= gpuSensorExternalTextureCount)
            {
                var fieldTextureIndex = index - gpuSensorExternalTextureCount;
                if (fieldTextureIndex < gpuSensorFieldTextureCount &&
                    TryGetFieldTexture2D(gpuSensorFieldTextureKeys[fieldTextureIndex], out var fieldTexture))
                {
                    fieldTexture.CreateShaderResourceView(device, descriptor);
                }
                else
                {
                    CreateNullSensorTextureView(descriptor);
                }

                continue;
            }

            var texture = sensorFrame.ExternalTextures[index];
            var cacheKey = D3D12ExternalSensorTexture.CacheKey(texture);
            activeHandles.Add(cacheKey);
            if (!externalSensorTextures.TryGetValue(cacheKey, out var externalTexture)
                || !externalTexture.Matches(texture))
            {
                externalTexture?.Dispose();
                if (D3D12ExternalSensorTexture.TryOpen(device, texture, out var openedTexture))
                {
                    externalTexture = openedTexture;
                    externalSensorTextures[cacheKey] = externalTexture;
                }
                else
                {
                    externalSensorTextures.Remove(cacheKey);
                    CreateNullSensorTextureView(descriptor);
                    continue;
                }
            }

            externalTexture.UpdateTimestamp(texture.TimestampNs);
            externalTexture.CreateShaderResourceView(device, descriptor);
        }

        foreach (var staleHandle in externalSensorTextures.Keys.Where(handle => !activeHandles.Contains(handle)).ToArray())
        {
            externalSensorTextures[staleHandle].Dispose();
            externalSensorTextures.Remove(staleHandle);
        }
    }

    private void CreateNullSensorTextureView(D3D12DescriptorSlot descriptor)
    {
        device.CreateShaderResourceView(
            null,
            new ShaderResourceViewDescription
            {
                Format = Format.B8G8R8A8_UNorm,
                ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D,
                Shader4ComponentMapping = ShaderComponentMapping.Default,
                Texture2D = new Texture2DShaderResourceView { MipLevels = 1 },
            },
            descriptor.Cpu);
    }

    private void RenderHeightField(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources)
    {
        var viewport = new Viewport(0.0f, 0.0f, HeightFieldTextureSize, HeightFieldTextureSize);
        var scissorRect = new RawRect(0, 0, HeightFieldTextureSize, HeightFieldTextureSize);

        activeCommandList.BeginEvent("Height Field Pass");
        BeginGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.HeightField);
        try
        {
            heightFieldRenderTarget.Transition(activeCommandList, ResourceStates.RenderTarget);
            activeCommandList.ClearRenderTargetView(heightFieldRenderTarget.RenderTargetView.Cpu, new Color4(0.0f, 0.0f, 0.0f, 1.0f));
            activeCommandList.SetDescriptorHeaps(frameResources.TransientShaderDescriptors.Heap);
            activeCommandList.SetPipelineState(heightFieldBasePipelineState!);
            activeCommandList.SetGraphicsRootSignature(fullscreenRootSignature);
            activeCommandList.SetGraphicsRootDescriptorTable(RootFrameConstants, frameResources.FrameConstantsDescriptor.Gpu);
            var pageConstants = BuildBokushoPageConstants();
            var pageConstantsUpload = frameResources.UploadRing.WriteConstant(pageConstants);
            bokushoBrushCanvasBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            bokushoBrushStrokeBuffer.Transition(activeCommandList, ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource);
            activeCommandList.SetGraphicsRootConstantBufferView(RootBokushoPageConstants, pageConstantsUpload.GpuVirtualAddress);
            activeCommandList.SetGraphicsRootShaderResourceView(RootBokushoPageCanvas, bokushoBrushCanvasBuffer.Resource.GPUVirtualAddress);
            activeCommandList.SetGraphicsRootShaderResourceView(RootBokushoPageStrokes, bokushoBrushStrokeBuffer.Resource.GPUVirtualAddress);
            activeCommandList.RSSetViewports(viewport);
            activeCommandList.RSSetScissorRects(scissorRect);
            activeCommandList.OMSetRenderTargets(heightFieldRenderTarget.RenderTargetView.Cpu, null);
            activeCommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            activeCommandList.DrawInstanced(3, 1, 0, 0);
            activeCommandList.SetPipelineState(heightFieldBrushPipelineState!);
            activeCommandList.SetGraphicsRootDescriptorTable(RootHeightFieldBrushes, frameResources.HeightFieldBrushConstantsDescriptor.Gpu);
            activeCommandList.DrawInstanced(6, D3D12HeightFieldBrushConstants.MaxBrushCount, 0, 0);
        }
        finally
        {
            EndGpuTiming(activeCommandList, frameResources, D3D12GpuTimingPass.HeightField);
            activeCommandList.EndEvent();
        }
    }

    private void CopySceneState(AquariumSceneState scene)
    {
        Array.Clear(sdfObjects);
        Array.Clear(sdfLights);
        temporalGaussianCount = 0;
        gpuSensorCameraCount = 0;
        gpuSensorTextureCount = 0;
        gpuSensorExternalTextureCount = 0;
        gpuSensorFieldTextureCount = 0;
        gpuSensorFieldUnsupportedTextureCount = 0;
        Array.Clear(gpuSensorFieldTextureKeys);
        acousticConstraintCount = 0;
        gpuFusionSeedCount = 0;
        gpuFusionPointCount = 0;
        gpuFusionPointSource = default;
        temporalGaussiansGpuGenerated = false;
        var bufferFieldUsesReservoir = ShouldLowerBufferFieldToReservoir(scene.BufferFieldFrame);
        activeFractalReservoirField = bufferFieldUsesReservoir && scene.BufferFieldFrame.Reservoir.HasInput
            ? scene.BufferFieldFrame.Reservoir
            : scene.FractalReservoirField.HasInput
            ? scene.FractalReservoirField
            : AquariumFractalReservoirField.Empty;
        activeBufferFieldFrame = scene.BufferFieldFrame.HasInput
            ? scene.BufferFieldFrame
            : AquariumBufferFieldFrame.Empty;
        activeBokushoBrushFrame = scene.BokushoBrushFrame.HasInput
            ? scene.BokushoBrushFrame.Normalized()
            : AquariumBokushoBrushFrame.Empty;
        activeTextureSplinePrograms = PackTextureSplinePrograms(activeBufferFieldFrame);
        activeTextureFieldSamples = FlattenTextureSamples(activeBufferFieldFrame);
        activeSplineFrame = MergeSplineFrames(
            scene.SplineFrame.HasInput ? scene.SplineFrame : AquariumSplineFrame.Empty,
            BuildTextureSplineFrame(activeBufferFieldFrame, bufferFieldUsesReservoir));
        activeFieldEvidenceFrame = scene.FieldEvidenceFrame.HasInput
            ? scene.FieldEvidenceFrame
            : AquariumFieldEvidenceFrame.Empty;
        activeFieldEvidenceValidation = AquariumFieldEvidenceValidator.Validate(activeFieldEvidenceFrame);
        activeFieldResourceStats = activeFieldEvidenceValidation.HasErrors
            ? fieldResourceRegistry.Resolve(device, resourceRegistry, [])
            : fieldResourceRegistry.Resolve(device, resourceRegistry, activeFieldEvidenceFrame.Resources);
        activeFieldLoweringPlan = activeFieldEvidenceValidation.HasErrors
            ? AquariumFieldLoweringPlan.Empty
            : AquariumFieldLoweringPlanner.Plan(activeFieldEvidenceFrame);
        EvaluateStereoDepthLowerings();
        var pointCloudReservoirField = BuildPointCloudSurfaceReservoirField();
        activeFractalProgramTransforms = activeFractalReservoirField.HasInput && scene.FractalReservoirField.ProgramTransforms.Count > 0
            ? scene.FractalReservoirField.ProgramTransforms as AquariumPackedFractalIfsTransform[] ?? scene.FractalReservoirField.ProgramTransforms.ToArray()
            : [];
        if (!activeFractalReservoirField.HasInput && pointCloudReservoirField.HasInput)
        {
            activeFractalReservoirField = pointCloudReservoirField;
            activePointCloudSurfaceReservoir = true;
        }
        else
        {
            activePointCloudSurfaceReservoir = false;
        }

        visibleFractalSplatCount = activeFractalReservoirField.HasInput
            ? Math.Min(activeFractalReservoirField.SplatCount, MaxVisibleFractalSplatCount)
            : 0;
        heightFieldBrushConstants = D3D12HeightFieldBrushConstants.FromBrushes(scene.HeightFieldBrushes);

        var objectCount = Math.Min(scene.SdfObjects.Count, MaxSdfObjectCount);
        for (var index = 0; index < objectCount; index++)
        {
            sdfObjects[index] = scene.SdfObjects[index];
        }

        var lightCount = Math.Min(scene.SdfLights.Count, MaxSdfLightCount);
        for (var index = 0; index < lightCount; index++)
        {
            sdfLights[index] = scene.SdfLights[index];
        }

        gpuSensorCameraCount = Math.Min(scene.GpuSensorFrame.Cameras.Count, MaxGpuSensorCameraCount);
        for (var index = 0; index < gpuSensorCameraCount; index++)
        {
            gpuSensorCameras[index] = ToGpuSensorCameraPacket(scene.GpuSensorFrame.Cameras[index]);
        }
        gpuSensorExternalTextureCount = Math.Min(scene.GpuSensorFrame.ExternalTextures.Count, MaxGpuSensorTextureCount);
        AppendFieldEvidenceGpuSensorInputs();

        acousticConstraintCount = Math.Min(scene.AcousticFieldFrame.Constraints.Count, MaxAcousticConstraintCount);
        for (var index = 0; index < acousticConstraintCount; index++)
        {
            acousticConstraints[index] = ToAcousticConstraintPacket(scene.AcousticFieldFrame.Constraints[index]);
        }

        var clapCapacity = MaxAcousticConstraintCount - acousticConstraintCount;
        var clapCount = Math.Min(scene.CalibrationEventFrame.ClapEvents.Count, clapCapacity);
        for (var index = 0; index < clapCount; index++)
        {
            acousticConstraints[acousticConstraintCount + index] = ToClapConstraintPacket(scene.CalibrationEventFrame.ClapEvents[index]);
        }

        acousticConstraintCount += clapCount;

        if (scene.GpuFusionField.PointBuffer.HasInput)
        {
            var pointCount = Math.Min(scene.GpuFusionField.PointBuffer.Count, MaxTemporalGaussianCount);
            temporalGaussianCount = pointCount;
            gpuFusionPointCount = pointCount;
            gpuFusionPointSource = scene.GpuFusionField.PointBuffer with { Count = pointCount };
            temporalGaussiansGpuGenerated = true;
            return;
        }

        if (scene.GpuFusionField.Seeds.Count > 0)
        {
            var seedCount = Math.Min(scene.GpuFusionField.Seeds.Count, MaxTemporalGaussianCount);
            temporalGaussianCount = seedCount;
            gpuFusionSeedCount = seedCount;
            temporalGaussiansGpuGenerated = true;
            for (var index = 0; index < seedCount; index++)
            {
                gpuFusionSeeds[index] = ToGpuFusionSeedPacket(scene.GpuFusionField.Seeds[index]);
            }
            return;
        }

        if (gpuSensorTextureCount > 0 && gpuSensorCameraCount > 0)
        {
            temporalGaussianCount = Math.Min(MaxTemporalGaussianCount, gpuSensorTextureCount * GpuSensorSamplesPerTexture);
            temporalGaussiansGpuGenerated = true;
            return;
        }

        var gaussianCount = Math.Min(scene.TemporalGaussianField.Gaussians.Count, MaxTemporalGaussianCount);
        temporalGaussianCount = gaussianCount;
        for (var index = 0; index < gaussianCount; index++)
        {
            temporalGaussians[index] = ToTemporalGaussianPacket(scene.TemporalGaussianField.Gaussians[index]);
        }
    }

    private D3D12BokushoBrushConstants BuildBokushoPageConstants()
    {
        if (!activeBokushoBrushFrame.HasInput)
        {
            return new D3D12BokushoBrushConstants(
                Vector4.Zero,
                Vector4.Zero,
                Vector4.Zero,
                Vector4.Zero,
                Vector4.Zero,
                Vector4.Zero,
                Vector4.Zero,
                Vector4.Zero);
        }

        var frame = activeBokushoBrushFrame.Normalized();
        var sampleCount = Math.Clamp(frame.SampleCount, 2, MaxBokushoBrushSamples);
        var tuftCount = Math.Clamp(frame.TuftCount, 1, MaxBokushoBrushTufts);
        var strokeCount = BoundedBokushoStrokeCount(frame, sampleCount, tuftCount);
        return new D3D12BokushoBrushConstants(
            new Vector4(sampleCount, tuftCount, frame.PhysicsHz, strokeCount),
            new Vector4(frame.BrushRadius, frame.Pressure, frame.InkLoad, frame.Wetness),
            new Vector4(frame.Splay, frame.Bend, frame.Friction, 0.0f),
            new Vector4(frame.RadiusScale, frame.PressureScale, frame.NormalScale, frame.TangentScale),
            frame.StrokeP0,
            frame.StrokeP1,
            frame.StrokeP2,
            frame.StrokeP3);
    }

    private int BoundedBokushoStrokeCount(AquariumBokushoBrushFrame frame, int sampleCount, int tuftCount)
    {
        var strokeCount = PackBokushoBrushStrokes(frame).Length;
        var maxStrokeCountForBuffer = Math.Max(1, MaxBokushoBrushValues / checked(sampleCount * tuftCount));
        return Math.Min(strokeCount, Math.Min(MaxBokushoBrushStrokes, maxStrokeCountForBuffer));
    }

    private static D3D12BokushoBrushStrokePacket[] PackBokushoBrushStrokes(AquariumBokushoBrushFrame frame)
    {
        if (frame.Strokes.Count > 0)
        {
            var packets = new D3D12BokushoBrushStrokePacket[frame.Strokes.Count];
            for (var index = 0; index < packets.Length; index++)
            {
                packets[index] = PackBokushoBrushStroke(frame.Strokes[index].Normalized());
            }

            return packets;
        }

        return
        [
            new D3D12BokushoBrushStrokePacket(
                new Vector4(frame.RadiusScale, frame.PressureScale, frame.NormalScale, frame.TangentScale),
                new Vector4(0.10f, 0.14f, 1.0f, 1.0f),
                frame.StrokeP0,
                frame.StrokeP1,
                frame.StrokeP2,
                frame.StrokeP3),
        ];
    }

    private static D3D12BokushoBrushStrokePacket PackBokushoBrushStroke(AquariumBokushoBrushStroke stroke)
    {
        return new D3D12BokushoBrushStrokePacket(
            new Vector4(stroke.RadiusScale, stroke.PressureScale, stroke.NormalScale, stroke.TangentScale),
            new Vector4(stroke.EntryTaper, stroke.ExitTaper, stroke.PigmentScale, stroke.SplitScale),
            stroke.StrokeP0,
            stroke.StrokeP1,
            stroke.StrokeP2,
            stroke.StrokeP3);
    }

    private void AppendFieldEvidenceGpuSensorInputs()
    {
        gpuSensorTextureCount = gpuSensorExternalTextureCount;
        if (!activeFieldEvidenceFrame.HasInput ||
            activeFieldLoweringPlan.Packets.Count == 0 ||
            gpuSensorCameraCount >= MaxGpuSensorCameraCount ||
            gpuSensorTextureCount >= MaxGpuSensorTextureCount)
        {
            return;
        }

        var resources = activeFieldEvidenceFrame.Resources.ToDictionary(static resource => resource.ResourceKey, StringComparer.Ordinal);
        var usedResources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var packet in activeFieldLoweringPlan.Packets)
        {
            if (packet.Backend != AquariumFieldBackendKind.GpuSensorFusion ||
                packet.Encoding != AquariumFieldEncoding.Feature ||
                string.IsNullOrWhiteSpace(packet.PayloadHandle) ||
                !usedResources.Add(packet.PayloadHandle) ||
                gpuSensorCameraCount >= MaxGpuSensorCameraCount ||
                gpuSensorTextureCount >= MaxGpuSensorTextureCount ||
                !resources.TryGetValue(packet.PayloadHandle, out var declaration) ||
                declaration.Kind != AquariumFieldResourceKind.Texture2D ||
                !declaration.IsGpuVisible)
            {
                continue;
            }

            if (!IsGpuSensorFusionSampleable(declaration) ||
                !TryGetFieldTexture2D(declaration.ResourceKey, out var texture))
            {
                gpuSensorFieldUnsupportedTextureCount++;
                continue;
            }

            var firstTextureIndex = gpuSensorTextureCount;
            gpuSensorFieldTextureKeys[gpuSensorFieldTextureCount] = declaration.ResourceKey;
            gpuSensorFieldTextureCount++;
            gpuSensorTextureCount++;
            var width = Math.Max(1, texture.Width);
            var height = Math.Max(1, texture.Height);
            var kind = SensorKindForFieldTexture(declaration);
            gpuSensorCameras[gpuSensorCameraCount] = ToGpuSensorCameraPacket(new AquariumGpuSensorCamera(
                SensorId: declaration.ResourceKey,
                Kind: kind,
                WorldFromSensor: Matrix4x4.Identity,
                SensorFromWorld: Matrix4x4.Identity,
                Intrinsics: new Vector4(width, height, width * 0.5f, height * 0.5f),
                Distortion01: Vector4.Zero,
                Distortion23: Vector4.Zero,
                Width: width,
                Height: height,
                FirstTextureIndex: firstTextureIndex,
                TextureCount: 1,
                TimestampNs: declaration.ValidFromNs > 0 ? declaration.ValidFromNs : unchecked((long)Math.Min(declaration.Version, (ulong)long.MaxValue)),
                PixelFormat: PixelFormatForFieldTexture(declaration)));
            gpuSensorCameraCount++;
        }
    }

    private static AquariumGpuSensorKind SensorKindForFieldTexture(AquariumFieldResourceDeclaration declaration)
    {
        if (declaration.Format.Equals("LeapStereoIr", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("LeapPackedMap", StringComparison.OrdinalIgnoreCase))
        {
            return AquariumGpuSensorKind.LeapPackedMap;
        }

        if (declaration.Format.Equals("Bayer8", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("Gray8", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("R8", StringComparison.OrdinalIgnoreCase) ||
            declaration.ResourceKey.Contains("ps3-eye", StringComparison.OrdinalIgnoreCase))
        {
            return AquariumGpuSensorKind.HighRateTracker;
        }

        return AquariumGpuSensorKind.RgbCamera;
    }

    private static bool IsGpuSensorFusionSampleable(AquariumFieldResourceDeclaration declaration) =>
        declaration.Format.Equals("LeapStereoIr", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("LeapPackedMap", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("Bayer8", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("Gray8", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("R8", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("R8Unorm", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("R8_UNorm", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("Rg8", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("Rg8Unorm", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("R8G8_UNorm", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("Bgra8", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("Bgra8Unorm", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("B8G8R8A8_UNorm", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("Rgba8Unorm", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("R8G8B8A8_UNorm", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("Yuy2", StringComparison.OrdinalIgnoreCase) ||
        declaration.Format.Equals("YUY2", StringComparison.OrdinalIgnoreCase);

    private static AquariumGpuSensorPixelFormat PixelFormatForFieldTexture(AquariumFieldResourceDeclaration declaration)
    {
        if (declaration.Format.Equals("LeapStereoIr", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("LeapPackedMap", StringComparison.OrdinalIgnoreCase))
        {
            return AquariumGpuSensorPixelFormat.LeapPackedMap;
        }

        if (declaration.Format.Equals("Bayer8", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("Gray8", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("R8", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("R8Unorm", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("R8_UNorm", StringComparison.OrdinalIgnoreCase))
        {
            return AquariumGpuSensorPixelFormat.R8Unorm;
        }

        if (declaration.Format.Equals("Rg8", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("Rg8Unorm", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("R8G8_UNorm", StringComparison.OrdinalIgnoreCase))
        {
            return AquariumGpuSensorPixelFormat.Rg8Unorm;
        }

        if (declaration.Format.Equals("Bgra8", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("Bgra8Unorm", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("B8G8R8A8_UNorm", StringComparison.OrdinalIgnoreCase))
        {
            return AquariumGpuSensorPixelFormat.Bgra8Unorm;
        }

        if (declaration.Format.Equals("Yuy2", StringComparison.OrdinalIgnoreCase) ||
            declaration.Format.Equals("YUY2", StringComparison.OrdinalIgnoreCase))
        {
            return AquariumGpuSensorPixelFormat.Yuy2;
        }

        return AquariumGpuSensorPixelFormat.Rgba8Unorm;
    }

    private static D3D12AcousticConstraintPacket ToAcousticConstraintPacket(AquariumAcousticConstraint constraint)
    {
        var confidence = Math.Clamp(constraint.Confidence, 0.0f, 1.0f);
        return new D3D12AcousticConstraintPacket(
            new Vector4(constraint.Position, MathF.Max(0.001f, constraint.RadiusMeters)),
            new Vector4(constraint.Velocity, confidence),
            new Vector4((int)constraint.Kind, constraint.TimestampNs * 0.000000001f, 0.0f, 0.0f));
    }

    private static D3D12AcousticConstraintPacket ToClapConstraintPacket(AquariumClapCalibrationEvent clap)
    {
        var confidence = Math.Clamp(clap.VisualConfidence * clap.AcousticConfidence, 0.0f, 1.0f);
        return new D3D12AcousticConstraintPacket(
            new Vector4(clap.Position, 0.24f),
            new Vector4(Vector3.Zero, confidence),
            new Vector4((int)AquariumAcousticConstraintKind.ClapImpact, clap.AcousticOracleNs * 0.000000001f, clap.TimingUncertaintyMicroseconds, 0.0f));
    }

    private static D3D12GpuSensorCameraPacket ToGpuSensorCameraPacket(AquariumGpuSensorCamera camera)
    {
        static Vector4 Row0(Matrix4x4 matrix) => new(matrix.M11, matrix.M12, matrix.M13, matrix.M14);
        static Vector4 Row1(Matrix4x4 matrix) => new(matrix.M21, matrix.M22, matrix.M23, matrix.M24);
        static Vector4 Row2(Matrix4x4 matrix) => new(matrix.M31, matrix.M32, matrix.M33, matrix.M34);

        return new D3D12GpuSensorCameraPacket(
            camera.Intrinsics,
            camera.Distortion01,
            camera.Distortion23,
            new Vector4(
                Math.Max(0, camera.Width),
                Math.Max(0, camera.Height),
                (int)camera.Kind,
                camera.TextureCount),
            new Vector4(
                Math.Max(0, camera.FirstTextureIndex),
                camera.TextureCount,
                camera.TimestampNs * 0.000000001f,
                (int)camera.PixelFormat),
            Row0(camera.WorldFromSensor),
            Row1(camera.WorldFromSensor),
            Row2(camera.WorldFromSensor),
            Row0(camera.SensorFromWorld),
            Row1(camera.SensorFromWorld),
            Row2(camera.SensorFromWorld));
    }

    private static D3D12GpuFusionSeedPacket ToGpuFusionSeedPacket(AquariumGpuFusionSeed seed)
    {
        return new D3D12GpuFusionSeedPacket(
            new Vector4(seed.Center, Math.Clamp(seed.HistoryWeight, 0.0f, 1.0f)),
            new Vector4(seed.PreviousCenter, seed.FieldId),
            new Vector4(seed.Velocity, Math.Clamp(seed.Confidence, 0.0f, 1.0f)),
            new Vector4(Vector3.Max(seed.Radii, new Vector3(0.0001f)), MathF.Max(0.0001f, seed.Falloff)),
            seed.ColorOpacity,
            new Vector4(MathF.Max(0.0001f, seed.ShapePower), 0.0f, 0.0f, 0.0f));
    }

    private static D3D12TemporalGaussianPacket ToTemporalGaussianPacket(AquariumTemporalSdfGaussian gaussian)
    {
        var orientation = gaussian.Orientation.LengthSquared() <= 0.000001f
            ? Quaternion.Identity
            : Quaternion.Normalize(gaussian.Orientation);
        return new D3D12TemporalGaussianPacket(
            new Vector4(gaussian.Center, gaussian.HistoryWeight),
            new Vector4(gaussian.PreviousCenter, gaussian.FieldId),
            new Vector4(gaussian.Velocity, gaussian.Confidence),
            new Vector4(gaussian.Radii, gaussian.Falloff),
            new Vector4(orientation.X, orientation.Y, orientation.Z, orientation.W),
            gaussian.ColorOpacity,
            new Vector4(gaussian.ShapePower, 0.0f, 0.0f, 0.0f));
    }

    private static AquariumPackedTextureSplineFieldProgram[] PackTextureSplinePrograms(AquariumBufferFieldFrame frame)
    {
        if (!frame.HasInput || frame.TextureSplineFields.Count == 0)
        {
            return [];
        }

        var textureOffsets = new Dictionary<string, (AquariumTextureFieldBinding Texture, int Offset, int Index)>(StringComparer.Ordinal);
        var offset = 0;
        for (var index = 0; index < frame.Textures.Count; index++)
        {
            var texture = frame.Textures[index];
            textureOffsets[texture.Id] = (texture, offset, index);
            offset += texture.Samples.Count;
        }

        var programs = new AquariumPackedTextureSplineFieldProgram[frame.TextureSplineFields.Count];
        for (var index = 0; index < frame.TextureSplineFields.Count; index++)
        {
            var program = frame.TextureSplineFields[index];
            if (!textureOffsets.TryGetValue(program.TextureId, out var textureItem))
            {
                continue;
            }

            var texture = textureItem.Texture;
            var appearance = program.Appearance.Normalized();
            var policy = program.ProbePolicy.Normalized();
            programs[index] = new AquariumPackedTextureSplineFieldProgram(
                new Vector4(textureItem.Index, texture.Width, texture.Height, texture.Channels),
                new Vector4((float)program.FrequencyAxis, (float)texture.RollingMode, texture.RollingOffset, textureItem.Offset),
                new Vector4(program.FirstColumn, program.ColumnCount, program.ColumnStride, program.RollingWindowModulo),
                new Vector4(program.Subdivisions, policy.MaxProbeCount, policy.BaseDensity, policy.MinimumVisualContribution),
                new Vector4(program.Origin, program.AmplitudeScale),
                new Vector4(program.AxisStep, appearance.Radius),
                new Vector4(program.ColumnStep, appearance.Alpha),
                new Vector4(program.ColumnGroupStep, Math.Max(0, program.ColumnGroupSize)),
                appearance.Emission,
                new Vector4(appearance.ZeroThreshold, appearance.Feather, appearance.TangentWeight, appearance.CurvatureWeight),
                new Vector4(appearance.NormalWeight, appearance.DerivativeWeight, policy.Seed, 0.0f));
        }

        return programs;
    }

    private static bool ShouldLowerBufferFieldToReservoir(AquariumBufferFieldFrame frame)
    {
        if (!frame.HasInput || !frame.Reservoir.HasInput)
        {
            return false;
        }

        var policy = frame.LoweringPolicy.Normalized();
        return policy.Mode switch
        {
            AquariumFieldLoweringMode.ReservoirSplats => true,
            AquariumFieldLoweringMode.DirectSdfTubes => false,
            AquariumFieldLoweringMode.Mesh => false,
            _ => EstimateTextureSplineColumnCount(frame) > policy.MaxDirectSplines,
        };
    }

    private AquariumFractalReservoirField BuildPointCloudSurfaceReservoirField()
    {
        var splatCount = 0;
        foreach (var packet in activeFieldLoweringPlan.Packets)
        {
            if (packet.Backend != AquariumFieldBackendKind.Mesh ||
                packet.Encoding != AquariumFieldEncoding.Mesh ||
                !fieldResourceRegistry.TryGetMesh(packet.PayloadHandle, out var mesh) ||
                mesh.Topology != AquariumFieldMeshTopology.PointList ||
                !mesh.IsGeneratedFromResource ||
                !mesh.HasStandardImportedLayout)
            {
                continue;
            }

            splatCount = Math.Max(splatCount, Math.Min(mesh.Vertices.ElementCount, mesh.Indices.ElementCount));
        }

        if (splatCount <= 0)
        {
            return AquariumFractalReservoirField.Empty;
        }

        splatCount = Math.Min(splatCount, MaxVisibleFractalSplatCount);
        return new AquariumFractalReservoirField
        {
            SplatCount = splatCount,
            Depth = 1,
            Seed = 0x1EA9D371u,
            CandidatesPerReservoirUpdate = 1,
            SplatUpdatesPerFrame = splatCount,
            ReservoirUpdatesPerPass = splatCount,
            WorldCenterRadius = new Vector4(0.0f, 0.0f, 0.0f, 1.0f),
            PriorityFocus = new Vector4(0.0f, 0.0f, 2.0f, 1.0f),
        };
    }

    private static int EstimateTextureSplineColumnCount(AquariumBufferFieldFrame frame)
    {
        if (!frame.HasInput || frame.TextureSplineFields.Count == 0 || frame.Textures.Count == 0)
        {
            return 0;
        }

        var textures = frame.Textures.ToDictionary(texture => texture.Id, StringComparer.Ordinal);
        var count = 0;
        foreach (var program in frame.TextureSplineFields)
        {
            if (!textures.TryGetValue(program.TextureId, out var texture))
            {
                continue;
            }

            count += Math.Clamp(
                program.ColumnCount,
                1,
                program.FrequencyAxis == AquariumTextureAxis.X ? texture.Height : texture.Width);
        }

        return count;
    }

    private static AquariumSplineFrame BuildTextureSplineFrame(AquariumBufferFieldFrame frame, bool loweredToReservoir)
    {
        if (loweredToReservoir || !frame.HasInput || frame.TextureSplineFields.Count == 0 || frame.Textures.Count == 0)
        {
            return AquariumSplineFrame.Empty;
        }

        var textures = frame.Textures.ToDictionary(texture => texture.Id, StringComparer.Ordinal);
        var splines = new List<AquariumSpline3D>();
        var policy = frame.LoweringPolicy.Normalized();
        var requestedColumns = Math.Max(1, EstimateTextureSplineColumnCount(frame));
        var effectiveMaxSplines = Math.Max(1, (int)MathF.Round(policy.MaxDirectSplines * policy.LodBias));
        var columnLodStride = Math.Max(1, (int)MathF.Ceiling(requestedColumns / (float)effectiveMaxSplines));
        var requestedControlPoints = 0;
        foreach (var program in frame.TextureSplineFields)
        {
            if (!textures.TryGetValue(program.TextureId, out var texture))
            {
                continue;
            }

            var axisSamples = program.FrequencyAxis == AquariumTextureAxis.X ? texture.Width : texture.Height;
            var columnCount = Math.Clamp(program.ColumnCount, 1, program.FrequencyAxis == AquariumTextureAxis.X ? texture.Height : texture.Width);
            requestedControlPoints += Math.Max(1, axisSamples) * Math.Max(1, (int)MathF.Ceiling(columnCount / (float)columnLodStride));
        }

        var effectiveMaxControlPoints = Math.Max(2, (int)MathF.Round(policy.MaxDirectControlPoints * policy.LodBias));
        var axisLodStride = Math.Max(1, (int)MathF.Ceiling(requestedControlPoints / (float)effectiveMaxControlPoints));
        foreach (var program in frame.TextureSplineFields)
        {
            if (!textures.TryGetValue(program.TextureId, out var texture))
            {
                continue;
            }

            AppendTextureSplineField(splines, texture, program, columnLodStride, axisLodStride);
        }

        return splines.Count == 0
            ? AquariumSplineFrame.Empty
            : new AquariumSplineFrame { Splines = splines };
    }

    private static void AppendTextureSplineField(
        List<AquariumSpline3D> splines,
        AquariumTextureFieldBinding texture,
        AquariumTextureSplineFieldProgram program,
        int columnLodStride,
        int axisLodStride)
    {
        var axisSamples = program.FrequencyAxis == AquariumTextureAxis.X ? texture.Width : texture.Height;
        if (axisSamples <= 1 || texture.Channels <= 0)
        {
            return;
        }

        var columnCount = Math.Clamp(program.ColumnCount, 1, program.FrequencyAxis == AquariumTextureAxis.X ? texture.Height : texture.Width);
        var style = new AquariumSplineStyle(
            program.Appearance.Radius,
            MathF.Max(MathF.Max(program.Appearance.Emission.X, MathF.Max(program.Appearance.Emission.Y, program.Appearance.Emission.Z)) * program.Appearance.Emission.W, 1.0f),
            program.Appearance.Alpha,
            program.Appearance.ZeroThreshold,
            program.Appearance.Feather);
        var columnStride = Math.Max(1, columnLodStride);
        var frequencyStride = Math.Max(1, axisLodStride);
        var pointCount = Math.Max(2, ((axisSamples - 1) / frequencyStride) + 1);
        for (var column = 0; column < columnCount; column += columnStride)
        {
            var vertices = new AquariumSplineVertex[pointCount];
            var hasContribution = false;
            for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                var frequencyIndex = Math.Min(axisSamples - 1, pointIndex * frequencyStride);
                var textureColumn = program.FirstColumn + column * Math.Max(1, program.ColumnStride);
                if (program.RollingWindowModulo > 0)
                {
                    textureColumn = PositiveModulo(textureColumn + texture.RollingOffset, program.RollingWindowModulo);
                }

                var x = program.FrequencyAxis == AquariumTextureAxis.X ? frequencyIndex : textureColumn;
                var y = program.FrequencyAxis == AquariumTextureAxis.X ? textureColumn : frequencyIndex;
                if (texture.RollingMode == AquariumRollingModuloMode.Columns)
                {
                    x = PositiveModulo(x + texture.RollingOffset, texture.Width);
                }
                else if (texture.RollingMode == AquariumRollingModuloMode.Rows)
                {
                    y = PositiveModulo(y + texture.RollingOffset, texture.Height);
                }

                x = Math.Clamp(x, 0, texture.Width - 1);
                y = Math.Clamp(y, 0, texture.Height - 1);
                var sample = Sample(texture, x, y);
                hasContribution |= sample > program.ProbePolicy.MinimumVisualContribution;
                var position = program.Origin +
                    program.AxisStep * frequencyIndex +
                    TextureSplineColumnOffset(program, column) +
                    new Vector3(0.0f, sample * program.AmplitudeScale, 0.0f);
                var color = Vector4.Lerp(
                    new Vector4(0.08f, 0.18f, 0.12f, 0.12f),
                    program.Appearance.Emission,
                    Math.Clamp(sample, 0.0f, 1.0f));
                color.W = Math.Clamp(0.16f + sample * program.Appearance.Alpha, 0.0f, 1.0f);
                vertices[pointIndex] = new AquariumSplineVertex(position, color);
            }

            if (hasContribution)
            {
                splines.Add(new AquariumSpline3D(
                    $"{program.Id}/column/{column}",
                    vertices,
                    style,
                    Math.Clamp(program.Subdivisions, 1, 16)));
            }
        }
    }

    private static Vector3 TextureSplineColumnOffset(AquariumTextureSplineFieldProgram program, int column)
    {
        if (program.ColumnGroupSize <= 0)
        {
            return program.ColumnStep * column;
        }

        var groupSize = Math.Max(1, program.ColumnGroupSize);
        return program.ColumnStep * (column % groupSize) +
            program.ColumnGroupStep * (column / groupSize);
    }

    private static AquariumSplineFrame MergeSplineFrames(AquariumSplineFrame first, AquariumSplineFrame second)
    {
        if (!first.HasInput)
        {
            return second;
        }

        if (!second.HasInput)
        {
            return first;
        }

        return new AquariumSplineFrame { Splines = first.Splines.Concat(second.Splines).ToArray() };
    }

    private static float Sample(AquariumTextureFieldBinding texture, int x, int y)
    {
        var index = (y * texture.Width + x) * texture.Channels;
        return index >= 0 && index < texture.Samples.Count ? texture.Samples[index] : 0.0f;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        if (modulus <= 0)
        {
            return value;
        }

        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static float StableFieldId(string key, float familyBase, int familyRange)
    {
        var hash = 2166136261u;
        foreach (var character in key)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return familyBase + hash % (uint)Math.Max(1, familyRange);
    }

    private static float[] FlattenTextureSamples(AquariumBufferFieldFrame frame)
    {
        if (!frame.HasInput || frame.Textures.Count == 0)
        {
            return [];
        }

        var samples = new float[frame.Textures.Sum(texture => texture.Samples.Count)];
        var offset = 0;
        foreach (var texture in frame.Textures)
        {
            for (var index = 0; index < texture.Samples.Count; index++)
            {
                samples[offset + index] = texture.Samples[index];
            }

            offset += texture.Samples.Count;
        }

        return samples;
    }

    private static float SceneFlags(AquariumSceneState scene)
    {
        var flags = 0;
        if (scene.TraceHeightFieldSurface)
        {
            flags |= 1;
        }

        if (scene.UseStarfieldBackground)
        {
            flags |= 2;
        }

        if (scene.UseStudioBackground)
        {
            flags |= 4;
        }

        return flags;
    }

    private void UpdateVisibleFrameRate(long frameTimestamp)
    {
        if (lastVisibleFrameTimestamp == 0)
        {
            lastVisibleFrameTimestamp = frameTimestamp;
            return;
        }

        var frameMilliseconds = (frameTimestamp - lastVisibleFrameTimestamp) * 1000.0 / Stopwatch.Frequency;
        lastVisibleFrameTimestamp = frameTimestamp;
        if (!double.IsFinite(frameMilliseconds) || frameMilliseconds <= 0.0)
        {
            return;
        }

        smoothedVisibleFrameMilliseconds = smoothedVisibleFrameMilliseconds <= 0.0
            ? frameMilliseconds
            : smoothedVisibleFrameMilliseconds * 0.90 + frameMilliseconds * 0.10;
        var fps = 1000.0 / Math.Max(smoothedVisibleFrameMilliseconds, 0.001);
        visibleFrameRateText = $"FPS {fps:0.0}  {smoothedVisibleFrameMilliseconds:0.0} ms";
    }

    private void SignalFrame(FrameResources frameResources)
    {
        var signalValue = ++fenceValue;
        commandQueue.Signal(fence, signalValue).CheckError();
        frameResources.FenceValue = signalValue;
    }

    private void BeginGpuTiming(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources, D3D12GpuTimingPass pass)
    {
        var passIndex = (int)pass;
        activeCommandList.EndQuery(frameResources.GpuTimingQueryHeap, QueryType.Timestamp, (uint)(passIndex * 2));
        frameResources.GpuTimingPassMask |= 1u << passIndex;
    }

    private void EndGpuTiming(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources, D3D12GpuTimingPass pass)
    {
        activeCommandList.EndQuery(frameResources.GpuTimingQueryHeap, QueryType.Timestamp, (uint)(((int)pass * 2) + 1));
    }

    private void ResolveGpuTimings(ID3D12GraphicsCommandList activeCommandList, FrameResources frameResources, bool eligibleForAccumulation)
    {
        activeCommandList.ResolveQueryData(
            frameResources.GpuTimingQueryHeap,
            QueryType.Timestamp,
            0,
            GpuTimingQueryCount,
            frameResources.GpuTimingReadback,
            0);
        frameResources.GpuTimingPendingPassMask = frameResources.GpuTimingPassMask;
        frameResources.GpuTimingEligibleForAccumulation = eligibleForAccumulation;
        frameResources.GpuTimingPassMask = 0;
    }

    private unsafe void AccumulateCompletedGpuTimings(FrameResources frameResources)
    {
        if (frameResources.GpuTimingPendingPassMask == 0)
        {
            return;
        }

        if (frameResources.GpuTimingEligibleForAccumulation && gpuTimestampFrequency > 0)
        {
            var timestamps = (ulong*)frameResources.GpuTimingReadback.Map<byte>(0);
            try
            {
                for (var passIndex = 0; passIndex < (int)D3D12GpuTimingPass.Count; passIndex++)
                {
                    if ((frameResources.GpuTimingPendingPassMask & (1u << passIndex)) == 0)
                    {
                        continue;
                    }

                    var start = timestamps[passIndex * 2];
                    var end = timestamps[(passIndex * 2) + 1];
                    if (end < start)
                    {
                        continue;
                    }

                    accumulatedGpuTimingMilliseconds[passIndex] += (end - start) * 1000.0 / gpuTimestampFrequency;
                    accumulatedGpuTimingPassFrames[passIndex]++;
                }
            }
            finally
            {
                frameResources.GpuTimingReadback.Unmap(0);
            }
        }

        frameResources.GpuTimingPendingPassMask = 0;
        frameResources.GpuTimingEligibleForAccumulation = false;
    }

    private float lastCapacityReportSecond = -1.0f;

    private void ReportCapacityOncePerSecond(float timeSeconds, FrameResources frameResources)
    {
        if (timeSeconds - lastCapacityReportSecond < 1.0f)
        {
            return;
        }

        lastCapacityReportSecond = timeSeconds;
        Console.WriteLine(
            $"D3D12 capacity: {frameResources.UploadRing.Describe()}; " +
            $"{frameResources.TransientShaderDescriptors.Describe()}; " +
            $"{staticShaderDescriptorArena.Describe()}; " +
            $"{renderTargetViewArena.Describe()}");
        if (activeFractalReservoirField.HasInput && fractalSplatBuffer is not null && fractalSdfReservoirBuffer is not null && fractalPbrReservoirBuffer is not null && fractalRadiosityReservoirBuffer is not null && fractalFlameStateBuffer is not null)
        {
            var residentBytes =
                fractalSplatBuffer.SizeBytes +
                fractalSdfReservoirBuffer.SizeBytes +
                fractalPbrReservoirBuffer.SizeBytes +
                fractalRadiosityReservoirBuffer.SizeBytes +
                fractalFlameStateBuffer.SizeBytes;
            Console.WriteLine(
                $"D3D12 fractal reservoirs: splats {activeFractalReservoirField.SplatCount:N0}; " +
                $"visible {visibleFractalSplatCount:N0}; " +
                $"program transforms {activeFractalProgramTransforms.Length:N0}; " +
                $"splat updates/frame {activeFractalReservoirField.SplatUpdatesPerFrame:N0}; " +
                $"updates/pass {activeFractalReservoirField.ReservoirUpdatesPerPass:N0}; " +
                $"candidates/update {activeFractalReservoirField.CandidatesPerReservoirUpdate}; " +
                $"resident {residentBytes / (1024.0 * 1024.0):0.0} MiB");
        }

        if (activeFieldEvidenceFrame.HasInput)
        {
            Console.WriteLine(
                $"D3D12 field evidence: domains {activeFieldEvidenceFrame.Domains.Count:N0}; " +
                $"resources {activeFieldEvidenceFrame.Resources.Count:N0}; " +
                $"resolved resources {activeFieldResourceStats.Resolved:N0}; " +
                $"structured buffers {activeFieldResourceStats.StructuredBuffers:N0}; " +
                $"texture2d {activeFieldResourceStats.Texture2D:N0}; " +
                $"surface pages {activeFieldResourceStats.SurfacePages:N0}; " +
                $"volume textures {activeFieldResourceStats.VolumeTextures:N0}; " +
                $"meshes {activeFieldResourceStats.Meshes:N0}; " +
                $"unsupported resources {activeFieldResourceStats.Unsupported:N0}; " +
                $"claims {activeFieldEvidenceFrame.Claims.Count:N0}; " +
                $"candidates {activeFieldEvidenceFrame.Candidates.Count:N0}; " +
                $"producer packets {activeFieldEvidenceFrame.BackendPackets.Count:N0}; " +
                $"planned packets {activeFieldLoweringPlan.Packets.Count:N0}; " +
                $"deferred {activeFieldLoweringPlan.DeferredRequests.Count:N0}; " +
                $"issues {activeFieldEvidenceValidation.Issues.Count:N0}");
        }

        if (activeFieldEvidenceFrame.TubeSplineLowerings.Count > 0)
        {
            Console.WriteLine(
                $"D3D12 tube fields: lowerings {activeFieldEvidenceFrame.TubeSplineLowerings.Count:N0}; " +
                $"segments requested {activeTubeFieldRequestedSegments:N0}; " +
                $"dispatched {activeTubeFieldDispatchedSegments:N0}; " +
                $"truncated {activeTubeFieldTruncatedSegments:N0}; " +
                $"indirect draws {activeTubeFieldIndirectDrawCount:N0}; " +
                $"skipped draw batches {activeTubeFieldSkippedDrawBatches:N0}; " +
                $"unplanned lowerings {activeTubeFieldUnplannedLowerings:N0}; " +
                $"invalid columns {activeTubeFieldInvalidColumns:N0}; " +
                $"resource uploads {activeFieldResourceUploadCount:N0}; " +
                $"skipped uploads {activeFieldResourceUploadSkippedCount:N0}; " +
                $"index draw count {activeTubeFieldDrawIndexCount:N0}; " +
                $"segment budget {MaxTubeFieldSegments:N0}");
        }

        if (activeFieldEvidenceFrame.StereoDepthLowerings.Count > 0)
        {
            Console.WriteLine(
                $"D3D12 stereo depth: lowerings {activeFieldEvidenceFrame.StereoDepthLowerings.Count:N0}; " +
                $"dispatch-ready {activeStereoDepthDispatchReadyLowerings:N0}; " +
                $"dispatched {activeStereoDepthDispatchedLowerings:N0}; " +
                $"unplanned {activeStereoDepthUnplannedLowerings:N0}; " +
                $"unresolved {activeStereoDepthUnresolvedLowerings:N0}; " +
                "kernel packed-block-match");
        }

        if (gpuSensorTextureCount > 0 ||
            gpuSensorFieldUnsupportedTextureCount > 0)
        {
            Console.WriteLine(
                $"D3D12 GPU sensor fusion: cameras {gpuSensorCameraCount:N0}; " +
                $"textures {gpuSensorTextureCount:N0}; " +
                $"field textures {gpuSensorFieldTextureCount:N0}; " +
                $"unsupported field textures {gpuSensorFieldUnsupportedTextureCount:N0}; " +
                $"samples/texture {GpuSensorSamplesPerTexture:N0}; " +
                $"generated gaussians {(temporalGaussiansGpuGenerated ? temporalGaussianCount : 0):N0}");
        }

        if (activePointCloudGeneratedMeshes > 0 ||
            activePointCloudRenderedMeshes > 0 ||
            activePointCloudUnresolvedMeshes > 0 ||
            activePointCloudUnplannedMeshes > 0)
        {
            Console.WriteLine(
                $"D3D12 point clouds: generated {activePointCloudGeneratedMeshes:N0}; " +
                $"rendered {activePointCloudRenderedMeshes:N0}; " +
                $"unresolved {activePointCloudUnresolvedMeshes:N0}; " +
                $"unplanned {activePointCloudUnplannedMeshes:N0}");
        }

        if (accumulatedTimingFrames > 0)
        {
            var scale = 1.0 / accumulatedTimingFrames;
            Console.WriteLine(
                $"D3D12 CPU timing avg over {accumulatedTimingFrames} frames: " +
                $"frame {accumulatedFrameCpuMilliseconds * scale:0.###} ms; " +
                $"record {accumulatedRecordCpuMilliseconds * scale:0.###} ms; " +
                $"overlay {accumulatedOverlayCpuMilliseconds * scale:0.###} ms");
            accumulatedFrameCpuMilliseconds = 0.0;
            accumulatedRecordCpuMilliseconds = 0.0;
            accumulatedOverlayCpuMilliseconds = 0.0;
            accumulatedTimingFrames = 0;
        }

        if (accumulatedGpuTimingPassFrames[(int)D3D12GpuTimingPass.FrameRecord] > 0)
        {
            Console.WriteLine(
                "D3D12 GPU timing avg: " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.FrameRecord, "record")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.GpuSensorFusion, "sensor-fusion")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.StereoDepthUpdate, "stereo-depth")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.FractalReservoirUpdate, "fractal-reservoir")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.TubeFieldUpdate, "tube-field")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.HeightField, "height-field")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.SceneCandidate, "scene-candidate")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.FieldReservoirResolve, "reservoir-resolve")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.ReservoirHistoryUpdate, "reservoir-update")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.Bloom, "bloom")}; " +
                $"{FormatGpuTiming(D3D12GpuTimingPass.PresentationResolve, "presentation")}");
            Array.Clear(accumulatedGpuTimingMilliseconds);
            Array.Clear(accumulatedGpuTimingPassFrames);
        }
    }

    private string FormatGpuTiming(D3D12GpuTimingPass pass, string label)
    {
        var passIndex = (int)pass;
        var frames = accumulatedGpuTimingPassFrames[passIndex];
        return frames > 0
            ? $"{label} {accumulatedGpuTimingMilliseconds[passIndex] / frames:0.###} ms/{frames}"
            : $"{label} n/a";
    }

    private static double ElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    private static string ResolveShaderSourceRoot(string? shaderPath, AquariumShaderManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(shaderPath))
        {
            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(shaderPath));
            if (!string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return sourceDirectory;
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.ShaderRoot))
        {
            return Path.GetFullPath(manifest.ShaderRoot);
        }

        return Path.Combine(AppContext.BaseDirectory, "Render", "Shaders");
    }

    private void CaptureShaderWriteTimes()
    {
        shaderWriteTimesUtc.Clear();
        foreach (var path in shaderPaths.All)
        {
            shaderWriteTimesUtc[path] = File.GetLastWriteTimeUtc(path);
        }
    }

    private DateTime LatestShaderWriteTimeUtc()
    {
        var latest = DateTime.MinValue;
        foreach (var path in shaderPaths.All)
        {
            var writeTime = File.GetLastWriteTimeUtc(path);
            if (writeTime > latest)
            {
                latest = writeTime;
            }
        }

        return latest;
    }

    private bool ShaderSourcesChanged(out DateTime latestWriteTimeUtc)
    {
        latestWriteTimeUtc = DateTime.MinValue;
        foreach (var path in shaderPaths.All)
        {
            var writeTime = File.GetLastWriteTimeUtc(path);
            if (writeTime > latestWriteTimeUtc)
            {
                latestWriteTimeUtc = writeTime;
            }

            if (!shaderWriteTimesUtc.TryGetValue(path, out var previousWriteTime) || writeTime > previousWriteTime)
            {
                return true;
            }
        }

        return false;
    }

    private void TryHotReloadShaders()
    {
        var now = shaderReloadClock.Elapsed;
        if (now - lastShaderReloadCheck < ShaderReloadPollInterval || pipelineBuildTask is { IsCompleted: false })
        {
            return;
        }

        lastShaderReloadCheck = now;
        try
        {
            if (!ShaderSourcesChanged(out var latestWriteTimeUtc) || DateTime.UtcNow - latestWriteTimeUtc < ShaderReloadWriteSettleTime)
            {
                return;
            }

            StartPipelineBuild("hot reload");
            shaderReloadFailureReported = false;
        }
        catch (Exception error)
        {
            if (!shaderReloadFailureReported)
            {
                Console.Error.WriteLine($"D3D12 shader hot reload cannot stat sources in {shaderSourceRoot}: {error.Message}");
                shaderReloadFailureReported = true;
            }
        }
    }

    private void StartPipelineBuild(string reason)
    {
        if (pipelineBuildTask is { IsCompleted: false })
        {
            return;
        }

        var paths = shaderPaths;
        LatestShaderWriteTimeUtc();
        pipelineBuildInProgressReported = false;
        pipelineBuildTask = Task.Run(() => CreatePipelineSet(paths));
        Console.WriteLine($"D3D12 shader pipeline build started ({reason}): {shaderSourceRoot}");
    }

    private void ApplyCompletedPipelineBuild()
    {
        var task = pipelineBuildTask;
        if (task is null || !task.IsCompleted)
        {
            if (!PipelinesReady && !pipelineBuildInProgressReported)
            {
                Console.WriteLine("D3D12 shader pipeline build still running; waiting behind startup splash.");
                pipelineBuildInProgressReported = true;
            }

            return;
        }

        pipelineBuildTask = null;
        if (task.IsFaulted || task.IsCanceled)
        {
            var error = task.Exception?.GetBaseException() ?? new InvalidOperationException("D3D12 shader pipeline build was canceled.");
            if (PipelinesReady)
            {
                Console.Error.WriteLine($"D3D12 shader hot reload failed; keeping previous pipelines. {error}");
            }
            else
            {
                Console.Error.WriteLine($"D3D12 initial shader pipeline build failed; retrying after source change. {error}");
                throw new InvalidOperationException("D3D12 initial shader pipeline build failed.", error);
            }

            shaderReloadFailureReported = true;
            return;
        }

        var replacement = task.Result;
        var oldPipelines = CapturePipelineSetOrNull();
        if (oldPipelines is not null)
        {
            WaitForGpu();
        }

        ApplyPipelineSet(replacement);
        CaptureShaderWriteTimes();
        oldPipelines?.Dispose();
        shaderReloadFailureReported = false;
        pipelineBuildInProgressReported = false;
        if (initialPipelineReadyReported)
        {
            Console.WriteLine($"D3D12 shader hot reload applied: {shaderSourceRoot}");
        }
        else
        {
            Console.WriteLine($"D3D12 shader pipelines ready: {shaderSourceRoot}");
            initialPipelineReadyReported = true;
        }
    }

    private D3D12PipelineSet? CapturePipelineSetOrNull()
    {
        return PipelinesReady
            ? new D3D12PipelineSet(
                heightFieldBasePipelineState!,
                heightFieldBrushPipelineState!,
                scenePipelineState!,
                splinePipelineState!,
                temporalGaussianPipelineState!,
                gpuSensorFusionPipelineState!,
                stereoDepthPipelineState!,
                pointCloudComputePipelineState!,
                pointCloudRenderPipelineState!,
                fractalSurfaceSplatRenderPipelineState!,
                fractalTransparentSplatRenderPipelineState!,
                fractalSplatPipelineState!,
                fractalSdfReservoirPipelineState!,
                fractalPbrReservoirPipelineState!,
                fractalRadiosityReservoirPipelineState!,
                tubeFieldComputePipelineState!,
                tubeFieldRenderPipelineState!,
                bokushoBrushPipelineState!,
                fieldReservoirResolvePipelineState!,
                sdfProxyPipelineStates.Select(pipeline => pipeline!).ToArray(),
                bloomPrefilterPipelineState!,
                bloomDownsamplePipelineState!,
                bloomBlurHorizontalPipelineState!,
                bloomBlurVerticalPipelineState!,
                reservoirHistoryUpdatePipelineState,
                resolvePipelineState!)
            : null;
    }

    private void ApplyPipelineSet(D3D12PipelineSet pipelines)
    {
        heightFieldBasePipelineState = pipelines.HeightFieldBase;
        heightFieldBrushPipelineState = pipelines.HeightFieldBrush;
        scenePipelineState = pipelines.Scene;
        splinePipelineState = pipelines.Spline;
        temporalGaussianPipelineState = pipelines.TemporalGaussian;
        gpuSensorFusionPipelineState = pipelines.GpuSensorFusion;
        stereoDepthPipelineState = pipelines.StereoDepth;
        pointCloudComputePipelineState = pipelines.PointCloudCompute;
        pointCloudRenderPipelineState = pipelines.PointCloudRender;
        fractalSurfaceSplatRenderPipelineState = pipelines.FractalSurfaceSplatRender;
        fractalTransparentSplatRenderPipelineState = pipelines.FractalTransparentSplatRender;
        fractalSplatPipelineState = pipelines.FractalSplat;
        fractalSdfReservoirPipelineState = pipelines.FractalSdfReservoir;
        fractalPbrReservoirPipelineState = pipelines.FractalPbrReservoir;
        fractalRadiosityReservoirPipelineState = pipelines.FractalRadiosityReservoir;
        tubeFieldComputePipelineState = pipelines.TubeFieldCompute;
        tubeFieldRenderPipelineState = pipelines.TubeFieldRender;
        bokushoBrushPipelineState = pipelines.BokushoBrush;
        fieldReservoirResolvePipelineState = pipelines.FieldReservoirResolve;
        for (var index = 0; index < sdfProxyPipelineStates.Length; index++)
        {
            sdfProxyPipelineStates[index] = pipelines.SdfProxies[index];
        }
        bloomPrefilterPipelineState = pipelines.BloomPrefilter;
        bloomDownsamplePipelineState = pipelines.BloomDownsample;
        bloomBlurHorizontalPipelineState = pipelines.BloomBlurHorizontal;
        bloomBlurVerticalPipelineState = pipelines.BloomBlurVertical;
        reservoirHistoryUpdatePipelineState = pipelines.ReservoirHistoryUpdate;
        resolvePipelineState = pipelines.Resolve;
    }

    private void DisposePipelineStates()
    {
        CapturePipelineSetOrNull()?.Dispose();
        heightFieldBasePipelineState = null;
        heightFieldBrushPipelineState = null;
        scenePipelineState = null;
        temporalGaussianPipelineState = null;
        gpuSensorFusionPipelineState = null;
        stereoDepthPipelineState = null;
        pointCloudComputePipelineState = null;
        pointCloudRenderPipelineState = null;
        fractalSurfaceSplatRenderPipelineState = null;
        fractalTransparentSplatRenderPipelineState = null;
        fractalSplatPipelineState = null;
        fractalSdfReservoirPipelineState = null;
        fractalPbrReservoirPipelineState = null;
        fractalRadiosityReservoirPipelineState = null;
        tubeFieldComputePipelineState = null;
        tubeFieldRenderPipelineState = null;
        bokushoBrushPipelineState = null;
        Array.Clear(sdfProxyPipelineStates);
        bloomPrefilterPipelineState = null;
        bloomDownsamplePipelineState = null;
        bloomBlurHorizontalPipelineState = null;
        bloomBlurVerticalPipelineState = null;
        reservoirHistoryUpdatePipelineState = null;
        resolvePipelineState = null;
    }

    private void WaitForFrame(FrameResources frameResources)
    {
        if (frameResources.FenceValue != 0 && fence.CompletedValue < frameResources.FenceValue)
        {
            fence.SetEventOnCompletion(frameResources.FenceValue, fenceEvent.SafeWaitHandle.DangerousGetHandle()).CheckError();
            fenceEvent.WaitOne();
        }
    }

    private void WaitForGpu()
    {
        var signalValue = ++fenceValue;
        commandQueue.Signal(fence, signalValue).CheckError();
        fence.SetEventOnCompletion(signalValue, fenceEvent.SafeWaitHandle.DangerousGetHandle()).CheckError();
        fenceEvent.WaitOne();
    }

    private static int AlignTo(int value, int alignment)
    {
        return (value + alignment - 1) & ~(alignment - 1);
    }

    private static void ReportStartupProgress(Action<string>? startupProgress, string message)
    {
        startupProgress?.Invoke(message);
        Console.WriteLine(message);
    }

    private D3D12DescriptorArena CreateRenderTargetViewArena()
    {
        return new D3D12DescriptorArena(
            device,
            DescriptorHeapType.RenderTargetView,
            64,
            DescriptorHeapFlags.None,
            "Aquarium D3D12 RTV Arena");
    }

    private D3D12DescriptorArena CreateDepthStencilViewArena()
    {
        return new D3D12DescriptorArena(
            device,
            DescriptorHeapType.DepthStencilView,
            8,
            DescriptorHeapFlags.None,
            "Aquarium D3D12 DSV Arena");
    }

    private D3D12DescriptorArena CreateStaticShaderDescriptorArena()
    {
        return new D3D12DescriptorArena(
            device,
            DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
            64,
            DescriptorHeapFlags.ShaderVisible,
            "Aquarium D3D12 Static Shader Descriptor Arena");
    }

    private D3D12DescriptorArena CreateCpuShaderDescriptorArena()
    {
        return new D3D12DescriptorArena(
            device,
            DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
            16,
            DescriptorHeapFlags.None,
            "Aquarium D3D12 CPU Shader Descriptor Arena");
    }

    private ID3D12RootSignature CreateFullscreenRootSignature()
    {
        var constantBufferRange = new DescriptorRange(
            DescriptorRangeType.ConstantBufferView,
            1,
            0,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var sourceTextureRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            0,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var heightFieldBrushRange = new DescriptorRange(
            DescriptorRangeType.ConstantBufferView,
            1,
            1,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var sdfLightRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            12,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var bloomRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            BloomLevelCount,
            29,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var currentSceneMetadataRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            5,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var currentSceneControlRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            7,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var currentReservoirGuideRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            26,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var studioPmremRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            22,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var studioIrradianceRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            23,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var sdfObjectRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            24,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var temporalGaussianRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            25,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var blueNoiseRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            28,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var reservoirResolvedOutputRange = new DescriptorRange(
            DescriptorRangeType.UnorderedAccessView,
            1,
            23,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var tubeFieldReplaySourceRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            MaxTubeFieldReplaySources,
            60,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var sceneOverdrawRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            76,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var rootParameters = new[]
        {
            new RootParameter(new RootDescriptorTable([constantBufferRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([sourceTextureRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([heightFieldBrushRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([sdfLightRange]), ShaderVisibility.Pixel),
            new RootParameter(new RootDescriptorTable([bloomRange]), ShaderVisibility.Pixel),
            new RootParameter(new RootDescriptorTable([currentSceneMetadataRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([currentSceneControlRange]), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(51, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(22, 0), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([studioPmremRange]), ShaderVisibility.Pixel),
            new RootParameter(new RootDescriptorTable([studioIrradianceRange]), ShaderVisibility.Pixel),
            new RootParameter(new RootDescriptorTable([sdfObjectRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([temporalGaussianRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([currentReservoirGuideRange]), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(38, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(39, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(40, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(41, 0), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([blueNoiseRange]), ShaderVisibility.Pixel),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(45, 0), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([reservoirResolvedOutputRange]), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(59, 0), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([tubeFieldReplaySourceRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([sceneOverdrawRange]), ShaderVisibility.Pixel),
            new RootParameter(RootParameterType.ConstantBufferView, new RootDescriptor(5, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(77, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(78, 0), ShaderVisibility.All),
        };
        var staticSamplers = new[]
        {
            new StaticSamplerDescription(
                0,
                Filter.MinMagMipLinear,
                TextureAddressMode.Clamp,
                TextureAddressMode.Clamp,
                TextureAddressMode.Clamp,
                0.0f,
                1,
                ComparisonFunction.Never,
                StaticBorderColor.TransparentBlack,
                0.0f,
                float.MaxValue,
                ShaderVisibility.All,
                0),
        };
        var description = new RootSignatureDescription(
            RootSignatureFlags.AllowInputAssemblerInputLayout,
            rootParameters,
            staticSamplers);
        return device.CreateRootSignature(0, in description, RootSignatureVersion.Version1);
    }

    private ID3D12RootSignature CreateGpuSensorFusionRootSignature()
    {
        var frameRange = new DescriptorRange(
            DescriptorRangeType.ConstantBufferView,
            1,
            0,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var seedRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            26,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var sensorCameraRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            27,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var sensorTextureRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            MaxGpuSensorTextureCount,
            28,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var acousticConstraintRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            36,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var outputRange = new DescriptorRange(
            DescriptorRangeType.UnorderedAccessView,
            1,
            0,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var nativePointRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            37,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var rootParameters = new[]
        {
            new RootParameter(new RootDescriptorTable([frameRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([seedRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([sensorCameraRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([sensorTextureRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([acousticConstraintRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([outputRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([nativePointRange]), ShaderVisibility.All),
        };
        var description = new RootSignatureDescription(
            RootSignatureFlags.None,
            rootParameters,
            []);
        return device.CreateRootSignature(0, in description, RootSignatureVersion.Version1);
    }

    private ID3D12RootSignature CreatePointCloudRootSignature()
    {
        var disparityRange = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            0,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var frameConstantsRange = new DescriptorRange(
            DescriptorRangeType.ConstantBufferView,
            1,
            0,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var vertexRange = new DescriptorRange(
            DescriptorRangeType.UnorderedAccessView,
            1,
            0,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var indexRange = new DescriptorRange(
            DescriptorRangeType.UnorderedAccessView,
            1,
            1,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var splatRange = new DescriptorRange(
            DescriptorRangeType.UnorderedAccessView,
            1,
            2,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var sdfReservoirRange = new DescriptorRange(
            DescriptorRangeType.UnorderedAccessView,
            1,
            3,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var pbrReservoirRange = new DescriptorRange(
            DescriptorRangeType.UnorderedAccessView,
            1,
            4,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var radiosityReservoirRange = new DescriptorRange(
            DescriptorRangeType.UnorderedAccessView,
            1,
            5,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var rootParameters = new[]
        {
            new RootParameter(new RootDescriptorTable([frameConstantsRange]), ShaderVisibility.All),
            new RootParameter(RootParameterType.ConstantBufferView, new RootDescriptor(1, 0), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([disparityRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([vertexRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([indexRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([splatRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([sdfReservoirRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([pbrReservoirRange]), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([radiosityReservoirRange]), ShaderVisibility.All),
        };
        var description = new RootSignatureDescription(
            RootSignatureFlags.None,
            rootParameters,
            []);
        return device.CreateRootSignature(0, in description, RootSignatureVersion.Version1);
    }

    private ID3D12RootSignature CreateFractalReservoirRootSignature()
    {
        var rootParameters = new[]
        {
            new RootParameter(new RootConstants(0, 0, 13), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(0, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(1, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(2, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(3, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(4, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(0, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(1, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(2, 0), ShaderVisibility.All),
        };
        var description = new RootSignatureDescription(
            RootSignatureFlags.None,
            rootParameters,
            []);
        return device.CreateRootSignature(0, in description, RootSignatureVersion.Version1);
    }

    private ID3D12RootSignature CreateTubeFieldRootSignature()
    {
        var rampTexture = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            43,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var rootParameters = new[]
        {
            new RootParameter(RootParameterType.ConstantBufferView, new RootDescriptor(3, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(42, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(10, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(11, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(12, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(13, 0), ShaderVisibility.All),
            new RootParameter(new RootDescriptorTable([rampTexture]), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(16, 0), ShaderVisibility.All),
        };
        var staticSamplers = new[]
        {
            new StaticSamplerDescription(
                0,
                Filter.MinMagMipLinear,
                TextureAddressMode.Clamp,
                TextureAddressMode.Clamp,
                TextureAddressMode.Clamp,
                0.0f,
                1,
                ComparisonFunction.Never,
                StaticBorderColor.TransparentBlack,
                0.0f,
                float.MaxValue,
                ShaderVisibility.All,
                0),
        };
        var description = new RootSignatureDescription(
            RootSignatureFlags.None,
            rootParameters,
            staticSamplers);
        return device.CreateRootSignature(0, in description, RootSignatureVersion.Version1);
    }

    private ID3D12RootSignature CreateBokushoBrushRootSignature()
    {
        var rootParameters = new[]
        {
            new RootParameter(RootParameterType.ConstantBufferView, new RootDescriptor(4, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(20, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(21, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(78, 0), ShaderVisibility.All),
        };
        var description = new RootSignatureDescription(
            RootSignatureFlags.None,
            rootParameters,
            []);
        return device.CreateRootSignature(0, in description, RootSignatureVersion.Version1);
    }

    private ID3D12CommandSignature CreateGeneratedMeshDrawCommandSignature()
    {
        var arguments = new[]
        {
            new IndirectArgumentDescription
            {
                Type = IndirectArgumentType.DrawIndexed,
            },
        };
        var description = new CommandSignatureDescription
        {
            ByteStride = GeneratedMeshDrawArgumentBytes,
            IndirectArguments = arguments,
        };
        return device.CreateCommandSignature<ID3D12CommandSignature>(description, null);
    }

    private ID3D12RootSignature CreateTubeFieldRenderRootSignature()
    {
        var frameConstants = new DescriptorRange(
            DescriptorRangeType.ConstantBufferView,
            1,
            0,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var rampTexture = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            43,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var blueNoiseTexture = new DescriptorRange(
            DescriptorRangeType.ShaderResourceView,
            1,
            44,
            0,
            D3D12.DescriptorRangeOffsetAppend);
        var rootParameters = new[]
        {
            new RootParameter(new RootDescriptorTable([frameConstants]), ShaderVisibility.All),
            new RootParameter(RootParameterType.ConstantBufferView, new RootDescriptor(3, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(42, 0), ShaderVisibility.Pixel),
            new RootParameter(new RootDescriptorTable([rampTexture]), ShaderVisibility.Pixel),
            new RootParameter(new RootDescriptorTable([blueNoiseTexture]), ShaderVisibility.Pixel),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(14, 0), ShaderVisibility.Pixel),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(15, 0), ShaderVisibility.Pixel),
        };
        var staticSamplers = new[]
        {
            new StaticSamplerDescription(
                0,
                Filter.MinMagMipLinear,
                TextureAddressMode.Clamp,
                TextureAddressMode.Clamp,
                TextureAddressMode.Clamp,
                0.0f,
                1,
                ComparisonFunction.Never,
                StaticBorderColor.TransparentBlack,
                0.0f,
                float.MaxValue,
                ShaderVisibility.Pixel,
                0),
        };
        var description = new RootSignatureDescription(
            RootSignatureFlags.AllowInputAssemblerInputLayout,
            rootParameters,
            staticSamplers);
        return device.CreateRootSignature(0, in description, RootSignatureVersion.Version1);
    }

    private FrameResources CreateFrameResources(int index)
    {
        var commandAllocator = device.CreateCommandAllocator(CommandListType.Direct);
        commandAllocator.Name = $"Aquarium D3D12 Frame {index} Command Allocator";
        var uploadRing = new D3D12UploadRing(device, 160 * 1024 * 1024, $"Aquarium D3D12 Frame {index} Upload Ring");
        var transientDescriptors = new D3D12DescriptorArena(
            device,
            DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
            128,
            DescriptorHeapFlags.ShaderVisible,
            $"Aquarium D3D12 Frame {index} Transient Shader Descriptor Arena");
        var gpuTimingQueryHeap = device.CreateQueryHeap<ID3D12QueryHeap>(
            new QueryHeapDescription(QueryHeapType.Timestamp, GpuTimingQueryCount));
        gpuTimingQueryHeap.Name = $"Aquarium D3D12 Frame {index} GPU Timing Query Heap";
        var gpuTimingReadback = device.CreateCommittedResource(
            HeapType.Readback,
            ResourceDescription.Buffer((ulong)(GpuTimingQueryCount * sizeof(ulong))),
            ResourceStates.CopyDest,
            null);
        gpuTimingReadback.Name = $"Aquarium D3D12 Frame {index} GPU Timing Readback";

        return new FrameResources(commandAllocator, uploadRing, transientDescriptors, gpuTimingQueryHeap, gpuTimingReadback);
    }

    private ID3D12PipelineState CreateScenePipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "FullscreenTriangleVS", "D3D12ScenePS", SceneEvidenceRenderTargetFormats, CreateSceneEvidenceBlend(), enableDepth: true);
    }

    private ID3D12PipelineState CreateSplinePipelineState(string path)
    {
        var vertexShader = CompileShader(path, "D3D12SplineVS", "vs_5_0");
        var pixelShader = CompileShader(path, "D3D12SplinePS", "ps_5_0");
        var blend = BlendDescription.Opaque;
        blend.IndependentBlendEnable = true;
        for (var index = 1; index < 8; index++)
        {
            blend.RenderTarget[index] = new RenderTargetBlendDescription(
                false,
                false,
                Blend.One,
                Blend.Zero,
                BlendOperation.Add,
                Blend.One,
                Blend.Zero,
                BlendOperation.Add,
                LogicOp.Noop,
                index < 4 ? ColorWriteEnable.All : ColorWriteEnable.None);
        }
        blend.RenderTarget[4] = AdditiveOverdrawBlend();

        var description = new GraphicsPipelineStateDescription
        {
            RootSignature = fullscreenRootSignature,
            VertexShader = vertexShader,
            PixelShader = pixelShader,
            BlendState = blend,
            RasterizerState = RasterizerDescription.CullNone,
            DepthStencilState = new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.LessEqual,
                StencilEnable = false,
            },
            SampleMask = uint.MaxValue,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            InputLayout = new InputLayoutDescription(
            [
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32B32_Float, 12, 0),
                new InputElementDescription("TEXCOORD", 1, Format.R32G32B32_Float, 24, 0),
                new InputElementDescription("TEXCOORD", 2, Format.R32G32B32_Float, 36, 0),
                new InputElementDescription("TEXCOORD", 3, Format.R32G32B32_Float, 48, 0),
                new InputElementDescription("TEXCOORD", 4, Format.R32G32B32A32_Float, 60, 0),
                new InputElementDescription("TEXCOORD", 5, Format.R32G32B32A32_Float, 76, 0),
                new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 92, 0),
                new InputElementDescription("TEXCOORD", 6, Format.R32G32B32A32_Float, 108, 0),
            ]),
            RenderTargetFormats = SceneEvidenceRenderTargetFormats,
            SampleDescription = new SampleDescription(1, 0),
            DepthStencilFormat = SceneDepthFormat,
        };

        try
        {
            return device.CreateGraphicsPipelineState(description);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create D3D12 spline pipeline.", ex);
        }
    }

    private ID3D12PipelineState CreateSdfObjectProxyPipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "D3D12SdfObjectProxyVS", "D3D12SdfProxyPS", SceneEvidenceRenderTargetFormats, CreateSceneEvidenceBlend(), enableDepth: true);
    }

    private ID3D12PipelineState CreateTemporalGaussianPipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "D3D12TemporalGaussianVS", "D3D12TemporalGaussianPS", SceneEvidenceRenderTargetFormats, CreateSceneEvidenceBlend(), enableDepth: true);
    }

    private ID3D12PipelineState CreateFractalSurfaceSplatRenderPipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "D3D12FractalSplatVS", "D3D12FractalSurfaceSplatPS", SceneEvidenceRenderTargetFormats, CreateSceneEvidenceBlend(), enableDepth: true);
    }

    private ID3D12PipelineState CreateFractalTransparentSplatRenderPipelineState(string path)
    {
        return CreateFullscreenPipelineState(
            path,
            "D3D12FractalSplatVS",
            "D3D12FractalTransparentSplatPS",
            SceneEvidenceRenderTargetFormats,
            CreateTransparentFieldBlend(),
            enableDepth: false);
    }

    private ID3D12PipelineState CreateGpuSensorFusionPipelineState(string path)
    {
        var computeShader = CompileShader(path, "D3D12GpuSensorFusionCS", "cs_5_0");
        var description = new ComputePipelineStateDescription
        {
            RootSignature = gpuSensorFusionRootSignature,
            ComputeShader = computeShader,
        };
        return device.CreateComputePipelineState(description);
    }

    private ID3D12PipelineState CreateStereoDepthPipelineState(string path)
    {
        var computeShader = CompileShader(path, "D3D12PackedStereoDepthCS", "cs_5_0");
        var description = new ComputePipelineStateDescription
        {
            RootSignature = gpuSensorFusionRootSignature,
            ComputeShader = computeShader,
        };
        return device.CreateComputePipelineState(description);
    }

    private ID3D12PipelineState CreatePointCloudComputePipelineState(string path)
    {
        var computeShader = CompileShader(path, "D3D12PointCloudFromDisparityCS", "cs_5_0");
        var description = new ComputePipelineStateDescription
        {
            RootSignature = pointCloudRootSignature,
            ComputeShader = computeShader,
        };
        return device.CreateComputePipelineState(description);
    }

    private ID3D12PipelineState CreatePointCloudRenderPipelineState(string path)
    {
        var vertexShader = CompileShader(path, "D3D12PointCloudVS", "vs_5_0");
        var pixelShader = CompileShader(path, "D3D12PointCloudPS", "ps_5_0");
        var description = new GraphicsPipelineStateDescription
        {
            RootSignature = fullscreenRootSignature,
            VertexShader = vertexShader,
            PixelShader = pixelShader,
            BlendState = CreateSceneEvidenceBlend(),
            RasterizerState = RasterizerDescription.CullNone,
            DepthStencilState = new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.LessEqual,
                StencilEnable = false,
            },
            SampleMask = uint.MaxValue,
            PrimitiveTopologyType = PrimitiveTopologyType.Point,
            InputLayout = new InputLayoutDescription(
            [
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
                new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 32, 0),
            ]),
            RenderTargetFormats = SceneEvidenceRenderTargetFormats,
            SampleDescription = new SampleDescription(1, 0),
            DepthStencilFormat = SceneDepthFormat,
        };
        return device.CreateGraphicsPipelineState(description);
    }

    private ID3D12PipelineState CreateFractalReservoirPipelineState(string path, string entryPoint)
    {
        var computeShader = CompileShader(path, entryPoint, "cs_5_0");
        var description = new ComputePipelineStateDescription
        {
            RootSignature = fractalReservoirRootSignature,
            ComputeShader = computeShader,
        };
        return device.CreateComputePipelineState(description);
    }

    private ID3D12PipelineState CreateTubeFieldComputePipelineState(string path)
    {
        var computeShader = CompileShader(path, "D3D12TubeFieldExpandCS", "cs_5_0");
        var description = new ComputePipelineStateDescription
        {
            RootSignature = tubeFieldRootSignature,
            ComputeShader = computeShader,
        };
        return device.CreateComputePipelineState(description);
    }

    private ID3D12PipelineState CreateBokushoBrushPipelineState(string path)
    {
        var computeShader = CompileShader(path, "D3D12BokushoBrushCS", "cs_5_0");
        var description = new ComputePipelineStateDescription
        {
            RootSignature = bokushoBrushRootSignature,
            ComputeShader = computeShader,
        };
        return device.CreateComputePipelineState(description);
    }

    private ID3D12PipelineState CreateTubeFieldRenderPipelineState(string path)
    {
        var vertexShader = CompileShader(path, "D3D12TubeFieldVS", "vs_5_0");
        var pixelShader = CompileShader(path, "D3D12TubeFieldPS", "ps_5_0");
        var blend = CreateTubeFieldEvidenceBlend();

        var description = new GraphicsPipelineStateDescription
        {
            RootSignature = tubeFieldRenderRootSignature,
            VertexShader = vertexShader,
            PixelShader = pixelShader,
            BlendState = blend,
            RasterizerState = RasterizerDescription.CullNone,
            DepthStencilState = DepthStencilDescription.None,
            SampleMask = uint.MaxValue,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            InputLayout = new InputLayoutDescription(
            [
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32B32_Float, 12, 0),
                new InputElementDescription("TEXCOORD", 1, Format.R32G32B32_Float, 24, 0),
                new InputElementDescription("TEXCOORD", 2, Format.R32G32B32_Float, 36, 0),
                new InputElementDescription("TEXCOORD", 3, Format.R32G32B32_Float, 48, 0),
                new InputElementDescription("TEXCOORD", 4, Format.R32G32B32A32_Float, 60, 0),
                new InputElementDescription("TEXCOORD", 5, Format.R32G32B32A32_Float, 76, 0),
                new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 92, 0),
                new InputElementDescription("TEXCOORD", 6, Format.R32G32B32A32_Float, 108, 0),
                new InputElementDescription("TEXCOORD", 7, Format.R32G32B32A32_Float, 124, 0),
            ]),
            RenderTargetFormats = SceneEvidenceRenderTargetFormats,
            SampleDescription = new SampleDescription(1, 0),
        };

        return device.CreateGraphicsPipelineState(description);
    }

    private static BlendDescription CreateTubeFieldEvidenceBlend()
    {
        var blend = BlendDescription.Opaque;
        blend.IndependentBlendEnable = true;
        for (var index = 0; index < 8; index++)
        {
            blend.RenderTarget[index] = new RenderTargetBlendDescription(
                false,
                false,
                Blend.One,
                Blend.Zero,
                BlendOperation.Add,
                Blend.One,
                Blend.Zero,
                BlendOperation.Add,
                LogicOp.Noop,
                ColorWriteEnable.None);
        }
        blend.RenderTarget[4] = AdditiveOverdrawBlend();

        return blend;
    }

    private ID3D12PipelineState CreateBloomPrefilterPipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "FullscreenTriangleVS", "D3D12BloomPrefilterPS", SceneHdrFormat);
    }

    private ID3D12PipelineState CreateFieldReservoirResolvePipelineState(string path)
    {
        return CreateFullscreenPipelineState(
            path,
            "FullscreenTriangleVS",
            "D3D12FieldReservoirResolvePS",
            [SceneHdrFormat, SceneHdrFormat, SceneHdrFormat, SceneHdrFormat]);
    }

    private ID3D12PipelineState CreateBloomDownsamplePipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "FullscreenTriangleVS", "D3D12BloomDownsamplePS", SceneHdrFormat);
    }

    private ID3D12PipelineState CreateBloomBlurHorizontalPipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "FullscreenTriangleVS", "D3D12BloomBlurHorizontalPS", SceneHdrFormat);
    }

    private ID3D12PipelineState CreateBloomBlurVerticalPipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "FullscreenTriangleVS", "D3D12BloomBlurVerticalPS", SceneHdrFormat);
    }

    private ID3D12PipelineState CreateReservoirHistoryUpdatePipelineState(string path)
    {
        static void Step(string name)
        {
            if (Environment.GetEnvironmentVariable("AQUARIUM_PIPELINE_DIAG") == "1")
            {
                Console.WriteLine($"D3D12 pipeline build step: {name}");
            }
        }

        Step("reservoir-history-update compile");
        var computeShader = CompileShader(path, "D3D12ReservoirHistoryUpdateCS", "cs_5_0", skipOptimizationInDebug: false);
        Step("reservoir-history-update create-pso");
        var description = new ComputePipelineStateDescription
        {
            RootSignature = fullscreenRootSignature,
            ComputeShader = computeShader,
        };
        var pipelineState = device.CreateComputePipelineState(description);
        Step("reservoir-history-update done");
        return pipelineState;
    }

    private ID3D12PipelineState CreateResolvePipelineState(string path)
    {
        return CreateFullscreenPipelineState(
            path,
            "FullscreenTriangleVS",
            "D3D12ReservoirPresentationResolvePS",
            Format.B8G8R8A8_UNorm);
    }

    private ID3D12PipelineState CreateHeightFieldBasePipelineState(string path)
    {
        return CreateFullscreenPipelineState(path, "FullscreenTriangleVS", "D3D12HeightFieldBasePS", HeightFieldFormat);
    }

    private ID3D12PipelineState CreateHeightFieldBrushPipelineState(string path)
    {
        return CreateFullscreenPipelineState(
            path,
            "D3D12HeightFieldBrushVS",
            "D3D12HeightFieldBrushPS",
            HeightFieldFormat,
            new BlendDescription(Blend.One, Blend.One, Blend.One, Blend.One));
    }

    private static BlendDescription CreateTransparentFieldBlend()
    {
        var blend = BlendDescription.Opaque;
        blend.IndependentBlendEnable = true;
        blend.RenderTarget[0] = new RenderTargetBlendDescription(
            blendEnable: true,
            logicOpEnable: false,
            srcBlend: Blend.One,
            destBlend: Blend.One,
            blendOp: BlendOperation.Add,
            srcBlendAlpha: Blend.Zero,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOperation.Add,
            logicOp: LogicOp.Noop,
            renderTargetWriteMask: ColorWriteEnable.All);

        for (var index = 1; index < 8; index++)
        {
            blend.RenderTarget[index] = new RenderTargetBlendDescription(
                blendEnable: false,
                logicOpEnable: false,
                srcBlend: Blend.One,
                destBlend: Blend.Zero,
                blendOp: BlendOperation.Add,
                srcBlendAlpha: Blend.One,
                destBlendAlpha: Blend.Zero,
                blendOpAlpha: BlendOperation.Add,
                logicOp: LogicOp.Noop,
                renderTargetWriteMask: ColorWriteEnable.None);
        }
        blend.RenderTarget[4] = AdditiveOverdrawBlend();

        return blend;
    }

    private static BlendDescription CreateSceneEvidenceBlend()
    {
        var blend = BlendDescription.Opaque;
        blend.IndependentBlendEnable = true;
        blend.RenderTarget[4] = AdditiveOverdrawBlend();
        return blend;
    }

    private static RenderTargetBlendDescription AdditiveOverdrawBlend()
    {
        return new RenderTargetBlendDescription(
            blendEnable: true,
            logicOpEnable: false,
            srcBlend: Blend.One,
            destBlend: Blend.One,
            blendOp: BlendOperation.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOperation.Add,
            logicOp: LogicOp.Noop,
            renderTargetWriteMask: ColorWriteEnable.All);
    }

    private ID3D12PipelineState CreateFullscreenPipelineState(
        string path,
        string vertexEntryPoint,
        string pixelEntryPoint,
        Format renderTargetFormat,
        BlendDescription? blendDescription = null,
        bool enableDepth = false)
    {
        return CreateFullscreenPipelineState(
            path,
            vertexEntryPoint,
            pixelEntryPoint,
            [renderTargetFormat],
            blendDescription,
            enableDepth);
    }

    private ID3D12PipelineState CreateFullscreenPipelineState(
        string path,
        string vertexEntryPoint,
        string pixelEntryPoint,
        IReadOnlyList<Format> renderTargetFormats,
        BlendDescription? blendDescription = null,
        bool enableDepth = false)
    {
        var vertexShader = CompileShader(path, vertexEntryPoint, "vs_5_0");
        var pixelShader = CompileShader(path, pixelEntryPoint, "ps_5_0");
        var description = new GraphicsPipelineStateDescription
        {
            RootSignature = fullscreenRootSignature,
            VertexShader = vertexShader,
            PixelShader = pixelShader,
            BlendState = blendDescription ?? BlendDescription.Opaque,
            RasterizerState = RasterizerDescription.CullNone,
            DepthStencilState = enableDepth
                ? new DepthStencilDescription
                {
                    DepthEnable = true,
                    DepthWriteMask = DepthWriteMask.All,
                    DepthFunc = ComparisonFunction.LessEqual,
                    StencilEnable = false,
                }
                : DepthStencilDescription.None,
            SampleMask = uint.MaxValue,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            RenderTargetFormats = renderTargetFormats.ToArray(),
            SampleDescription = new SampleDescription(1, 0),
            DepthStencilFormat = enableDepth ? SceneDepthFormat : Format.Unknown,
        };

        try
        {
            return device.CreateGraphicsPipelineState(description);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create D3D12 pipeline VS={vertexEntryPoint} PS={pixelEntryPoint} depth={enableDepth}.", ex);
        }
    }

    private static ReadOnlyMemory<byte> CompileShader(string path, string entryPoint, string profile, bool skipOptimizationInDebug = true)
    {
        var shaderFlags = ShaderFlags.EnableStrictness;
#if DEBUG
        shaderFlags |= ShaderFlags.Debug;
        if (skipOptimizationInDebug)
        {
            shaderFlags |= ShaderFlags.SkipOptimization;
        }
#endif

        var source = ExpandShaderIncludes(path, []);
        try
        {
            return Compiler.Compile(source, entryPoint, path, profile, shaderFlags, EffectFlags.None);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to compile shader path={path} entry={entryPoint} profile={profile}: {ex.Message}", ex);
        }
    }

    private static string ExpandShaderIncludes(string path, HashSet<string> stack)
    {
        var fullPath = Path.GetFullPath(path);
        if (!stack.Add(fullPath))
        {
            throw new InvalidOperationException($"Circular shader include detected at {fullPath}");
        }

        var lines = File.ReadAllLines(fullPath);
        var expanded = new List<string>(lines.Length);
        var directory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#include \"", StringComparison.Ordinal))
            {
                var firstQuote = trimmed.IndexOf('"');
                var secondQuote = trimmed.IndexOf('"', firstQuote + 1);
                if (firstQuote >= 0 && secondQuote > firstQuote)
                {
                    var includeName = trimmed.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                    var includePath = ResolveShaderIncludePath(directory, includeName);
                    expanded.Add($"#line 1 \"{includePath.Replace("\\", "\\\\")}\"");
                    expanded.Add(ExpandShaderIncludes(includePath, stack));
                    expanded.Add($"#line {lineIndex + 2} \"{fullPath.Replace("\\", "\\\\")}\"");
                    continue;
                }
            }

            expanded.Add(line);
        }

        stack.Remove(fullPath);
        return string.Join(Environment.NewLine, expanded);
    }

    private static string ResolveShaderIncludePath(string directory, string includeName)
    {
        var localPath = Path.GetFullPath(Path.Combine(directory, includeName));
        if (File.Exists(localPath))
        {
            return localPath;
        }

        var normalized = includeName.Replace('\\', '/');
        const string cultMathPrefix = "CultMath/";
        if (normalized.StartsWith(cultMathPrefix, StringComparison.Ordinal))
        {
            var relative = normalized[cultMathPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            foreach (var start in new[] { directory, AppContext.BaseDirectory })
            {
                var current = Path.GetFullPath(start);
                while (!string.IsNullOrWhiteSpace(current))
                {
                    var candidate = Path.Combine(current, "CultMath", "shaders", relative);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    var parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    current = parent;
                }
            }
        }

        return localPath;
    }

    private readonly record struct D3D12PassContext(
        ID3D12GraphicsCommandList CommandList,
        D3D12TrackedResource BackBuffer,
        CpuDescriptorHandle RenderTargetView);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct FrameConstants(
        Vector2 Resolution,
        float TimeSeconds,
        float viewRadius,
        Vector3 CameraPosition,
        float FarDistance,
        Vector3 CameraTarget,
        float SceneFlags,
        Vector2 viewCenter,
        float FrameIndex,
        float PreviousTimeSeconds,
        Vector3 PreviousCameraPosition,
        float previousViewRadius,
        Vector3 PreviousCameraTarget,
        float PreviousSceneFlags,
        Vector2 previousViewCenter,
        Vector2 JitterPixels,
        Vector2 PreviousJitterPixels,
        float RenderDebugMode,
        float Exposure,
        float BloomIntensity,
        float BloomVeilIntensity,
        Vector2 Padding0,
        Vector4 CursorWorlds,
        Vector4 TemporalGaussianInfo,
        Vector4 CameraFrustumXy,
        Vector4 CameraFrustumZ,
        Vector4 GpuFusionInfo,
        Vector4 FractalReservoirInfo,
        Vector4 FractalReservoirFrame,
        Vector4 ReservoirBudgetInfo);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12SplineVertex(
        Vector3 Position,
        Vector3 SegmentStart,
        Vector3 SegmentEnd,
        Vector3 Previous,
        Vector3 Next,
        Vector4 ShapeData,
        Vector4 RadiusData,
        Vector4 Color,
        Vector4 Material);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12TubeFieldVertex(
        Vector3 Position,
        Vector3 SegmentStart,
        Vector3 SegmentEnd,
        Vector3 Previous,
        Vector3 Next,
        Vector4 ShapeData,
        Vector4 RadiusData,
        Vector4 Color,
        Vector4 Material,
        Vector4 TubeData);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12TubeFieldSegment(
        Vector4 PreviousRadius,
        Vector4 StartRadius,
        Vector4 EndFeather,
        Vector4 NextAlpha,
        Vector4 Color0,
        Vector4 Color1,
        Vector4 Material,
        Vector4 TubeData);

    private readonly record struct D3D12TubeFieldDrawBatch(
        D3D12StructuredBuffer Source,
        D3D12TubeFieldConstants Constants,
        ulong ConstantsGpuVirtualAddress,
        string RampResourceKey,
        int DrawArgumentOffsetBytes);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12BokushoBrushConstants(
        Vector4 Shape,
        Vector4 Brush,
        Vector4 Dynamics,
        Vector4 Profile,
        Vector4 StrokeP0,
        Vector4 StrokeP1,
        Vector4 StrokeP2,
        Vector4 StrokeP3);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12BokushoBrushStrokePacket(
        Vector4 Profile,
        Vector4 Dynamics,
        Vector4 StrokeP0,
        Vector4 StrokeP1,
        Vector4 StrokeP2,
        Vector4 StrokeP3);

    private readonly record struct D3D12PipelinePrivateGeneratedMesh(
        D3D12StructuredBuffer Vertices,
        D3D12StructuredBuffer Indices,
        D3D12StructuredBuffer DrawArguments,
        VertexBufferView VertexView,
        IndexBufferView IndexView,
        PrimitiveTopology Topology);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12TubeFieldConstants(
        Vector4 Shape,
        Vector4 Columns,
        Vector4 Amplitude,
        Vector4 Material,
        Vector4 Dispatch,
        Vector4 Draw,
        Vector3 Origin,
        float Padding0,
        Vector3 AxisStep,
        float Padding1,
        Vector3 ColumnStep,
        float Padding2)
    {
        public static readonly D3D12TubeFieldConstants Disabled = new(
            Vector4.Zero,
            Vector4.Zero,
            Vector4.Zero,
            Vector4.Zero,
            Vector4.Zero,
            Vector4.Zero,
            Vector3.Zero,
            0.0f,
            Vector3.Zero,
            0.0f,
            Vector3.Zero,
            0.0f);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12TubeFieldReplayManifestEntry(
        Vector4 Shape,
        Vector4 Columns,
        Vector4 Amplitude,
        Vector4 Material,
        Vector4 Dispatch,
        Vector4 Draw,
        Vector4 Origin,
        Vector4 AxisStep,
        Vector4 ColumnStep,
        Vector4 Source)
    {
        public static readonly D3D12TubeFieldReplayManifestEntry Disabled = FromConstants(D3D12TubeFieldConstants.Disabled, -1);

        public static D3D12TubeFieldReplayManifestEntry FromConstants(D3D12TubeFieldConstants constants, int sourceIndex)
        {
            return new D3D12TubeFieldReplayManifestEntry(
                constants.Shape,
                constants.Columns,
                constants.Amplitude,
                constants.Material,
                constants.Dispatch,
                constants.Draw,
                new Vector4(constants.Origin, 0.0f),
                new Vector4(constants.AxisStep, 0.0f),
                new Vector4(constants.ColumnStep, 0.0f),
                new Vector4(sourceIndex, sourceIndex >= 0 ? 1.0f : 0.0f, 0.0f, 0.0f));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12StereoDepthConstants(
        Vector4 Shape,
        Vector4 Match,
        Vector4 Output);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct D3D12PointCloudConstants(
        Vector4 SourceShape,
        Vector4 Projection,
        Vector4 DepthRange);

    private sealed record D3D12ShaderPaths(
        string HeightField,
        string Scene,
        string Spline,
        string TemporalGaussian,
        string GpuSensorFusion,
        string StereoDepth,
        string PointCloud,
        string TubeField,
        string BokushoBrush,
        string FractalReservoir,
        string FractalSplatRender,
        string SdfCommon,
        string SdfProxy,
        IReadOnlyList<string> SdfShaders,
        string SdfMath,
        IReadOnlyList<string> Includes,
        string Post,
        string ReservoirHistoryUpdate)
    {
        public IReadOnlyList<string> All { get; } = [HeightField, Scene, Spline, TemporalGaussian, GpuSensorFusion, StereoDepth, PointCloud, TubeField, BokushoBrush, FractalReservoir, FractalSplatRender, SdfCommon, SdfProxy, ..SdfShaders, SdfMath, ..Includes, Post, ReservoirHistoryUpdate];

        public static D3D12ShaderPaths FromManifest(string root, AquariumShaderManifest manifest)
        {
            string shaderPath(string path) => Path.IsPathRooted(path) ? path : Path.Combine(root, path);
            var includes = manifest.SdfLibraryInclude is null
                ? manifest.IncludePaths
                : [.. manifest.IncludePaths, manifest.SdfLibraryInclude];
            return new D3D12ShaderPaths(
                shaderPath(manifest.HeightFieldShader),
                shaderPath(manifest.SceneShader),
                shaderPath("D3D12Scene.hlsl"),
                shaderPath(manifest.TemporalGaussianShader),
                shaderPath(manifest.GpuSensorFusionShader),
                shaderPath("D3D12StereoDepth.hlsl"),
                shaderPath("D3D12PointCloud.hlsl"),
                shaderPath("D3D12TubeField.hlsl"),
                shaderPath("D3D12BokushoBrush.hlsl"),
                shaderPath(manifest.FractalReservoirShader),
                shaderPath(manifest.FractalSplatRenderShader),
                shaderPath(manifest.SdfCommonInclude),
                shaderPath(manifest.SdfProxyInclude),
                manifest.SdfShaderPaths.Select(shaderPath).ToArray(),
                shaderPath(manifest.SdfMathInclude),
                [.. includes.Select(shaderPath), shaderPath("D3D12Aces2.hlsl")],
                shaderPath(manifest.PostShader),
                shaderPath("D3D12ReservoirHistoryUpdate.hlsl"));
        }
    }

    private sealed record D3D12PipelineSet(
        ID3D12PipelineState HeightFieldBase,
        ID3D12PipelineState HeightFieldBrush,
        ID3D12PipelineState Scene,
        ID3D12PipelineState Spline,
        ID3D12PipelineState TemporalGaussian,
        ID3D12PipelineState GpuSensorFusion,
        ID3D12PipelineState StereoDepth,
        ID3D12PipelineState PointCloudCompute,
        ID3D12PipelineState PointCloudRender,
        ID3D12PipelineState FractalSurfaceSplatRender,
        ID3D12PipelineState FractalTransparentSplatRender,
        ID3D12PipelineState FractalSplat,
        ID3D12PipelineState FractalSdfReservoir,
        ID3D12PipelineState FractalPbrReservoir,
        ID3D12PipelineState FractalRadiosityReservoir,
        ID3D12PipelineState TubeFieldCompute,
        ID3D12PipelineState TubeFieldRender,
        ID3D12PipelineState BokushoBrush,
        ID3D12PipelineState FieldReservoirResolve,
        IReadOnlyList<ID3D12PipelineState> SdfProxies,
        ID3D12PipelineState BloomPrefilter,
        ID3D12PipelineState BloomDownsample,
        ID3D12PipelineState BloomBlurHorizontal,
        ID3D12PipelineState BloomBlurVertical,
        ID3D12PipelineState? ReservoirHistoryUpdate,
        ID3D12PipelineState Resolve) : IDisposable
    {
        public void Dispose()
        {
            Resolve.Dispose();
            ReservoirHistoryUpdate?.Dispose();
            BloomBlurVertical.Dispose();
            BloomBlurHorizontal.Dispose();
            BloomDownsample.Dispose();
            BloomPrefilter.Dispose();
            FieldReservoirResolve.Dispose();
            BokushoBrush.Dispose();
            FractalRadiosityReservoir.Dispose();
            TubeFieldRender.Dispose();
            TubeFieldCompute.Dispose();
            PointCloudRender.Dispose();
            PointCloudCompute.Dispose();
            StereoDepth.Dispose();
            FractalPbrReservoir.Dispose();
            FractalSdfReservoir.Dispose();
            FractalSplat.Dispose();
            FractalTransparentSplatRender.Dispose();
            FractalSurfaceSplatRender.Dispose();
            GpuSensorFusion.Dispose();
            TemporalGaussian.Dispose();
            Spline.Dispose();
            foreach (var sdfProxy in SdfProxies)
            {
                sdfProxy.Dispose();
            }

            Scene.Dispose();
            HeightFieldBrush.Dispose();
            HeightFieldBase.Dispose();
        }
    }

    private sealed record SharedTextureLeaseSlot(
        D3D12FieldTexture2D Texture,
        ID3D12Fence ProducerFence,
        int Width,
        int Height,
        Format Format,
        AquariumFieldShaderAccess ProducerAccess,
        ulong Version) : IDisposable
    {
        public ulong CommittedProducerFenceValue { get; set; }

        public ulong WaitedProducerFenceValue { get; set; }

        public IntPtr NativeHandle => IntPtr.Zero;

        public void Dispose()
        {
            ProducerFence.Dispose();
            Texture.Dispose();
        }
    }

    private sealed class ExternalProducerFenceSlot(ID3D12Fence fence, IntPtr handle) : IDisposable
    {
        public ID3D12Fence Fence { get; } = fence;

        public IntPtr Handle { get; } = handle;

        public ulong WaitedValue { get; set; }

        public void Dispose()
        {
            Fence.Dispose();
        }
    }

    private enum D3D12GpuTimingPass
    {
        FrameRecord,
        GpuSensorFusion,
        StereoDepthUpdate,
        FractalReservoirUpdate,
        TubeFieldUpdate,
        HeightField,
        SceneCandidate,
        FieldReservoirResolve,
        ReservoirHistoryUpdate,
        Bloom,
        PresentationResolve,
        Count,
    }

    private sealed class FrameResources(
        ID3D12CommandAllocator commandAllocator,
        D3D12UploadRing uploadRing,
        D3D12DescriptorArena transientShaderDescriptors,
        ID3D12QueryHeap gpuTimingQueryHeap,
        ID3D12Resource gpuTimingReadback) : IDisposable
    {
        public ID3D12CommandAllocator CommandAllocator { get; } = commandAllocator;

        public D3D12UploadRing UploadRing { get; } = uploadRing;

        public D3D12DescriptorArena TransientShaderDescriptors { get; } = transientShaderDescriptors;

        public ID3D12QueryHeap GpuTimingQueryHeap { get; } = gpuTimingQueryHeap;

        public ID3D12Resource GpuTimingReadback { get; } = gpuTimingReadback;

        public uint GpuTimingPassMask { get; set; }

        public uint GpuTimingPendingPassMask { get; set; }

        public bool GpuTimingEligibleForAccumulation { get; set; }

        public D3D12DescriptorSlot FrameConstantsDescriptor { get; set; }

        public D3D12DescriptorSlot HeightFieldBrushConstantsDescriptor { get; set; }

        public D3D12DescriptorSlot SdfLightDescriptor { get; set; }

        public D3D12DescriptorSlot SdfObjectDescriptor { get; set; }

        public D3D12DescriptorSlot GpuSensorCameraDescriptor { get; set; }

        public D3D12DescriptorSlot GpuSensorTextureDescriptor { get; set; }

        public D3D12DescriptorSlot AcousticConstraintDescriptor { get; set; }

        public D3D12DescriptorSlot GpuFusionSeedDescriptor { get; set; }

        public D3D12DescriptorSlot GpuFusionPointDescriptor { get; set; }

        public D3D12DescriptorSlot TemporalGaussianDescriptor { get; set; }

        public D3D12DescriptorSlot TemporalGaussianUnorderedAccessDescriptor { get; set; }

        public D3D12DescriptorSlot BlueNoiseDescriptor { get; set; }

        public D3D12DescriptorSlot StudioPmremDescriptor { get; set; }

        public D3D12DescriptorSlot StudioIrradianceDescriptor { get; set; }

        public D3D12DescriptorSlot HeightFieldDescriptor { get; set; }

        public D3D12DescriptorSlot SceneDescriptor { get; set; }

        public D3D12DescriptorSlot SceneMetadataDescriptor { get; set; }

        public D3D12DescriptorSlot SceneControlDescriptor { get; set; }

        public D3D12DescriptorSlot SceneReservoirGuideDescriptor { get; set; }

        public D3D12DescriptorSlot SceneCandidateDescriptor { get; set; }

        public D3D12DescriptorSlot SceneCandidateMetadataDescriptor { get; set; }

        public D3D12DescriptorSlot SceneCandidateControlDescriptor { get; set; }

        public D3D12DescriptorSlot SceneCandidateReservoirGuideDescriptor { get; set; }

        public D3D12DescriptorSlot SceneOverdrawDescriptor { get; set; }

        public D3D12DescriptorSlot ReservoirResolvedDescriptor { get; set; }

        public D3D12DescriptorSlot ReservoirResolvedUnorderedAccessDescriptor { get; set; }

        public D3D12DescriptorSlot FieldReservoirCandidateUnorderedAccessDescriptor { get; set; }

        public D3D12DescriptorSlot FieldReservoirLockUnorderedAccessDescriptor { get; set; }

        public D3D12DescriptorSlot[] BloomDescriptors { get; } = new D3D12DescriptorSlot[BloomLevelCount];

        public D3D12DescriptorSlot[] BloomScratchDescriptors { get; } = new D3D12DescriptorSlot[BloomLevelCount];

        public D3D12DescriptorSlot BloomPresentationDescriptor { get; set; }

        public D3D12TrackedResource BackBuffer { get; set; } = null!;

        public D3D12DescriptorSlot BackBufferRenderTargetView { get; set; }

        public ulong FenceValue { get; set; }

        public void Dispose()
        {
            BackBuffer.Dispose();
            GpuTimingReadback.Dispose();
            GpuTimingQueryHeap.Dispose();
            TransientShaderDescriptors.Dispose();
            UploadRing.Dispose();
            CommandAllocator.Dispose();
        }
    }
}
