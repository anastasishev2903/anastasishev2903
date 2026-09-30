using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Every layer of one generated button, handed back so a caller can restyle it
/// later through a reference instead of a name lookup.
/// </summary>
public sealed class RoadButtonParts
{
    public RectTransform Root;
    public Button Button;
    public Image Edge;
    public Image Fill;
    public Image Icon;
    public TextMeshProUGUI Caption;
}

/// <summary>
/// The UGUI construction kit for the delivery road: a dark card inside a bright
/// gold rim, flat fills, no gradients. Every label it builds has word wrap OFF,
/// autosize ON and a floor above the readable minimum, so a line breaks only
/// where the string says so (rules C.10 and C.12).
/// </summary>
public static class RoadUi
{
    public const float LabelFloor = 30f;
    public const float RimPixels = 5f;
    public static RectTransform Node(Transform parent, string label, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        GameObject go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    public static Image Block(Transform parent, string label, Vector2 anchor, Vector2 offset, Vector2 size, Sprite sprite, Color colour)
    {
        RectTransform rect = Node(parent, label, anchor, offset, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced;
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>A full-stretch layer over its parent - shades, veils, overlays.</summary>
    public static Image Sheet(Transform parent, string label, Sprite sprite, Color colour, bool blocksTaps)
    {
        Image image = Block(parent, label, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, sprite, colour);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        image.raycastTarget = blocksTaps;
        return image;
    }

    /// <summary>
    /// A road card: a bright rim with a dark body inset inside it. The BODY comes
    /// back, so children added to it draw over the fill, which is the order rule
    /// E.1 asks for.
    /// </summary>
    public static RectTransform Plate(Transform parent, string label, Vector2 anchor, Vector2 offset, Vector2 size, Sprite sprite, Color body, Color rim)
    {
        RectTransform host = Node(parent, label, anchor, offset, size);
        Block(host, "PlateRim", new Vector2(0.5f, 0.5f), Vector2.zero, size, sprite, rim);
        Image inner = Block(host, "PlateBody", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(RimPixels * 2f, RimPixels * 2f), sprite, body);
        return inner.rectTransform;
    }

    public static TextMeshProUGUI Label(Transform parent, string label, string text, Vector2 anchor, Vector2 offset, Vector2 size, float sizeMax, float sizeMin, Color colour, TMP_FontAsset font, TextAlignmentOptions align)
    {
        RectTransform rect = Node(parent, label, anchor, offset, size);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.color = colour;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        Wrap(tmp, sizeMax, sizeMin);
        return tmp;
    }

    /// <summary>
    /// Word wrap OFF, autosize ON, floor never below the readable minimum. Where a
    /// line ends is the author's decision and lives in the string as a newline.
    /// </summary>
    public static void Wrap(TMP_Text tmp, float sizeMax, float sizeMin)
    {
        if (tmp == null)
            return;
        float floor = Mathf.Max(LabelFloor, sizeMin);
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = Mathf.Max(floor, sizeMax);
        tmp.fontSizeMin = floor;
        tmp.fontSize = Mathf.Max(floor, sizeMax);
    }

    /// <summary>
    /// A tappable road plate. Children go rim, then fill, then icon, then caption,
    /// so the label is the last sibling and is always drawn on top (rule C.13).
    /// </summary>
    public static RoadButtonParts CtaParts(Transform parent, string label, string caption, Vector2 anchor, Vector2 offset, Vector2 size, Sprite plate, Sprite icon, Color fill, Color rim, Color captionColour, TMP_FontAsset font, float captionSize)
    {
        RectTransform host = Node(parent, label, anchor, offset, size);
        RoadButtonParts parts = new RoadButtonParts();
        parts.Root = host;
        // Every Image inside this template's BASE_PANEL ships with raycastTarget off,
        // so a button needs a graphic of its own that accepts the pointer. A fully
        // transparent one is culled by the raycaster, hence the sliver of alpha.
        Image hit = host.gameObject.AddComponent<Image>();
        hit.sprite = plate;
        hit.type = plate == null ? Image.Type.Simple : Image.Type.Sliced;
        hit.color = new Color(1f, 1f, 1f, 0.004f);
        hit.raycastTarget = true;
        hit.canvasRenderer.cullTransparentMesh = false;
        parts.Edge = Block(host, "CtaRim", new Vector2(0.5f, 0.5f), Vector2.zero, size, plate, rim);
        parts.Fill = Block(host, "CtaFill", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(RimPixels * 2f, RimPixels * 2f), plate, fill);
        float textShift = 0f;
        if (icon != null)
        {
            float side = Mathf.Min(48f, size.y * 0.5f);
            parts.Icon = Block(host, "CtaIcon", new Vector2(0.5f, 0.5f), new Vector2(string.IsNullOrEmpty(caption) ? 0f : -(size.x * 0.5f - side * 0.85f), 0f), new Vector2(side, side), icon, captionColour);
            parts.Icon.type = Image.Type.Simple;
            textShift = string.IsNullOrEmpty(caption) ? 0f : side * 0.6f;
        }

        if (!string.IsNullOrEmpty(caption))
            parts.Caption = Label(host, "CtaText", caption, new Vector2(0.5f, 0.5f), new Vector2(textShift, 0f), new Vector2(size.x - 56f - textShift, size.y - 26f), captionSize, captionSize * 0.72f, captionColour, font, TextAlignmentOptions.Center);
        // The press tint multiplies the FILL. Tinting the near invisible hit layer
        // would be no feedback at all, and rule C.7 wants a press to be seen.
        Button button = host.gameObject.AddComponent<Button>();
        button.targetGraphic = parts.Fill != null ? parts.Fill : (Graphic)hit;
        ColorBlock colours = button.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = Color.white;
        colours.pressedColor = new Color(0.68f, 0.72f, 0.8f, 1f);
        colours.selectedColor = Color.white;
        colours.disabledColor = new Color(0.42f, 0.45f, 0.52f, 0.7f);
        colours.colorMultiplier = 1f;
        colours.fadeDuration = 0.06f;
        button.colors = colours;
        parts.Button = button;
        return parts;
    }

    public static Button Cta(Transform parent, string label, string caption, Vector2 anchor, Vector2 offset, Vector2 size, Sprite plate, Sprite icon, Color fill, Color rim, Color captionColour, TMP_FontAsset font, float captionSize)
    {
        return CtaParts(parent, label, caption, anchor, offset, size, plate, icon, fill, rim, captionColour, font, captionSize).Button;
    }

    /// <summary>A three-layer bar: gold rim, dark track, accent fill pinned left.</summary>
    public static Image Bar(Transform parent, string label, Vector2 anchor, Vector2 offset, Vector2 size, Sprite plate, Color track, Color rim, Color fill)
    {
        RectTransform host = Node(parent, label, anchor, offset, size);
        Block(host, "BarRim", new Vector2(0.5f, 0.5f), Vector2.zero, size, plate, rim);
        Block(host, "BarTrack", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(6f, 6f), plate, track);
        RectTransform fillHost = Node(host, "BarFillHost", new Vector2(0f, 0.5f), new Vector2(3f, 0f), size - new Vector2(6f, 6f));
        fillHost.pivot = new Vector2(0f, 0.5f);
        fillHost.anchorMin = new Vector2(0f, 0.5f);
        fillHost.anchorMax = new Vector2(0f, 0.5f);
        Image progress = fillHost.gameObject.AddComponent<Image>();
        progress.sprite = plate;
        progress.type = plate == null ? Image.Type.Simple : Image.Type.Sliced;
        progress.color = fill;
        progress.raycastTarget = false;
        return progress;
    }

    public static void SetBar(Image fillImage, float fraction, float fullWidth)
    {
        if (fillImage == null)
            return;
        float clamped = Mathf.Clamp01(fraction);
        Vector2 size = fillImage.rectTransform.sizeDelta;
        fillImage.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, fullWidth * clamped), size.y);
    }

    /// <summary>
    /// A fresh RectTransform starts 100x100 at the centre, which would silently
    /// resolve every child anchor against 100x100 instead of the real canvas. This
    /// makes a host that covers its parent exactly.
    /// </summary>
    public static RectTransform Host(Transform parent, string label)
    {
        GameObject go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }

    // The scene template ships seven tutorial panels carrying filler copy. This game
    // keeps the template tutorial switched off and explains its gesture on its own
    // HOW TO card plus a permanent hint in the game scene, but leaving that filler
    // in place risks it reaching a screen (rule C.15) - so all seven are rewritten.
    private static readonly string[] StepHeaders =
    {
        "PICK THE ORDER",
        "CHANGE LANE",
        "MIND THE FEATHERS",
    };
    private static readonly string[] StepBodies =
    {
        "THE STRIP ON TOP SHOWS\nWHICH PARCEL COMES NEXT",
        "SWIPE OR TAP LEFT / RIGHT\nTO MOVE BETWEEN LANES",
        "A WRONG PARCEL OR A CRASH\nCOSTS ONE OF THREE",
    };
    public static void SanitiseTutorials(TMP_FontAsset font)
    {
        PanelController panels = PanelController.Instance;
        if (panels == null || panels.Panels == null)
            return;
        int[] indices =
        {
            SETTINGS.PANELS.TUTORIAL0,
            SETTINGS.PANELS.TUTORIAL1,
            SETTINGS.PANELS.TUTORIAL2,
            SETTINGS.PANELS.TUTORIAL3,
            SETTINGS.PANELS.TUTORIAL4,
            SETTINGS.PANELS.TUTORIAL5,
            SETTINGS.PANELS.TUTORIAL6,
        };
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];
            if (index < 0 || index >= panels.Panels.Count)
                continue;
            Panel panel = panels.Panels[index];
            if (panel == null || panel.Content == null)
                continue;
            TMP_Text[] labels = panel.Content.GetComponentsInChildren<TMP_Text>(true);
            for (int t = 0; t < labels.Length; t++)
            {
                if (labels[t] == null)
                    continue;
                string copy = string.Empty;
                if (t == 0 && i < StepHeaders.Length)
                    copy = StepHeaders[i];
                else if (t == 1 && i < StepBodies.Length)
                    copy = StepBodies[i];
                labels[t].text = copy;
                labels[t].color = t == 0 ? CourierPalette.Gold : CourierPalette.Cream;
                if (font != null)
                    labels[t].font = font;
                Wrap(labels[t], t == 0 ? 68f : 46f, t == 0 ? 50f : 36f);
            }
        }
    }

    /// <summary>The body of a template panel, addressed by index only (rule A).</summary>
    public static Transform PanelContent(int index)
    {
        PanelController panels = PanelController.Instance;
        if (panels == null || panels.Panels == null)
            return null;
        if (index < 0 || index >= panels.Panels.Count)
            return null;
        Panel panel = panels.Panels[index];
        if (panel == null || panel.Content == null)
            return null;
        return panel.Content.transform;
    }

    /// <summary>
    /// Switch off everything the template authored inside a panel body, leaving the
    /// pops alone - they are raised by the template and have to survive the wipe.
    /// </summary>
    public static void ClearTemplateChrome(Transform content)
    {
        if (content == null)
            return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child == null)
                continue;
            if (child.GetComponentInChildren<Pop>(true) != null)
                continue;
            child.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// The splash loading bar: a gold fill over a dark track with a real alpha, so
    /// the bar reads instead of shipping the template white on white default.
    /// </summary>
    public static void ThemeSplashSlider()
    {
        Transform splash = PanelContent(SETTINGS.PANELS.SPLASH);
        if (splash == null)
            return;
        Slider bar = splash.GetComponentInChildren<Slider>(true);
        if (bar == null)
            return;
        RectTransform fillRect = bar.fillRect;
        if (fillRect != null)
        {
            Image fill = fillRect.GetComponent<Image>();
            if (fill != null)
                fill.color = CourierPalette.Gold;
            if (fillRect.parent != null)
            {
                Image track = fillRect.parent.GetComponent<Image>();
                if (track != null)
                    track.color = CourierPalette.Fade(CourierPalette.Deep, 0.92f);
            }
        }

        Image backing = bar.GetComponent<Image>();
        if (backing != null)
            backing.color = CourierPalette.Fade(CourierPalette.Cyan, 0.35f);
        // The splash body holds the bar and nothing else, so anything this game adds
        // to it lands on top. Putting the bar back at the end of the child list keeps
        // it drawing over the shade rather than under it (rule C.13).
        bar.transform.SetAsLastSibling();
    }
}