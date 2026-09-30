using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dresses the three cards the template raises by index - WIN (7), LOSE (8) and
/// PAUSE (6). Everything the template put inside a card is switched off first, so
/// no leftover label and no sprite-less close icon (which Unity draws as a plain
/// white square) survives. What the player sees is built here, in this game's
/// words and this game's palette (rule C.3).
/// </summary>
public sealed class ResultCardDresser : MonoBehaviour
{
    private readonly List<Pop> _dressed = new List<Pop>();
    private readonly List<GameObject> _cards = new List<GameObject>();
    private RoadArt _art;
    private TMP_FontAsset _font;
    public void Initialize(RoadArt art, TMP_FontAsset font)
    {
        this._art = art;
        this._font = font;
    }

    /// <summary>
    /// Build a card inside a pop's body. The Pop is passed in from the literal
    /// GetPop(SETTINGS.POPS.X) at the call site, so the pop this dresser fills and
    /// the pop the caller raises are provably the same one.
    /// </summary>
    public void Dress(Pop pop, string header, Color headerColour, string main, string extra, string primaryCaption, System.Action primary, string secondaryCaption, System.Action secondary, string tertiaryCaption, System.Action tertiary)
    {
        if (pop == null || pop.Content == null)
            return;
        Transform content = pop.Content.transform;
        this.Strip(pop, content);
        RectTransform card = RoadUi.Plate(content, "RUN_CARD", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020f, 1220f), this._art.PlatePanel, CourierPalette.Surface, CourierPalette.Fade(CourierPalette.Gold, 0.9f));
        RoadUi.Label(card, "CARD_HEADER", header, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(930f, 160f), 76f, 56f, headerColour, this._font, TextAlignmentOptions.Center);
        RoadUi.Block(card, "CARD_RULE", new Vector2(0.5f, 1f), new Vector2(0f, -286f), new Vector2(700f, 8f), null, CourierPalette.Fade(CourierPalette.Cyan, 0.85f));
        RoadUi.Label(card, "CARD_MAIN", main, new Vector2(0.5f, 1f), new Vector2(0f, -396f), new Vector2(900f, 150f), 56f, 42f, CourierPalette.Cream, this._font, TextAlignmentOptions.Center);
        RoadUi.Label(card, "CARD_EXTRA", extra, new Vector2(0.5f, 1f), new Vector2(0f, -552f), new Vector2(900f, 170f), 40f, 32f, CourierPalette.CreamDim, this._font, TextAlignmentOptions.Center);
        Button first = RoadUi.Cta(card, "CARD_PRIMARY", primaryCaption, new Vector2(0.5f, 0f), new Vector2(0f, 384f), new Vector2(720f, 148f), this._art.PlatePanel, this._art.IconChevron, CourierPalette.Gold, CourierPalette.Ink, CourierPalette.Base, this._font, 48f);
        first.onClick.AddListener(() => primary.Invoke());
        Button second = RoadUi.Cta(card, "CARD_SECONDARY", secondaryCaption, new Vector2(0.5f, 0f), new Vector2(0f, 222f), new Vector2(720f, 136f), this._art.PlatePanel, null, CourierPalette.SurfaceLit, CourierPalette.Cyan, CourierPalette.Cyan, this._font, 44f);
        second.onClick.AddListener(() => secondary.Invoke());
        if (!string.IsNullOrEmpty(tertiaryCaption))
        {
            Button third = RoadUi.Cta(card, "CARD_TERTIARY", tertiaryCaption, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(720f, 128f), this._art.PlatePanel, null, CourierPalette.Fade(CourierPalette.Base, 0.95f), CourierPalette.Fade(CourierPalette.Cream, 0.5f), CourierPalette.Cream, this._font, 42f);
            third.onClick.AddListener(() => tertiary.Invoke());
        }

        // The corner close button carries this game's own cross, never the template's
        // unassigned sprite, which ships as a white square (rule B.1).
        Button close = RoadUi.Cta(card, "CARD_CLOSE", string.Empty, new Vector2(1f, 1f), new Vector2(-62f, -62f), new Vector2(108f, 108f), this._art.PlatePanel, this._art.IconClose, CourierPalette.Fade(CourierPalette.Base, 0.95f), CourierPalette.Fade(CourierPalette.Gold, 0.7f), CourierPalette.Cream, this._font, 40f);
        close.onClick.AddListener(() => secondary.Invoke());
        card.localScale = Vector3.one * 0.9f;
        DOTween.Kill(card);
        card.DOScale(1f, 0.28f).SetEase(Ease.OutBack);
        this._dressed.Add(pop);
        this._cards.Add(card.parent == null ? card.gameObject : card.parent.gameObject);
    }

    /// <summary>
    /// Switch off everything the template authored inside this card and remove the
    /// card this dresser built last time, so re-dressing replaces instead of stacking.
    /// </summary>
    private void Strip(Pop pop, Transform content)
    {
        for (int i = this._dressed.Count - 1; i >= 0; i--)
        {
            if (this._dressed[i] != pop)
                continue;
            if (this._cards[i] != null)
                Destroy(this._cards[i]);
            this._dressed.RemoveAt(i);
            this._cards.RemoveAt(i);
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child != null)
                child.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        DOTween.Kill(this.transform);
    }
}