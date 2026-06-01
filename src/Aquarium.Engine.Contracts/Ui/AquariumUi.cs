namespace Aquarium.Engine.Ui;

public sealed class AquariumUiDocument
{
    private readonly List<AquariumUiPanel> panels = [];
    private readonly List<AquariumUiSurface> surfaces = [];
    private readonly List<AquariumConsoleCommand> commands = [];

    public static AquariumUiDocument Empty { get; } = new();

    public IReadOnlyList<AquariumUiPanel> Panels => panels;

    public IReadOnlyList<AquariumUiSurface> Surfaces => surfaces;

    public IReadOnlyList<AquariumConsoleCommand> Commands => commands;

    public AquariumUiDocument Panel(string title, Action<AquariumUiPanelBuilder> compose)
    {
        return Panel(title, 18.0f, 82.0f, 360.0f, compose);
    }

    public AquariumUiDocument Panel(string title, float left, float top, float width, Action<AquariumUiPanelBuilder> compose)
    {
        return Panel(title, left, top, width, fadeWhenMouseDistant: false, compose);
    }

    public AquariumUiDocument Panel(string title, float left, float top, float width, bool fadeWhenMouseDistant, Action<AquariumUiPanelBuilder> compose)
    {
        var controls = new List<AquariumUiControl>();
        compose(new AquariumUiPanelBuilder(controls));
        panels.Add(new AquariumUiPanel(title, left, top, width, controls, fadeWhenMouseDistant));
        return this;
    }

    public AquariumUiDocument Surface(string id, string title, float left, float top, float width, float height, Action<AquariumUiSurfaceBuilder> compose)
    {
        return Surface(id, title, left, top, width, height, contentPadding: 8.0f, rootGap: 8.0f, rootPadding: 8.0f, compose);
    }

    public AquariumUiDocument Surface(
        string id,
        string title,
        float left,
        float top,
        float width,
        float height,
        float contentPadding,
        float rootGap,
        float rootPadding,
        Action<AquariumUiSurfaceBuilder> compose)
    {
        var children = new List<AquariumUiElement>();
        compose(new AquariumUiSurfaceBuilder(children));
        surfaces.Add(new AquariumUiSurface(
            "cultmesh.eve_surface.v0",
            id,
            title,
            new AquariumUiRect(left, top, width, height),
            new AquariumUiElement(id + ".root", "dashboard", title, Role: "root", Layout: AquariumUiLayout.Vertical(rootGap, rootPadding), Children: children),
            Math.Max(0.0f, contentPadding)));
        return this;
    }

    public AquariumUiDocument Command(string name, Func<IReadOnlyList<string>, string> execute, string description = "")
    {
        commands.Add(new AquariumConsoleCommand(name, execute, description));
        return this;
    }
}

public sealed record AquariumUiPanel(
    string Title,
    float Left,
    float Top,
    float Width,
    IReadOnlyList<AquariumUiControl> Controls,
    bool FadeWhenMouseDistant = false);

public sealed record AquariumUiSurface(
    string Schema,
    string Id,
    string Title,
    AquariumUiRect Bounds,
    AquariumUiElement Root,
    float ContentPadding = 8.0f);

public sealed record AquariumUiRect(float Left, float Top, float Width, float Height);

public sealed record AquariumUiLayout(
    string Direction = "vertical",
    float Gap = 8.0f,
    float Padding = 8.0f)
{
    public static AquariumUiLayout Vertical(float gap = 8.0f, float padding = 8.0f) => new("vertical", gap, padding);

    public static AquariumUiLayout Horizontal(float gap = 8.0f, float padding = 8.0f) => new("horizontal", gap, padding);
}

public sealed record AquariumUiStyle(string Variant = "default", string Tone = "neutral");

public sealed record AquariumUiPreviewItem(
    string Id,
    string Label,
    float X,
    float Y,
    float Width,
    float Height,
    bool Selected = false,
    string Tone = "neutral");

public readonly record struct AquariumUiPreviewInteraction(
    string ItemId,
    string Handle,
    string Phase,
    float X,
    float Y,
    float DeltaX,
    float DeltaY);

public readonly record struct AquariumUiPreviewState(
    string HoverItemId = "",
    string HoverHandle = "",
    string ActiveItemId = "",
    string ActiveHandle = "");

public readonly record struct AquariumUiPreviewGuide(
    string Axis,
    float Position,
    string Tone = "neutral");

public sealed record AquariumUiElement(
    string Id,
    string Kind,
    string? Text = null,
    string? Role = null,
    AquariumUiLayout? Layout = null,
    AquariumUiStyle? Style = null,
    Func<string>? ReadText = null,
    Func<double>? ReadMetric = null,
    Func<bool>? ReadToggle = null,
    Action<bool>? WriteToggle = null,
    Func<float>? ReadFloat = null,
    Action<float>? WriteFloat = null,
    float Min = 0.0f,
    float Max = 1.0f,
    string Format = "0.###",
    Func<int>? ReadOption = null,
    Action<int>? WriteOption = null,
    IReadOnlyList<AquariumUiOption>? Options = null,
    Func<IReadOnlyList<AquariumUiPreviewItem>>? ReadPreviewItems = null,
    Func<AquariumUiPreviewState>? ReadPreviewState = null,
    Func<IReadOnlyList<AquariumUiPreviewGuide>>? ReadPreviewGuides = null,
    bool PreviewBlitsOutputBuffer = false,
    Action<AquariumUiPreviewInteraction>? HandlePreviewInteraction = null,
    Action? Invoke = null,
    Func<bool>? IsVisible = null,
    float Weight = 1.0f,
    float? FixedExtent = null,
    IReadOnlyList<AquariumUiElement>? Children = null)
{
    public bool Visible => IsVisible?.Invoke() ?? true;
}

public sealed class AquariumUiSurfaceBuilder(List<AquariumUiElement> children)
{
    public AquariumUiSurfaceBuilder Vertical(string id, Action<AquariumUiSurfaceBuilder> compose, float weight = 1.0f, float gap = 8.0f, float padding = 0.0f, Func<bool>? isVisible = null, float? fixedExtent = null)
    {
        var scopeChildren = new List<AquariumUiElement>();
        compose(new AquariumUiSurfaceBuilder(scopeChildren));
        children.Add(new AquariumUiElement(id, "group", Layout: AquariumUiLayout.Vertical(gap, padding), IsVisible: isVisible, Weight: Math.Max(0.001f, weight), FixedExtent: fixedExtent, Children: scopeChildren));
        return this;
    }

    public AquariumUiSurfaceBuilder Horizontal(string id, Action<AquariumUiSurfaceBuilder> compose, float weight = 1.0f, float gap = 8.0f, float padding = 0.0f, Func<bool>? isVisible = null, float? fixedExtent = null)
    {
        var scopeChildren = new List<AquariumUiElement>();
        compose(new AquariumUiSurfaceBuilder(scopeChildren));
        children.Add(new AquariumUiElement(id, "group", Layout: AquariumUiLayout.Horizontal(gap, padding), IsVisible: isVisible, Weight: Math.Max(0.001f, weight), FixedExtent: fixedExtent, Children: scopeChildren));
        return this;
    }

    public AquariumUiSurfaceBuilder Row(string id, Action<AquariumUiSurfaceBuilder> compose, float weight = 1.0f, Func<bool>? isVisible = null)
    {
        return Horizontal(id, compose, weight, isVisible: isVisible);
    }

    public AquariumUiSurfaceBuilder Pane(string id, string title, Action<AquariumUiSurfaceBuilder> compose, string tone = "neutral", float weight = 1.0f, float? fixedExtent = null)
    {
        var paneChildren = new List<AquariumUiElement>();
        compose(new AquariumUiSurfaceBuilder(paneChildren));
        children.Add(new AquariumUiElement(id, "pane", title, Style: new AquariumUiStyle("panel", tone), Layout: AquariumUiLayout.Vertical(8.0f, 10.0f), Weight: Math.Max(0.001f, weight), FixedExtent: fixedExtent, Children: paneChildren));
        return this;
    }

    public AquariumUiSurfaceBuilder Card(string id, Action<AquariumUiSurfaceBuilder> compose, string tone = "neutral", float weight = 1.0f)
    {
        var cardChildren = new List<AquariumUiElement>();
        compose(new AquariumUiSurfaceBuilder(cardChildren));
        children.Add(new AquariumUiElement(id, "card", Style: new AquariumUiStyle("compact", tone), Layout: AquariumUiLayout.Vertical(4.0f, 8.0f), Weight: Math.Max(0.001f, weight), Children: cardChildren));
        return this;
    }

    public AquariumUiSurfaceBuilder Text(string id, string text, string role = "body", float weight = 1.0f, Func<bool>? isVisible = null)
    {
        children.Add(new AquariumUiElement(id, "text", text, role, IsVisible: isVisible, Weight: Math.Max(0.001f, weight)));
        return this;
    }

    public AquariumUiSurfaceBuilder Text(string id, Func<string> read, string role = "body", float weight = 1.0f, Func<bool>? isVisible = null)
    {
        children.Add(new AquariumUiElement(id, "text", Role: role, ReadText: read, IsVisible: isVisible, Weight: Math.Max(0.001f, weight)));
        return this;
    }

    public AquariumUiSurfaceBuilder Metric(string id, string label, Func<double> read, string tone = "neutral", float weight = 1.0f)
    {
        children.Add(new AquariumUiElement(id, "metric", label, Style: new AquariumUiStyle("compact", tone), ReadMetric: read, Weight: Math.Max(0.001f, weight)));
        return this;
    }

    public AquariumUiSurfaceBuilder Toggle(string id, string label, Func<bool> read, Action<bool> write, float weight = 1.0f)
    {
        children.Add(new AquariumUiElement(id, "toggle", label, ReadToggle: read, WriteToggle: write, Weight: Math.Max(0.001f, weight)));
        return this;
    }

    public AquariumUiSurfaceBuilder Slider(string id, string label, Func<float> read, Action<float> write, float min, float max, string format = "0.###", float weight = 1.0f)
    {
        children.Add(new AquariumUiElement(id, "slider", label, ReadFloat: read, WriteFloat: write, Min: min, Max: max, Format: format, Weight: Math.Max(0.001f, weight)));
        return this;
    }

    public AquariumUiSurfaceBuilder Options(string id, string label, Func<int> read, Action<int> write, IReadOnlyList<AquariumUiOption> options, float weight = 1.0f)
    {
        children.Add(new AquariumUiElement(id, "select", label, ReadOption: read, WriteOption: write, Options: options, Weight: Math.Max(0.001f, weight)));
        return this;
    }

    public AquariumUiSurfaceBuilder Button(string id, string label, Action invoke, float weight = 1.0f, Func<bool>? isVisible = null)
    {
        children.Add(new AquariumUiElement(id, "button", label, Invoke: invoke, IsVisible: isVisible, Weight: Math.Max(0.001f, weight)));
        return this;
    }

    public AquariumUiSurfaceBuilder Preview(
        string id,
        string label,
        Func<IReadOnlyList<AquariumUiPreviewItem>> readItems,
        float weight = 1.0f,
        bool blitOutputBuffer = false,
        Action<AquariumUiPreviewInteraction>? handleInteraction = null,
        Func<AquariumUiPreviewState>? readState = null,
        Func<IReadOnlyList<AquariumUiPreviewGuide>>? readGuides = null)
    {
        children.Add(new AquariumUiElement(
            id,
            "preview",
            label,
            ReadPreviewItems: readItems,
            ReadPreviewState: readState,
            ReadPreviewGuides: readGuides,
            PreviewBlitsOutputBuffer: blitOutputBuffer,
            HandlePreviewInteraction: handleInteraction,
            Weight: Math.Max(0.001f, weight)));
        return this;
    }
}

public sealed class AquariumUiPanelBuilder(List<AquariumUiControl> controls)
{
    public AquariumUiPanelBuilder Section(string title, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiSection(title, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder Button(string label, Action action, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiButton(label, action, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder Toggle(string label, Func<bool> read, Action<bool> write, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiToggle(label, read, write, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder TreeItem(string label, int depth, bool canExpand, Func<bool> isExpanded, Action<bool> setExpanded, Func<bool> isSelected, Action select, string? detail = null, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiTreeItem(label, Math.Max(0, depth), canExpand, isExpanded, setExpanded, isSelected, select, detail, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder Slider(string label, Func<float> read, Action<float> write, float min, float max, string format = "0.###", string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiFloatSlider(label, read, write, min, max, format, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder Slider(string label, Func<int> read, Action<int> write, int min, int max, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiIntSlider(label, read, write, min, max, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder Options(string label, Func<int> read, Action<int> write, IReadOnlyList<AquariumUiOption> options, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiOptions(label, read, write, options, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder Text(string label, Func<string> read, Action<string> write, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiText(label, read, write, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder TextBox(string label, Func<string> read, Action<string> write, int lines = 3, bool acceptsReturn = true, Action? submit = null, bool monospace = false, bool alignBottom = false, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiTextBox(label, read, write, Math.Max(1, lines), acceptsReturn, submit, monospace, alignBottom, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder Readout(string label, Func<string> read, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiReadout(label, read, tooltip, isVisible));
        return this;
    }

    public AquariumUiPanelBuilder MelSpectrumStack(string label, Func<IReadOnlyList<AquariumUiMelSpectrumLane>> read, int laneBins = 64, float laneHeight = 112.0f, string? tooltip = null, Func<bool>? isVisible = null)
    {
        controls.Add(new AquariumUiMelSpectrumStack(label, read, Math.Clamp(laneBins, 8, 256), Math.Clamp(laneHeight, 64.0f, 220.0f), tooltip, isVisible));
        return this;
    }
}

public readonly record struct AquariumUiOption(int Value, string Label);

public sealed record AquariumConsoleCommand(string Name, Func<IReadOnlyList<string>, string> Execute, string Description = "");

public abstract record AquariumUiControl(string Label, string? Tooltip, Func<bool>? IsVisible)
{
    public bool Visible => IsVisible?.Invoke() ?? true;
}

public sealed record AquariumUiSection(string Label, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, null, IsVisible);

public sealed record AquariumUiButton(string Label, Action Action, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiToggle(string Label, Func<bool> Read, Action<bool> Write, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiTreeItem(string Label, int Depth, bool CanExpand, Func<bool> IsExpanded, Action<bool> SetExpanded, Func<bool> IsSelected, Action Select, string? Detail = null, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiFloatSlider(string Label, Func<float> Read, Action<float> Write, float Min, float Max, string Format = "0.###", string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiIntSlider(string Label, Func<int> Read, Action<int> Write, int Min, int Max, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiOptions(string Label, Func<int> Read, Action<int> Write, IReadOnlyList<AquariumUiOption> Options, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiText(string Label, Func<string> Read, Action<string> Write, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiTextBox(string Label, Func<string> Read, Action<string> Write, int Lines = 3, bool AcceptsReturn = true, Action? Submit = null, bool Monospace = false, bool AlignBottom = false, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiReadout(string Label, Func<string> Read, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);

public sealed record AquariumUiMelSpectrumLane(
    string Label,
    string SourceId,
    double Rms,
    double Peak,
    double NoiseFloorDb,
    IReadOnlyList<double> MelDecibels,
    IReadOnlyList<string> Peaks);

public sealed record AquariumUiMelSpectrumStack(string Label, Func<IReadOnlyList<AquariumUiMelSpectrumLane>> Read, int LaneBins = 64, float LaneHeight = 112.0f, string? Tooltip = null, Func<bool>? IsVisible = null)
    : AquariumUiControl(Label, Tooltip, IsVisible);
