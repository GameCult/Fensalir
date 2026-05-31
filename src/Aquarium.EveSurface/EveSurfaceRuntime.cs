using System.Globalization;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Aquarium.Engine;
using Aquarium.Engine.Audio;
using Aquarium.Engine.Input;
using Aquarium.Engine.Render;
using Aquarium.Engine.Ui;

namespace Aquarium.EveSurface;

public sealed class EveSurfaceRuntime : IAquariumRuntime
{
    private const string DefaultBrokerUrl = "ws://127.0.0.1:8795/eve/deck";
    private const string DefaultProviderId = "voidbot.swarm";
    private readonly object sync = new();
    private readonly string brokerUrl = Environment.GetEnvironmentVariable("FENSALIR_EVE_BROKER") ?? DefaultBrokerUrl;
    private readonly string providerId = Environment.GetEnvironmentVariable("FENSALIR_EVE_PROVIDER") ?? DefaultProviderId;
    private CancellationTokenSource? connectionLifetime;
    private Task? connectionTask;
    private EveSurfaceState state = EveSurfaceState.Empty;
    private string status = "not started";
    private string selectedNodeId = "";
    private float timeSeconds;

    public AquariumRuntimeOptions Options { get; private set; }

    public GraphicsSettings GraphicsSettings { get; set; } = GraphicsSettings.Default;

    public AquariumRenderPlan RenderPlan { get; } = CreateRenderPlan();

    public AquariumUiDocument Ui => ComposeUi();

    public AquariumAudioDocument Audio { get; } = new();

    public AquariumSynthDocument Synth { get; } = AquariumSynthDocument.Empty;

    public AquariumFrame Frame => new(
        new ViewFrame(Vector2.Zero, 24.0f),
        new Vector3(0.0f, -10.0f, 7.0f),
        timeSeconds,
        Vector2.Zero,
        AquariumSceneState.Empty);

    public AquariumFrame ComposeFrame(AquariumFrame frame, AquariumFrameInput input) => frame;

    public void Start()
    {
        Console.WriteLine($"Eve surface client connecting to {brokerUrl} provider {providerId}.");
        connectionLifetime = new CancellationTokenSource();
        connectionTask = Task.Run(() => RunConnectionLoopAsync(connectionLifetime.Token));
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
        connectionLifetime?.Cancel();
        try
        {
            connectionTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        connectionLifetime?.Dispose();
    }

    internal void SetOptions(AquariumRuntimeOptions options)
    {
        Options = options;
    }

    private AquariumUiDocument ComposeUi()
    {
        EveSurfaceState snapshot;
        string connectionStatus;
        lock (sync)
        {
            snapshot = state;
            connectionStatus = status;
            if (string.IsNullOrWhiteSpace(selectedNodeId))
            {
                selectedNodeId = snapshot.SelectedNodeId;
            }
        }

        var nodes = snapshot.Nodes;
        var selected = nodes.FirstOrDefault(node => node.Id == selectedNodeId)
            ?? nodes.FirstOrDefault(node => node.Id == snapshot.SelectedNodeId)
            ?? nodes.FirstOrDefault();
        var ctb = nodes.Where(static node => string.Equals(node.Kind, "ctb-turn", StringComparison.OrdinalIgnoreCase)).Take(12).ToArray();
        var leaves = nodes.Where(static node => string.Equals(node.Kind, "state-leaf", StringComparison.OrdinalIgnoreCase)).Take(32).ToArray();
        var primaryNodes = ctb.Length > 0 ? ctb : nodes.Take(16).ToArray();
        var detailNodes = leaves.Length > 0 ? leaves : nodes.Skip(primaryNodes.Length).Take(32).ToArray();
        if (detailNodes.Length == 0 && ctb.Length == 0)
        {
            detailNodes = nodes.Take(32).ToArray();
        }
        var summary = nodes.FirstOrDefault(static node => node.Id == "voidbot-summary");
        var agent = nodes.FirstOrDefault(static node => node.Id == "agent-detail");

        return new AquariumUiDocument()
            .Panel("Eve Surface", 18.0f, 82.0f, 420.0f, fadeWhenMouseDistant: true, panel =>
            {
                panel.Section("CultMesh Provider");
                panel.Readout("Provider", () => string.IsNullOrWhiteSpace(snapshot.ProviderId) ? providerId : snapshot.ProviderId);
                panel.Readout("Title", () => snapshot.Title);
                panel.Readout("Version", () => snapshot.Version.ToString(CultureInfo.InvariantCulture));
                panel.Readout("Broker", () => brokerUrl);
                panel.Readout("Status", () => connectionStatus);
                panel.Button("Reconnect", Reconnect, "Restart the WebSocket subscription and reopen the provider.");
            })
            .Panel(ctb.Length > 0 ? "CTB" : "Surface Nodes", 456.0f, 82.0f, 520.0f, fadeWhenMouseDistant: true, panel =>
            {
                panel.Section(ctb.Length > 0 ? "VoidBot Turn Bar" : snapshot.Title);
                foreach (var node in primaryNodes)
                {
                    panel.TreeItem(
                        CompactLabel(node.Label),
                        0,
                        canExpand: false,
                        isExpanded: static () => false,
                        setExpanded: static _ => { },
                        isSelected: () => selectedNodeId == node.Id,
                        select: () => selectedNodeId = node.Id,
                        detail: CompactDetail(node));
                }
            })
            .Panel(ctb.Length > 0 ? "Selected Face" : "Selected Node", 18.0f, 382.0f, 420.0f, fadeWhenMouseDistant: true, panel =>
            {
                panel.Section(summary?.Label ?? "VoidBot Swarm");
                panel.Readout("Summary", () => summary?.Health ?? "waiting");
                panel.TextBox("Selected", () => selected?.Label ?? "none", _ => { }, lines: 1, acceptsReturn: false, monospace: false);
                panel.TextBox("Detail", () => selected?.Detail ?? agent?.Detail ?? "No provider detail yet.", _ => { }, lines: 12, acceptsReturn: false, monospace: true);
            })
            .Panel("State Graph", 456.0f, 222.0f, 520.0f, fadeWhenMouseDistant: true, panel =>
            {
                panel.Section(leaves.Length > 0 ? "Leaves" : "Details");
                foreach (var node in detailNodes)
                {
                    panel.TreeItem(
                        CompactLabel(node.Label),
                        0,
                        canExpand: false,
                        isExpanded: static () => false,
                        setExpanded: static _ => { },
                        isSelected: () => selectedNodeId == node.Id,
                        select: () => selectedNodeId = node.Id,
                        detail: CompactDetail(node));
                }
            })
            .Panel("Surface Detail", 994.0f, 82.0f, 520.0f, fadeWhenMouseDistant: true, panel =>
            {
                panel.Section(selected?.Kind ?? "detail");
                panel.Readout("Node", () => selected?.Id ?? "none");
                panel.Readout("Health", () => selected?.Health ?? "");
                panel.TextBox("State", () => selected?.Detail ?? "No selected node.", _ => { }, lines: 18, acceptsReturn: false, monospace: true);
            })
            .Command("eve-status", _ => $"{connectionStatus}; {snapshot.ProviderId} v{snapshot.Version}; nodes {nodes.Count}; surface {snapshot.Surface?.Schema ?? "none"} root {snapshot.Surface?.Root?.Kind ?? "none"}", "Report the active Eve surface subscription.");
    }

    private void Reconnect()
    {
        connectionLifetime?.Cancel();
        connectionLifetime = new CancellationTokenSource();
        connectionTask = Task.Run(() => RunConnectionLoopAsync(connectionLifetime.Token));
    }

    private async Task RunConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SetStatus("connecting");
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri(brokerUrl), cancellationToken).ConfigureAwait(false);
                SetStatus("connected");
                await SendOpenProviderAsync(socket, cancellationToken).ConfigureAwait(false);

                var buffer = new byte[64 * 1024];
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var message = await ReceiveTextAsync(socket, buffer, cancellationToken).ConfigureAwait(false);
                    if (message is null)
                    {
                        break;
                    }

                    var next = EveSurfaceState.Parse(message);
                    if (!string.Equals(next.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    lock (sync)
                    {
                        state = next;
                        status = $"live {next.UpdatedAt}";
                        if (string.IsNullOrWhiteSpace(selectedNodeId))
                        {
                            selectedNodeId = next.SelectedNodeId;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                SetStatus($"error {exception.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SendOpenProviderAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes($$"""{"type":"open-provider","providerId":"{{providerId}}"}""");
        await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    private void SetStatus(string value)
    {
        lock (sync)
        {
            status = value;
        }
    }

    private static string CompactLabel(string value)
    {
        var label = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return label.Length <= 72 ? label : label[..69] + "...";
    }

    private static string CompactDetail(EveSurfaceNode node)
    {
        var parts = new[] { node.Kind, node.Health, node.StatePath }
            .Where(static value => !string.IsNullOrWhiteSpace(value));
        return string.Join("  ", parts);
    }

    private static AquariumRenderPlan CreateRenderPlan()
    {
        var app = new AquariumApp();
        var scene = app.RenderTargets.Hdr("scene");
        app.Cameras.Perspective("main");
        app.Features.Presentation(scene.Color);
        app.Features.DirectWriteOverlay();
        app.Debug.View("Scene", scene.Color);
        return app.Plan;
    }
}

public sealed class EveSurfaceRuntimeFactory : IAquariumRuntimeFactory
{
    public IAquariumRuntime Create(AquariumRuntimeOptions options)
    {
        var runtime = new EveSurfaceRuntime();
        runtime.SetOptions(options);
        return runtime;
    }
}

internal sealed record EveSurfaceState(
    string ProviderId,
    string Title,
    long Version,
    string UpdatedAt,
    string SelectedNodeId,
    IReadOnlyList<EveSurfaceNode> Nodes,
    EveCompositionSurface? Surface)
{
    public static EveSurfaceState Empty { get; } = new(
        "",
        "Eve Surface",
        0,
        "",
        "",
        [],
        null);

    public static EveSurfaceState Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var nodes = new List<EveSurfaceNode>();
        if (root.TryGetProperty("nodes", out var nodeArray) && nodeArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodeArray.EnumerateArray())
            {
                nodes.Add(new EveSurfaceNode(
                    ReadString(node, "id"),
                    ReadString(node, "label"),
                    ReadString(node, "kind"),
                    ReadDouble(node, "x"),
                    ReadDouble(node, "y"),
                    ReadDouble(node, "width"),
                    ReadDouble(node, "height"),
                    ReadString(node, "health"),
                    ReadString(node, "detail"),
                    ReadString(node, "statePath"),
                    ReadString(node, "avatarUrl")));
            }
        }

        return new EveSurfaceState(
            ReadString(root, "providerId"),
            ReadString(root, "title", "Eve Surface"),
            ReadInt64(root, "version"),
            ReadString(root, "updatedAt"),
            ReadString(root, "selectedNodeId"),
            nodes,
            ReadSurface(root));
    }

    private static EveCompositionSurface? ReadSurface(JsonElement root)
    {
        if (!root.TryGetProperty("surface", out var surface) || surface.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new EveCompositionSurface(
            ReadString(surface, "schema"),
            ReadString(surface, "id"),
            ReadString(surface, "title"),
            surface.TryGetProperty("root", out var rootElement) ? ReadElement(rootElement) : null,
            surface.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array
                ? assets.EnumerateArray()
                    .Select(static asset => new EveCompositionAsset(
                        ReadString(asset, "id"),
                        ReadString(asset, "kind"),
                        ReadString(asset, "uri")))
                    .Where(static asset => !string.IsNullOrWhiteSpace(asset.Id))
                    .ToArray()
                : []);
    }

    private static EveCompositionElement? ReadElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var children = element.TryGetProperty("children", out var childArray) && childArray.ValueKind == JsonValueKind.Array
            ? childArray.EnumerateArray().Select(ReadElement).OfType<EveCompositionElement>().ToArray()
            : [];
        return new EveCompositionElement(
            ReadString(element, "id"),
            ReadString(element, "kind"),
            ReadString(element, "role"),
            ReadString(element, "text"),
            ReadString(element, "assetRef"),
            ReadString(element, "assetUri"),
            ReadString(element, "bindNodeId"),
            ReadString(element, "commandId"),
            children);
    }

    private static string ReadString(JsonElement element, string property, string fallback = "")
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static long ReadInt64(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetInt64(out var number)
            ? number
            : 0;
    }

    private static double ReadDouble(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetDouble(out var number)
            ? number
            : 0.0;
    }
}

internal sealed record EveSurfaceNode(
    string Id,
    string Label,
    string Kind,
    double X,
    double Y,
    double Width,
    double Height,
    string Health,
    string Detail,
    string StatePath,
    string AvatarUrl);

internal sealed record EveCompositionSurface(
    string Schema,
    string Id,
    string Title,
    EveCompositionElement? Root,
    IReadOnlyList<EveCompositionAsset> Assets);

internal sealed record EveCompositionElement(
    string Id,
    string Kind,
    string Role,
    string Text,
    string AssetRef,
    string AssetUri,
    string BindNodeId,
    string CommandId,
    IReadOnlyList<EveCompositionElement> Children);

internal sealed record EveCompositionAsset(string Id, string Kind, string Uri);
