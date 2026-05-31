using System.Numerics;
using Aquarium.Engine;
using Aquarium.Engine.Audio;
using Aquarium.Engine.Input;
using Aquarium.Engine.Render;
using Aquarium.Engine.Ui;

namespace Aquarium.Sample.Minimal;

public sealed class MinimalRuntime : IAquariumRuntime
{
    private float timeSeconds;
    private int selectedFeed;
    private int selectedLut;
    private bool feedEnabled = true;
    private bool visible = true;
    private bool locked;
    private float opacity = 1.0f;
    private float gain = 0.72f;
    private float lutStrength = 1.0f;
    private float x = -3.2f;
    private float y = 2.1f;
    private float z;
    private float rotation;
    private float scaleX = 2.4f;
    private float scaleY = 0.8f;

    public AquariumRuntimeOptions Options { get; private set; }

    public GraphicsSettings GraphicsSettings { get; set; } = GraphicsSettings.Default;

    public AquariumRenderPlan RenderPlan { get; } = CreateRenderPlan();

    public AquariumUiDocument Ui { get; }

    public AquariumAudioDocument Audio { get; } = new();

    public AquariumSynthDocument Synth { get; } = AquariumSynthDocument.Empty;

    public AquariumFrame Frame => new(
        new ViewFrame(Vector2.Zero, 24.0f),
        new Vector3(0.0f, -10.0f, 7.0f),
        timeSeconds,
        Vector2.Zero,
        AquariumSceneState.Empty);

    public MinimalRuntime()
    {
        Ui = CreateUi();
    }

    public AquariumFrame ComposeFrame(AquariumFrame frame, AquariumFrameInput input)
    {
        return frame;
    }

    public void Start()
    {
        Console.WriteLine("Minimal Aquarium sample booted.");
    }

    public void Update(float deltaSeconds, InputState input)
    {
        timeSeconds += Math.Max(deltaSeconds, 0.0f);
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
    }

    private AquariumUiDocument CreateUi()
    {
        var feeds = new[]
        {
            new AquariumUiOption(0, "PS3 Eye 0 high-speed"),
            new AquariumUiOption(1, "Kiyo Pro RGB"),
            new AquariumUiOption(2, "Synthetic checker field"),
        };
        var luts = new[]
        {
            new AquariumUiOption(0, "Neutral"),
            new AquariumUiOption(1, "Thermal"),
            new AquariumUiOption(2, "Depth debug"),
        };

        return new AquariumUiDocument()
            .Surface("aquarium.ui_smoke", "CultUI Smoke", 16.0f, 56.0f, 980.0f, 620.0f, surface =>
            {
                surface.Horizontal("columns", columns =>
                {
                    columns.Vertical("left", left =>
                    {
                        left.Pane("program", "Program", pane =>
                        {
                            pane.Options("feed", "Feed", () => selectedFeed, value => selectedFeed = value, feeds);
                            pane.Toggle("feed-enabled", "Visible", () => feedEnabled, value => feedEnabled = value);
                            pane.Slider("opacity", "Opacity", () => opacity, value => opacity = value, 0.0f, 1.0f, "0.00");
                            pane.Text("program-compose", () => $"1:{feeds[selectedFeed].Label}@{opacity:0.00} | LUT:{luts[selectedLut].Label}", "mono");
                            pane.Options("stem", "Stem", () => 0, _ => { }, [new AquariumUiOption(0, "Focusrite Input 1")]);
                            pane.Slider("gain", "Gain", () => gain, value => gain = value, 0.0f, 1.5f, "0.00");
                            pane.Options("lut", "LUT", () => selectedLut, value => selectedLut = value, luts);
                            pane.Slider("lut-strength", "LUT Strength", () => lutStrength, value => lutStrength = value, 0.0f, 1.0f, "0.00");
                        }, weight: 1.2f);

                        left.Pane("scene", "Scene", pane =>
                        {
                            pane.Options("editor-view", "Editor View", () => 0, _ => { }, [new AquariumUiOption(0, "Scene")]);
                            pane.Card("tree", card =>
                            {
                                card.Text("tree-0", "Scene", "strong");
                                card.Text("tree-1", "  - [eye] Editor Camera <Camera>", "mono");
                                card.Text("tree-2", "  - Sensor Feeds (4)", "mono");
                                card.Text("tree-3", "    - [eye] Kiyo Pro RGB context <SensorFeed>", "mono");
                                card.Text("tree-4", "    - [eye] Synthetic checker field <SensorFeed>", "mono");
                            });
                            pane.Row("scene-buttons", row =>
                            {
                                row.Button("previous", "Previous", () => selectedFeed = Math.Max(0, selectedFeed - 1));
                                row.Button("next", "Next", () => selectedFeed = Math.Min(feeds.Length - 1, selectedFeed + 1));
                            });
                            pane.Text("scene-status", () => $"SdfTextPanel pos={x:0.00},{y:0.00},{z:0.00} scale={scaleX:0.00},{scaleY:0.00}", "mono");
                        });
                    }, weight: 1.05f);

                    columns.Vertical("middle", middle =>
                    {
                        middle.Pane("sync", "Sync", pane =>
                        {
                            pane.Text("streams", "4 video / 4 audio buffers", "strong");
                            pane.Metric("sources", "sources", () => 5.0);
                            pane.Metric("ingested", "samples ingested", () => 533_939.0 + (timeSeconds * 120.0));
                            pane.Text("buffer-detail", "Focusrite Input 1 [asio-ch0]: 3751 1ch 192000Hz; passive mode waiting for program audio column", "body");
                            pane.Text("audio-sync", () => $"asio-ch0: {1484.0 + Math.Sin(timeSeconds) * 0.2:0.00} samples  {779.2 + Math.Cos(timeSeconds) * 0.1:0.00} us", "mono");
                            pane.Text("chirplet", "chirplet reference: asio-ch1 passive; calibration emission command-ready", "body");
                            pane.Slider("perspective", "Perspective", () => 10.0f, _ => { }, 1.0f, 20.0f, "0.0x");
                            pane.Slider("angle", "Angle", () => 25.0f, _ => { }, 0.0f, 90.0f, "0.0 deg");
                            pane.Text("tube-budget", "40/40 age columns x 4/8 lanes x 4 subdivisions", "mono");
                        }, weight: 1.15f);

                        middle.Pane("transform", "Transform", pane =>
                        {
                            pane.Toggle("visible", "Visible", () => visible, value => visible = value);
                            pane.Toggle("locked", "Locked", () => locked, value => locked = value);
                            pane.Text("mode", "Mode: Grab", "mono");
                            pane.Slider("x", "X", () => x, value => x = value, -10.0f, 10.0f, "0.00");
                            pane.Slider("y", "Y", () => y, value => y = value, -10.0f, 10.0f, "0.00");
                            pane.Slider("z", "Z", () => z, value => z = value, -10.0f, 10.0f, "0.00");
                            pane.Slider("rotation", "Rotation", () => rotation, value => rotation = value, -180.0f, 180.0f, "0.00");
                            pane.Slider("scale-x", "Scale X", () => scaleX, value => scaleX = value, 0.1f, 4.0f, "0.00");
                            pane.Slider("scale-y", "Scale Y", () => scaleY, value => scaleY = value, 0.1f, 4.0f, "0.00");
                            pane.Row("transform-buttons", row =>
                            {
                                row.Button("reset-transform", "Reset Transform", ResetTransform);
                                row.Button("reset-camera", "Reset Camera", ResetTransform);
                            });
                        });
                    }, weight: 1.1f);

                    columns.Pane("create", "Create", pane =>
                    {
                        pane.Text("text-label", "Text", "body");
                        pane.Text("text-value", "Mimir", "strong");
                        pane.Button("add-sdf-text", "Add SDF Text", () => { });
                        pane.Text("model-path-label", "Model Path", "body");
                        pane.Text("model-path", "assets/models/example.glb", "mono");
                        pane.Button("import-model", "Import Model", () => { });
                        pane.Metric("fps", "ui smoke fps proxy", () => 1.0 / Math.Max(0.001, Math.Min(0.25, timeSeconds % 1.0)));
                        pane.Text("budget", "This pane is intentionally narrow enough to prove labels clip inside their cells instead of wandering across neighbors.", "body");
                    }, weight: 0.75f);
                });
            });
    }

    private void ResetTransform()
    {
        x = -3.2f;
        y = 2.1f;
        z = 0.0f;
        rotation = 0.0f;
        scaleX = 2.4f;
        scaleY = 0.8f;
    }

    private static AquariumRenderPlan CreateRenderPlan()
    {
        var app = new AquariumApp();
        var scene = app.RenderTargets.Hdr("scene");
        app.Cameras.Perspective("main");
        app.Graph.Pass("scene").Fullscreen();
        app.Features.Bloom(scene.Color);
        app.Features.Presentation(scene.Color);
        app.Features.DirectWriteOverlay();
        app.Debug.View("Scene", scene.Color);
        return app.Plan;
    }
}

public sealed class MinimalRuntimeFactory : IAquariumRuntimeFactory
{
    public IAquariumRuntime Create(AquariumRuntimeOptions options)
    {
        var runtime = new MinimalRuntime();
        runtime.SetOptions(options);
        return runtime;
    }
}
