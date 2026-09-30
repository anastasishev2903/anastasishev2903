using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The roads card in the menu. Choosing a road is not an extra step on the way
/// into the game - PLAY always leaves straight for whatever is selected - it is a
/// switch that lives on the same screen.
///
/// Rule C.7 asks that pressing it be visible in a screenshot, so a pick changes
/// four things at once: the chip's rim colour, its fill, a gold marker in its
/// corner, and the objective line above the card.
/// </summary>
public sealed class RouteChooser : MonoBehaviour
{
    private readonly RectTransform[] _chipRoot = new RectTransform[3];
    private readonly Image[] _chipRim = new Image[3];
    private readonly Image[] _chipFill = new Image[3];
    private readonly Image[] _chipMark = new Image[3];
    private readonly TextMeshProUGUI[] _chipBest = new TextMeshProUGUI[3];
    private RoadArt _art;
    private TMP_FontAsset _font;
    private System.Action<int> _onPicked;
    private int _picked;
    public void Build(Transform parent, RoadArt art, TMP_FontAsset font, Vector2 anchor, System.Action<int> onPicked)
    {
        this._art = art;
        this._font = font;
        this._onPicked = onPicked;
        RectTransform card = RoadUi.Plate(parent, "MENU_ROADS", anchor, Vector2.zero, new Vector2(1080f, 300f), art.PlatePanel, CourierPalette.Fade(CourierPalette.Base, 0.92f), CourierPalette.Fade(CourierPalette.Gold, 0.85f));
        RoadUi.Label(card, "MENU_ROADS_TAG", "ROUTES", new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(600f, 48f), 36f, 32f, CourierPalette.Gold, this._font, TextAlignmentOptions.Center);
        int count = RoadTuning.Routes.Length;
        if (count == 0)
        {
            // A card with nothing in it gets a line of its own rather than a hole in
            // the layout (rule G - an empty list always explains itself).
            RoadUi.Label(card, "MENU_ROADS_EMPTY", "NO ROUTES OPEN YET", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 80f), 38f, 32f, CourierPalette.CreamDim, this._font, TextAlignmentOptions.Center);
            return;
        }

        float step = 352f;
        float left = -(count - 1) * step * 0.5f;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            RouteSetup route = RoadTuning.Routes[i];
            Vector2 size = new Vector2(330f, 180f);
            RectTransform chip = RoadUi.Node(card, "MENU_CHIP", new Vector2(0.5f, 0.5f), new Vector2(left + i * step, -28f), size);
            this._chipRoot[i] = chip;
            Image hit = chip.gameObject.AddComponent<Image>();
            hit.sprite = art.PlatePanel;
            hit.type = art.PlatePanel == null ? Image.Type.Simple : Image.Type.Sliced;
            hit.color = new Color(1f, 1f, 1f, 0.004f);
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false;
            this._chipRim[i] = RoadUi.Block(chip, "ChipRim", new Vector2(0.5f, 0.5f), Vector2.zero, size, art.PlatePanel, CourierPalette.Surface);
            this._chipFill[i] = RoadUi.Block(chip, "ChipFill", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(8f, 8f), art.PlatePanel, CourierPalette.Surface);
            RoadUi.Label(chip, "ChipName", route.Caption, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(290f, 48f), 34f, 30f, CourierPalette.Cream, this._font, TextAlignmentOptions.Center);
            RoadUi.Label(chip, "ChipLoad", route.Parcels + " PARCELS", new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(290f, 44f), 32f, 30f, CourierPalette.Gold, this._font, TextAlignmentOptions.Center);
            this._chipBest[i] = RoadUi.Label(chip, "ChipBest", string.Empty, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(290f, 44f), 30f, 30f, CourierPalette.CreamDim, this._font, TextAlignmentOptions.Center);
            this._chipMark[i] = RoadUi.Block(chip, "ChipMark", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(28f, 28f), art.FeatherLive, CourierPalette.Gold);
            this._chipMark[i].type = Image.Type.Simple;
            Button button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = this._chipFill[i];
            ColorBlock colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = Color.white;
            colours.pressedColor = new Color(0.7f, 0.74f, 0.82f, 1f);
            colours.selectedColor = Color.white;
            colours.disabledColor = new Color(0.42f, 0.45f, 0.52f, 0.7f);
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.06f;
            button.colors = colours;
            button.onClick.AddListener(() => this.Pick(index));
        }

        this.Pick(RunMemory.Road);
    }

    private void Pick(int index)
    {
        this._picked = Mathf.Clamp(index, 0, RoadTuning.Routes.Length - 1);
        RunMemory.Road = this._picked;
        RunMemory.Buzz();
        for (int i = 0; i < RoadTuning.Routes.Length; i++)
        {
            bool live = i == this._picked;
            if (this._chipRim[i] != null)
                this._chipRim[i].color = live ? CourierPalette.Gold : CourierPalette.Fade(CourierPalette.Surface, 0.9f);
            if (this._chipFill[i] != null)
                this._chipFill[i].color = live ? CourierPalette.SurfaceLit : CourierPalette.Fade(CourierPalette.Surface, 0.55f);
            if (this._chipMark[i] != null)
                this._chipMark[i].color = live ? CourierPalette.Gold : CourierPalette.Fade(CourierPalette.Surface, 0.45f);
            if (this._chipBest[i] != null)
            {
                int best = RunMemory.BestDelivered(i);
                this._chipBest[i].text = best > 0 ? "BEST " + best + " / " + RoadTuning.Routes[i].Parcels : "NOT RUN YET";
                this._chipBest[i].color = RunMemory.Finished(i) ? CourierPalette.Lime : CourierPalette.CreamDim;
            }

            if (this._chipRoot[i] == null)
                continue;
            this._chipRoot[i].localScale = Vector3.one;
            if (live)
            {
                DOTween.Kill(this._chipRoot[i]);
                this._chipRoot[i].DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.6f);
            }
        }

        if (this._onPicked != null)
            this._onPicked.Invoke(this._picked);
    }

    private void OnDestroy()
    {
        DOTween.Kill(this.transform);
    }
}