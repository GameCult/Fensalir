using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using Aquarium.Engine.Render.Ui;
using Aquarium.Engine.Ui;
using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using DWriteFactoryType = Vortice.DirectWrite.FactoryType;

namespace Aquarium.Engine.Render;

internal sealed class DirectWriteOverlay : IDisposable
{
    private readonly ID2D1Factory direct2DFactory;
    private readonly IDWriteFactory6 directWriteFactory;
    private readonly IDWriteFontCollection fontCollection;
    private readonly IDWriteTypography smallCapsTypography;
    private readonly ID2D1RenderTarget renderTarget;
    private readonly ID2D1SolidColorBrush primaryTextBrush;
    private readonly ID2D1SolidColorBrush quietTextBrush;
    private readonly ID2D1SolidColorBrush panelBrush;
    private readonly ID2D1SolidColorBrush rowBrush;
    private readonly ID2D1SolidColorBrush hoverRowBrush;
    private readonly ID2D1SolidColorBrush activeRowBrush;
    private readonly ID2D1SolidColorBrush outlineBrush;
    private readonly ID2D1SolidColorBrush accentBrush;
    private readonly ID2D1SolidColorBrush accentHoverBrush;
    private readonly ID2D1SolidColorBrush accentActiveBrush;
    private readonly ID2D1SolidColorBrush dimAccentBrush;
    private readonly ID2D1SolidColorBrush trackHoverBrush;
    private readonly ID2D1SolidColorBrush trackActiveBrush;
    private readonly IDWriteTextFormat titleFormat;
    private readonly IDWriteTextFormat smallFormat;
    private readonly IDWriteTextFormat monospaceFormat;
    private readonly int width;
    private readonly int height;

    public DirectWriteOverlay(IDXGISurface backBufferSurface, int width, int height)
    {
        this.width = width;
        this.height = height;

        direct2DFactory = D2D1.D2D1CreateFactory<ID2D1Factory>(D2DFactoryType.SingleThreaded);
        directWriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory6>(DWriteFactoryType.Shared);
        fontCollection = CreateBrandFontCollection();
        smallCapsTypography = directWriteFactory.CreateTypography();
        smallCapsTypography.AddFontFeature(new FontFeature
        {
            NameTag = FontFeatureTag.SmallCapitalsFromCapitals,
            Parameter = 1
        });
        smallCapsTypography.AddFontFeature(new FontFeature
        {
            NameTag = FontFeatureTag.CapitalSpacing,
            Parameter = 1
        });

        var renderTargetProperties = new RenderTargetProperties(
            RenderTargetType.Default,
            new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore),
            96.0f,
            96.0f,
            RenderTargetUsage.None,
            FeatureLevel.Default);
        renderTarget = direct2DFactory.CreateDxgiSurfaceRenderTarget(backBufferSurface, renderTargetProperties);
        renderTarget.AntialiasMode = AntialiasMode.PerPrimitive;
        renderTarget.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;

        primaryTextBrush = renderTarget.CreateSolidColorBrush(new Color4(0.88f, 0.96f, 1.0f, 0.92f));
        quietTextBrush = renderTarget.CreateSolidColorBrush(new Color4(0.54f, 0.68f, 0.72f, 0.72f));
        panelBrush = renderTarget.CreateSolidColorBrush(new Color4(0.018f, 0.022f, 0.032f, 0.96f));
        rowBrush = renderTarget.CreateSolidColorBrush(new Color4(0.085f, 0.084f, 0.12f, 0.96f));
        hoverRowBrush = renderTarget.CreateSolidColorBrush(new Color4(0.13f, 0.13f, 0.18f, 0.98f));
        activeRowBrush = renderTarget.CreateSolidColorBrush(new Color4(0.19f, 0.18f, 0.24f, 0.99f));
        outlineBrush = renderTarget.CreateSolidColorBrush(new Color4(0.28f, 0.34f, 0.36f, 0.72f));
        accentBrush = renderTarget.CreateSolidColorBrush(new Color4(1.0f, 0.38f, 0.055f, 0.96f));
        accentHoverBrush = renderTarget.CreateSolidColorBrush(new Color4(1.0f, 0.58f, 0.30f, 0.98f));
        accentActiveBrush = renderTarget.CreateSolidColorBrush(new Color4(1.0f, 0.78f, 0.58f, 1.0f));
        dimAccentBrush = renderTarget.CreateSolidColorBrush(new Color4(0.20f, 0.20f, 0.22f, 0.95f));
        trackHoverBrush = renderTarget.CreateSolidColorBrush(new Color4(0.32f, 0.29f, 0.29f, 0.98f));
        trackActiveBrush = renderTarget.CreateSolidColorBrush(new Color4(0.45f, 0.38f, 0.34f, 1.0f));
        titleFormat = CreateTextFormat("Montserrat", 18.0f, FontWeight.Thin);
        smallFormat = CreateTextFormat("Ubuntu Sans", 11.0f, FontWeight.Regular);
        monospaceFormat = CreateTextFormat("Ubuntu Sans Mono", 11.0f, FontWeight.Regular, ParagraphAlignment.Near);
    }

    public void Render(AquariumFrame frame, int renderDebugMode, DebugUi? debugUi, IReadOnlyList<DebugUi> clientUiPanels, IReadOnlyList<AquariumUiSurface> clientUiSurfaces, string performanceText)
    {
        renderTarget.BeginDraw();
        DrawPerformanceCounter(performanceText);
        if (debugUi is not null)
        {
            DrawPanel(debugUi, 1.0f);
        }

        foreach (var panel in clientUiPanels)
        {
            DrawPanel(panel, panel.DrawOpacity());
        }

        foreach (var surface in clientUiSurfaces)
        {
            DrawSurface(surface);
        }

        renderTarget.EndDraw();
    }

    private void DrawPerformanceCounter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var right = width - 12.0f;
        var top = 12.0f;
        var bounds = RectFromEdges(Math.Max(12.0f, right - 152.0f), top, right, top + 28.0f);
        renderTarget.FillRectangle(bounds, panelBrush);
        renderTarget.DrawRectangle(bounds, outlineBrush, 1.0f);
        renderTarget.DrawText(text, smallFormat, RectFromEdges(bounds.Left + 8.0f, bounds.Top, bounds.Right - 8.0f, bounds.Bottom), primaryTextBrush, DrawTextOptions.Clip);
    }

    private void DrawPanel(DebugUi panel, float opacity)
    {
        var brushes = new ID2D1SolidColorBrush[]
        {
            panelBrush,
            rowBrush,
            hoverRowBrush,
            activeRowBrush,
            outlineBrush,
            primaryTextBrush,
            quietTextBrush,
            accentBrush,
            accentHoverBrush,
            accentActiveBrush,
            dimAccentBrush,
            trackHoverBrush,
            trackActiveBrush,
        };
        var originalOpacity = new float[brushes.Length];
        for (var index = 0; index < brushes.Length; index++)
        {
            originalOpacity[index] = brushes[index].Opacity;
            brushes[index].Opacity = originalOpacity[index] * opacity;
        }

        try
        {
            panel.Draw(
                renderTarget,
                directWriteFactory,
                titleFormat,
                smallFormat,
                monospaceFormat,
                panelBrush,
                rowBrush,
                hoverRowBrush,
                activeRowBrush,
                outlineBrush,
                primaryTextBrush,
                quietTextBrush,
                accentBrush,
                accentHoverBrush,
                accentActiveBrush,
                dimAccentBrush,
                trackHoverBrush,
                trackActiveBrush,
                width,
                height);
        }
        finally
        {
            for (var index = 0; index < brushes.Length; index++)
            {
                brushes[index].Opacity = originalOpacity[index];
            }
        }
    }

    private void DrawSurface(AquariumUiSurface surface)
    {
        var bounds = RectFromEdges(
            Math.Clamp(surface.Bounds.Left, 8.0f, Math.Max(8.0f, width - 80.0f)),
            Math.Clamp(surface.Bounds.Top, 8.0f, Math.Max(8.0f, height - 48.0f)),
            Math.Clamp(surface.Bounds.Left + surface.Bounds.Width, 88.0f, width - 8.0f),
            Math.Clamp(surface.Bounds.Top + surface.Bounds.Height, 56.0f, height - 8.0f));
        renderTarget.FillRectangle(bounds, panelBrush);
        renderTarget.DrawRectangle(bounds, outlineBrush, 1.0f);
        DrawHeader(surface.Title, titleFormat, RectFromEdges(bounds.Left + 12.0f, bounds.Top + 8.0f, bounds.Right - 12.0f, bounds.Top + 34.0f), primaryTextBrush);
        var content = RectFromEdges(bounds.Left + 8.0f, bounds.Top + 42.0f, bounds.Right - 8.0f, bounds.Bottom - 8.0f);
        DrawSurfaceChildren(surface.Root.Children ?? [], content, surface.Root.Layout ?? AquariumUiLayout.Vertical());
    }

    private void DrawSurfaceChildren(IReadOnlyList<AquariumUiElement> elements, Rect bounds, AquariumUiLayout layout)
    {
        var visible = elements.Where(static element => element.Visible).ToArray();
        if (visible.Length == 0)
        {
            return;
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
        var preferred = visible.Select(element => PreferredExtent(element, horizontal)).ToArray();
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
            DrawSurfaceElement(element, childBounds);
            cursor += extent + layout.Gap;
        }
    }

    private void DrawSurfaceElement(AquariumUiElement element, Rect bounds)
    {
        switch (element.Kind)
        {
            case "group":
                DrawSurfaceChildren(element.Children ?? [], bounds, element.Layout ?? AquariumUiLayout.Vertical());
                break;
            case "pane":
                DrawSurfaceFrame(bounds, element.Text ?? element.Id, element.Style?.Tone ?? "neutral", drawTitle: true);
                DrawSurfaceChildren(element.Children ?? [], RectFromEdges(bounds.Left + 6.0f, bounds.Top + 32.0f, bounds.Right - 6.0f, bounds.Bottom - 6.0f), element.Layout ?? AquariumUiLayout.Vertical());
                break;
            case "card":
                DrawSurfaceFrame(bounds, null, element.Style?.Tone ?? "neutral", drawTitle: false);
                DrawSurfaceChildren(element.Children ?? [], bounds, element.Layout ?? AquariumUiLayout.Vertical(4.0f, 8.0f));
                break;
            case "metric":
                DrawMetricElement(element, bounds);
                break;
            case "toggle":
                DrawToggleElement(element, bounds);
                break;
            case "slider":
                DrawSliderElement(element, bounds);
                break;
            case "select":
                DrawSelectElement(element, bounds);
                break;
            case "button":
                DrawButtonElement(element, bounds);
                break;
            default:
                DrawTextElement(element, bounds);
                break;
        }
    }

    private void DrawSurfaceFrame(Rect bounds, string? titleText, string tone, bool drawTitle)
    {
        renderTarget.FillRectangle(bounds, tone == "danger" ? activeRowBrush : tone == "warm" ? hoverRowBrush : rowBrush);
        renderTarget.DrawRectangle(bounds, ToneBrush(tone), 1.0f);
        if (drawTitle && !string.IsNullOrWhiteSpace(titleText))
        {
            DrawHeader(titleText, smallFormat, RectFromEdges(bounds.Left + 8.0f, bounds.Top + 6.0f, bounds.Right - 8.0f, bounds.Top + 28.0f), accentBrush);
        }
    }

    private void DrawTextElement(AquariumUiElement element, Rect bounds)
    {
        var text = element.ReadText?.Invoke() ?? element.Text ?? "";
        var format = element.Role is "mono" ? monospaceFormat : smallFormat;
        var brush = element.Role is "caption" ? quietTextBrush : element.Role is "strong" or "title" ? primaryTextBrush : primaryTextBrush;
        renderTarget.DrawText(text, format, bounds, brush, DrawTextOptions.Clip);
    }

    private void DrawMetricElement(AquariumUiElement element, Rect bounds)
    {
        var value = element.ReadMetric?.Invoke() ?? 0.0;
        var normalized = Math.Clamp(value, 0.0, 1.0);
        DrawSurfaceFrame(bounds, null, element.Style?.Tone ?? "neutral", drawTitle: false);
        renderTarget.DrawText($"{element.Text}: {value:0.000}", smallFormat, RectFromEdges(bounds.Left + 8.0f, bounds.Top + 2.0f, bounds.Right - 8.0f, bounds.Top + 18.0f), primaryTextBrush, DrawTextOptions.Clip);
        var track = RectFromEdges(bounds.Left + 8.0f, bounds.Bottom - 10.0f, bounds.Right - 8.0f, bounds.Bottom - 5.0f);
        renderTarget.FillRectangle(track, dimAccentBrush);
        renderTarget.FillRectangle(RectFromEdges(track.Left, track.Top, track.Left + track.Width * (float)normalized, track.Bottom), ToneBrush(element.Style?.Tone ?? "neutral"));
    }

    private void DrawToggleElement(AquariumUiElement element, Rect bounds)
    {
        var isOn = element.ReadToggle?.Invoke() ?? false;
        DrawSurfaceFrame(bounds, null, isOn ? "cool" : "neutral", drawTitle: false);
        renderTarget.DrawText(element.Text ?? "", smallFormat, RectFromEdges(bounds.Left + 8.0f, bounds.Top, bounds.Right - 36.0f, bounds.Bottom), primaryTextBrush, DrawTextOptions.Clip);
        var box = RectFromEdges(bounds.Right - 26.0f, bounds.Top + 7.0f, bounds.Right - 10.0f, bounds.Top + 23.0f);
        renderTarget.DrawRectangle(box, isOn ? accentBrush : outlineBrush, 1.0f);
        if (isOn)
        {
            renderTarget.FillRectangle(RectFromEdges(box.Left + 3.0f, box.Top + 3.0f, box.Right - 3.0f, box.Bottom - 3.0f), accentBrush);
        }
    }

    private void DrawSliderElement(AquariumUiElement element, Rect bounds)
    {
        var raw = element.ReadFloat?.Invoke() ?? element.Min;
        var span = Math.Max(0.0001f, element.Max - element.Min);
        var normalized = Math.Clamp((raw - element.Min) / span, 0.0f, 1.0f);
        DrawSurfaceFrame(bounds, null, "neutral", drawTitle: false);
        renderTarget.DrawText($"{element.Text}: {raw.ToString(element.Format, System.Globalization.CultureInfo.InvariantCulture)}", smallFormat, RectFromEdges(bounds.Left + 8.0f, bounds.Top, bounds.Left + 170.0f, bounds.Bottom), primaryTextBrush, DrawTextOptions.Clip);
        var track = RectFromEdges(bounds.Left + 178.0f, bounds.Top + 13.0f, bounds.Right - 12.0f, bounds.Top + 18.0f);
        renderTarget.FillRectangle(track, dimAccentBrush);
        renderTarget.FillRectangle(RectFromEdges(track.Left, track.Top, track.Left + track.Width * normalized, track.Bottom), accentBrush);
    }

    private void DrawSelectElement(AquariumUiElement element, Rect bounds)
    {
        var selected = element.ReadOption?.Invoke() ?? 0;
        var label = element.Options?.FirstOrDefault(option => option.Value == selected).Label ?? selected.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DrawSurfaceFrame(bounds, null, "neutral", drawTitle: false);
        renderTarget.DrawText(element.Text ?? "", smallFormat, RectFromEdges(bounds.Left + 8.0f, bounds.Top, bounds.Left + 130.0f, bounds.Bottom), accentBrush, DrawTextOptions.Clip);
        renderTarget.DrawText(label, smallFormat, RectFromEdges(bounds.Left + 138.0f, bounds.Top, bounds.Right - 8.0f, bounds.Bottom), primaryTextBrush, DrawTextOptions.Clip);
    }

    private void DrawButtonElement(AquariumUiElement element, Rect bounds)
    {
        DrawSurfaceFrame(bounds, null, "warm", drawTitle: false);
        renderTarget.DrawText(element.Text ?? "", smallFormat, RectFromEdges(bounds.Left + 8.0f, bounds.Top, bounds.Right - 8.0f, bounds.Bottom), primaryTextBrush, DrawTextOptions.Clip);
    }

    private ID2D1SolidColorBrush ToneBrush(string tone) =>
        tone switch
        {
            "cool" => primaryTextBrush,
            "warm" => accentBrush,
            "danger" => accentActiveBrush,
            _ => outlineBrush,
        };

    private static float? PreferredExtent(AquariumUiElement element, bool horizontal)
    {
        if (horizontal)
        {
            return null;
        }

        var weight = Math.Max(0.001f, element.Weight);
        return element.Kind switch
        {
            "group" or "pane" or "card" => PreferredContainerExtent(element) * weight,
            "text" when element.Role is "mono" => 18.0f * weight,
            "text" when element.Role is "strong" or "title" => 22.0f * weight,
            "text" => 18.0f * weight,
            "toggle" or "select" or "slider" => 24.0f * weight,
            "button" => 30.0f * weight,
            "metric" => 36.0f * weight,
            _ => null,
        };
    }

    private static float PreferredContainerExtent(AquariumUiElement element)
    {
        var layout = element.Layout ?? AquariumUiLayout.Vertical();
        var children = element.Children?.Where(static child => child.Visible).ToArray() ?? [];
        if (children.Length == 0)
        {
            return element.Kind == "pane" ? 72.0f : 24.0f;
        }

        var horizontal = string.Equals(layout.Direction, "horizontal", StringComparison.Ordinal);
        var childExtents = children.Select(static child => PreferredExtent(child, horizontal: false) ?? 48.0f * Math.Max(0.001f, child.Weight)).ToArray();
        var contentExtent = horizontal
            ? childExtents.Max()
            : childExtents.Sum() + layout.Gap * Math.Max(0, childExtents.Length - 1);
        contentExtent += layout.Padding * 2.0f;

        return element.Kind switch
        {
            "pane" => contentExtent + 38.0f,
            "card" => contentExtent,
            _ => contentExtent,
        };
    }

    public void Dispose()
    {
        smallFormat.Dispose();
        monospaceFormat.Dispose();
        titleFormat.Dispose();
        smallCapsTypography.Dispose();
        fontCollection.Dispose();
        dimAccentBrush.Dispose();
        trackActiveBrush.Dispose();
        trackHoverBrush.Dispose();
        accentActiveBrush.Dispose();
        accentHoverBrush.Dispose();
        accentBrush.Dispose();
        outlineBrush.Dispose();
        activeRowBrush.Dispose();
        hoverRowBrush.Dispose();
        rowBrush.Dispose();
        panelBrush.Dispose();
        quietTextBrush.Dispose();
        primaryTextBrush.Dispose();
        renderTarget.Dispose();
        directWriteFactory.Dispose();
        direct2DFactory.Dispose();
    }

    private IDWriteTextFormat CreateTextFormat(string familyName, float size, FontWeight weight, ParagraphAlignment paragraphAlignment = ParagraphAlignment.Center)
    {
        var format = directWriteFactory.CreateTextFormat(
            familyName,
            fontCollection,
            weight,
            FontStyle.Normal,
            FontStretch.Normal,
            size,
            "en-us");
        format.WordWrapping = WordWrapping.NoWrap;
        format.ParagraphAlignment = paragraphAlignment;
        return format;
    }

    private IDWriteFontCollection CreateBrandFontCollection()
    {
        using var builder = directWriteFactory.CreateFontSetBuilder();
        builder.AddFontFile(FontAssetPath("Montserrat[wght].ttf"));
        builder.AddFontFile(FontAssetPath("UbuntuSans[wdth,wght].ttf"));
        builder.AddFontFile(FontAssetPath("UbuntuSansMono.ttf"));
        using var fontSet = builder.CreateFontSet();
        return directWriteFactory.CreateFontCollectionFromFontSet(fontSet, FontFamilyModel.Typographic);
    }

    private void DrawHeader(string text, IDWriteTextFormat format, Rect bounds, ID2D1Brush brush)
    {
        var displayText = text.ToUpperInvariant();
        using var layout = directWriteFactory.CreateTextLayout(displayText, format, bounds.Width, bounds.Height);
        layout.SetTypography(smallCapsTypography, new TextRange(0, (uint)displayText.Length));
        renderTarget.DrawTextLayout(new System.Numerics.Vector2(bounds.Left, bounds.Top), layout, brush, DrawTextOptions.Clip);
    }

    private static string FontAssetPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", fileName);
    }

    private static Rect RectFromEdges(float left, float top, float right, float bottom)
    {
        return new Rect(left, top, Math.Max(0.0f, right - left), Math.Max(0.0f, bottom - top));
    }
}
