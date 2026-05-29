using System.Numerics;
using Aquarium.Engine;
using Aquarium.Engine.Audio;
using Aquarium.Engine.Input;
using Aquarium.Engine.Render;
using Aquarium.Engine.Ui;

namespace Aquarium.Fensalir;

public sealed class FensalirRuntime : IAquariumRuntime
{
    private float timeSeconds;
    private float previousTimeSeconds;
    private float orbitYaw;
    private float orbitPitch = 0.19f;
    private float orbitDistance = 7.6f;
    private bool autoDrift = true;

    public AquariumRuntimeOptions Options { get; private set; }

    public GraphicsSettings GraphicsSettings { get; set; } = new(
        0,
        1.04f,
        0.52f,
        0.08f,
        GraphicsSettings.FieldReservoirModeNativeDomain);

    public AquariumRenderPlan RenderPlan { get; } = FensalirRenderPlan.Create();

    public AquariumUiDocument Ui { get; }

    public AquariumAudioDocument Audio { get; } = new();

    public AquariumSynthDocument Synth { get; } = AquariumSynthDocument.Empty;

    public AquariumFrame Frame
    {
        get
        {
            var target = new Vector3(0.0f, 0.12f, 2.1f);
            var camera = ComposeCamera(target);
            return new AquariumFrame(
                new ViewFrame(Vector2.Zero, 8.0f),
                camera,
                target,
                timeSeconds,
                Vector2.Zero,
                FensalirSceneBuilder.Build(timeSeconds, previousTimeSeconds));
        }
    }

    public FensalirRuntime()
    {
        Ui = new AquariumUiDocument()
            .Panel("Fensalir", 18.0f, 82.0f, 360.0f, panel =>
            {
                panel.Section("Splash Reconstruction");
                panel.Toggle("Auto Drift", () => autoDrift, value => autoDrift = value);
                panel.Slider("Orbit Distance", () => orbitDistance, value => orbitDistance = Math.Clamp(value, 3.2f, 14.0f), 3.2f, 14.0f, "0.00");
                panel.Readout("Patch", () => FensalirFractalScene.Summary);
                panel.Readout("Surface", () => $"{FensalirFractalScene.HeightBrushes.Count(brush => brush.WaveAmplitude != 0.0f)} ripple brushes / quadtree plane");
                panel.Readout("Renderer", () => "2D marsh field + emissive SDF spine + bloom");
                panel.Readout("Camera", () => $"{orbitDistance:0.00} wu / yaw {orbitYaw:0.00}");
            })
            .Command("fensalir", _ => $"Fensalir: {FensalirFractalScene.Summary}", "Report Fensalir splash scene status.")
            .Command("fensalir-fractal", _ => FensalirFractalScene.DebugDump, "Dump the compiled Fensalir splash .aquageo patch.");
    }

    public AquariumFrame ComposeFrame(AquariumFrame frame, AquariumFrameInput input)
    {
        return frame;
    }

    public void Start()
    {
        Console.WriteLine("Fensalir splash reconstruction booted.");
    }

    public void Update(float deltaSeconds, InputState input)
    {
        previousTimeSeconds = timeSeconds;
        var safeDelta = Math.Max(deltaSeconds, 0.0f);
        timeSeconds += safeDelta;

        if (autoDrift)
        {
            orbitYaw += safeDelta * 0.018f;
        }

        if (input.IsKeyDown(KeyCode.A))
        {
            orbitYaw -= safeDelta * 0.7f;
            autoDrift = false;
        }

        if (input.IsKeyDown(KeyCode.D))
        {
            orbitYaw += safeDelta * 0.7f;
            autoDrift = false;
        }

        if (input.IsKeyDown(KeyCode.W))
        {
            orbitPitch += safeDelta * 0.32f;
        }

        if (input.IsKeyDown(KeyCode.S))
        {
            orbitPitch -= safeDelta * 0.32f;
        }

        if (MathF.Abs(input.WheelDelta) > 0.0f)
        {
            orbitDistance = Math.Clamp(orbitDistance - input.WheelDelta * MathF.Max(orbitDistance * 0.09f, 0.05f), 3.2f, 14.0f);
        }

        if (input.LeftMouseDown && input.MouseDelta != Vector2.Zero)
        {
            orbitYaw -= input.MouseDelta.X * 0.006f;
            orbitPitch += input.MouseDelta.Y * 0.0035f;
            autoDrift = false;
        }

        orbitPitch = Math.Clamp(orbitPitch, 0.08f, 0.42f);
    }

    public void FlushState()
    {
    }

    public void Dispose()
    {
    }

    internal void SetOptions(AquariumRuntimeOptions options)
    {
        Options = options;
        if (options.Headless)
        {
            autoDrift = false;
            timeSeconds = 4.0f;
            previousTimeSeconds = timeSeconds - (1.0f / 60.0f);
            orbitYaw = 0.0f;
            orbitPitch = 0.12f;
            orbitDistance = 6.1f;
        }
    }

    private Vector3 ComposeCamera(Vector3 target)
    {
        var yaw = -0.03f + orbitYaw;
        var horizontal = MathF.Cos(orbitPitch) * orbitDistance;
        return new Vector3(
            MathF.Sin(yaw) * horizontal,
            -MathF.Cos(yaw) * horizontal,
            target.Z + MathF.Sin(orbitPitch) * orbitDistance);
    }
}

public sealed class FensalirRuntimeFactory : IAquariumRuntimeFactory
{
    public IAquariumRuntime Create(AquariumRuntimeOptions options)
    {
        var runtime = new FensalirRuntime();
        runtime.SetOptions(options);
        return runtime;
    }
}
