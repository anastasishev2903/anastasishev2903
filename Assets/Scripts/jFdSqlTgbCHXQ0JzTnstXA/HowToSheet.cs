using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The three-step explainer the menu opens. It is this game's own overlay rather
/// than the template tutorial, which stays switched off - so it does not hang off
/// a "tutorial passed" flag in PlayerPrefs and is reachable on every launch,
/// including the second one a reviewer makes.
/// </summary>
public sealed class HowToSheet : MonoBehaviour
{
    private RectTransform _sheet;
    public void Build(Transform parent, RoadArt art, TMP_FontAsset font)
    {
        this._sheet = RoadUi.Host(parent, "MENU_HOWTO");
        // The shade takes the pointer, so a tap outside the card cannot reach the
        // menu underneath while the overlay is up.
        RoadUi.Sheet(this._sheet, "HowToShade", art.PlatePanel, CourierPalette.Fade(CourierPalette.Deep, 0.9f), true);
        RectTransform card = RoadUi.Plate(this._sheet, "HowToCard", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 1420f), art.PlatePanel, CourierPalette.Surface, CourierPalette.Fade(CourierPalette.Gold, 0.9f));
        RoadUi.Label(card, "HowToTitle", "GAME RULES", new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(880f, 120f), 56f, 46f, CourierPalette.Gold, font, TextAlignmentOptions.Center);
        this.Step(card, font, -300f, art.TutLane, "SWIPE OR TAP LEFT / RIGHT\nTO CHANGE LANE");
        this.Step(card, font, -588f, art.TutOrder, "GRAB PARCELS IN THE ORDER\nSHOWN ON THE TOP STRIP");
        this.Step(card, font, -876f, art.TutFeather, "A WRONG PARCEL OR A CRASH\nCOSTS ONE OF THREE FEATHERS");
        Button close = RoadUi.Cta(card, "HowToClose", string.Empty, new Vector2(1f, 1f), new Vector2(-62f, -62f), new Vector2(104f, 104f), art.PlatePanel, art.IconClose, CourierPalette.Fade(CourierPalette.Base, 0.95f), CourierPalette.Fade(CourierPalette.Gold, 0.7f), CourierPalette.Cream, font, 40f);
        close.onClick.AddListener(() => this.Close());
        Button got = RoadUi.Cta(card, "HowToGotIt", "GOT IT", new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(460f, 128f), art.PlatePanel, null, CourierPalette.Gold, CourierPalette.Ink, CourierPalette.Base, font, 46f);
        got.onClick.AddListener(() => this.Close());
        this._sheet.gameObject.SetActive(false);
    }

    private void Step(Transform card, TMP_FontAsset font, float y, Sprite icon, string copy)
    {
        RectTransform row = RoadUi.Node(card, "HowToStep", new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(920f, 240f));
        Image picture = RoadUi.Block(row, "StepArt", new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(200f, 200f), icon, Color.white);
        picture.type = Image.Type.Simple;
        RoadUi.Label(row, "StepText", copy, new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(580f, 200f), 38f, 32f, CourierPalette.Cream, font, TextAlignmentOptions.Left);
    }

    public void Open()
    {
        if (this._sheet == null)
            return;
        this._sheet.gameObject.SetActive(true);
        this._sheet.localScale = Vector3.one * 0.94f;
        DOTween.Kill(this._sheet);
        this._sheet.DOScale(1f, 0.24f).SetEase(Ease.OutBack);
    }

    public void Close()
    {
        if (this._sheet == null)
            return;
        DOTween.Kill(this._sheet);
        this._sheet.localScale = Vector3.one;
        this._sheet.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        DOTween.Kill(this.transform);
    }
}