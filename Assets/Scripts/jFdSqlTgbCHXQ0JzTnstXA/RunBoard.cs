using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The run read-out, built as this game's OWN objects inside the template panel
/// body - never as content pushed into a template placeholder, because those are
/// switched off before a build ships (rule C.2).
///
/// Top to bottom: the two chrome buttons, the order strip that says which parcel
/// the round wants next, the route clock, the delivery counter and the three
/// feathers, and at the foot the gesture hint, which never leaves the screen.
/// </summary>
public sealed class RunBoard : MonoBehaviour
{
    private const float SlotSide = 148f;
    private const float HeadSide = 168f;
    private const float SlotStep = 166f;
    private const float ClockWidth = 620f;
    private RoadArt _art;
    private TMP_FontAsset _font;
    private RectTransform _stripRow;
    private readonly Image[] _slotRim = new Image[RoadTuning.OrderWindow];
    private readonly Image[] _slotFace = new Image[RoadTuning.OrderWindow];
    private Image _clockFill;
    private TextMeshProUGUI _clockLabel;
    private TextMeshProUGUI _bonusLabel;
    private TextMeshProUGUI _countValue;
    private RectTransform _countCard;
    private readonly Image[] _pips = new Image[RoadTuning.FeatherBudget];
    private RectTransform _featherCard;
    private RectTransform _hintWide;
    private RectTransform _hintChip;
    public void Build(Transform host, RoadArt art, TMP_FontAsset font, System.Action onBack, System.Action onPause)
    {
        this._art = art;
        this._font = font;
        this.BuildChrome(host, onBack, onPause);
        this.BuildStrip(host);
        this.BuildClock(host);
        this.BuildProgress(host);
        this.BuildFeathers(host);
        this.BuildHint(host);
    }

    // -- chrome ----------------------------------------------------------------
    // The template's own TopPanel buttons are switched off by the pipeline before a
    // build ships, so this game carries its own pair and adds nothing beside theirs
    // (rule H, option 2).
    private void BuildChrome(Transform host, System.Action onBack, System.Action onPause)
    {
        RoadUi.Block(host, "HUD_TOP_BAR", new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1242f, 192f), this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Deep, 0.55f));
        Button back = RoadUi.Cta(host, "HUD_BACK", string.Empty, new Vector2(0.085f, 0.94f), Vector2.zero, new Vector2(128f, 128f), this._art.PlatePanel, this._art.IconBack, CourierPalette.Surface, CourierPalette.Gold, CourierPalette.Gold, this._font, 40f);
        back.onClick.AddListener(() => onBack.Invoke());
        Button pause = RoadUi.Cta(host, "HUD_PAUSE", string.Empty, new Vector2(0.915f, 0.94f), Vector2.zero, new Vector2(128f, 128f), this._art.PlatePanel, this._art.IconPause, CourierPalette.Surface, CourierPalette.Gold, CourierPalette.Gold, this._font, 40f);
        pause.onClick.AddListener(() => onPause.Invoke());
    }

    // -- order strip -----------------------------------------------------------
    private void BuildStrip(Transform host)
    {
        RectTransform card = RoadUi.Plate(host, "HUD_ORDER", new Vector2(0.5f, 0.885f), Vector2.zero, new Vector2(940f, 210f), this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Base, 0.92f), CourierPalette.Fade(CourierPalette.Gold, 0.85f));
        RoadUi.Label(card, "HUD_ORDER_TAG", "NEXT", new Vector2(0f, 1f), new Vector2(120f, -34f), new Vector2(200f, 44f), 32f, 30f, CourierPalette.Cyan, this._font, TextAlignmentOptions.Center);
        this._stripRow = RoadUi.Node(card, "HUD_ORDER_ROW", new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(880f, 180f));
        float left = -(RoadTuning.OrderWindow - 1) * SlotStep * 0.5f;
        for (int i = 0; i < RoadTuning.OrderWindow; i++)
        {
            float side = i == 0 ? HeadSide : SlotSide;
            RectTransform slot = RoadUi.Node(this._stripRow, "HUD_SLOT", new Vector2(0.5f, 0.5f), new Vector2(left + i * SlotStep, 0f), new Vector2(side, side));
            this._slotRim[i] = RoadUi.Block(slot, "SlotRim", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(side, side), this._art.SlotFrame, CourierPalette.Gold);
            this._slotFace[i] = RoadUi.Block(slot, "SlotFace", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(side * 0.7f, side * 0.7f), this._art.ParcelFace(0), Color.white);
            this._slotFace[i].type = Image.Type.Simple;
            float shrink = i == 0 ? 1f : 1f - (i - 1) * 0.06f;
            slot.localScale = Vector3.one * shrink;
        }
    }

    /// <summary>Repaint the five slots from the colour chain starting at the head.</summary>
    public void SetOrder(DeliveryPlan plan, int head)
    {
        for (int i = 0; i < RoadTuning.OrderWindow; i++)
        {
            int index = head + i;
            bool live = plan != null && index < plan.Length;
            int kind = live ? plan.ColourAt(index) : 0;
            if (this._slotFace[i] != null)
            {
                this._slotFace[i].sprite = this._art.ParcelFace(kind);
                this._slotFace[i].color = live ? CourierPalette.Fade(Color.white, i == 0 ? 1f : 0.62f) : CourierPalette.Fade(Color.white, 0.12f);
            }

            if (this._slotRim[i] != null)
                this._slotRim[i].color = live ? (i == 0 ? CourierPalette.Cream : CourierPalette.Fade(CourierPalette.Parcel(kind), 0.62f)) : CourierPalette.Fade(CourierPalette.Surface, 0.7f);
        }
    }

    /// <summary>The strip slides one step left and snaps back with the new order.</summary>
    public void AdvanceStrip(DeliveryPlan plan, int head)
    {
        if (this._stripRow == null)
        {
            this.SetOrder(plan, head);
            return;
        }

        DOTween.Kill(this._stripRow);
        this._stripRow.anchoredPosition = new Vector2(SlotStep, -14f);
        this._stripRow.DOAnchorPos(new Vector2(0f, -14f), 0.22f).SetEase(Ease.OutCubic);
        this.SetOrder(plan, head);
        if (this._slotRim[0] != null)
        {
            Transform headSlot = this._slotRim[0].transform.parent;
            DOTween.Kill(headSlot);
            headSlot.localScale = Vector3.one;
            headSlot.DOPunchScale(Vector3.one * 0.12f, 0.2f, 6, 0.6f);
        }
    }

    // -- route clock -----------------------------------------------------------
    private void BuildClock(Transform host)
    {
        this._clockLabel = RoadUi.Label(host, "HUD_CLOCK_LABEL", "01:40", new Vector2(0.165f, 0.808f), Vector2.zero, new Vector2(260f, 76f), 52f, 40f, CourierPalette.Cream, this._font, TextAlignmentOptions.Center);
        this._clockFill = RoadUi.Bar(host, "HUD_CLOCK_BAR", new Vector2(0.58f, 0.808f), Vector2.zero, new Vector2(ClockWidth, 40f), this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Deep, 0.92f), CourierPalette.Fade(CourierPalette.Cyan, 0.45f), CourierPalette.Gold);
        this._bonusLabel = RoadUi.Label(host, "HUD_CLOCK_BONUS", string.Empty, new Vector2(0.58f, 0.808f), new Vector2(0f, 56f), new Vector2(320f, 60f), 44f, 34f, CourierPalette.Lime, this._font, TextAlignmentOptions.Center);
        this._bonusLabel.alpha = 0f;
    }

    public void SetClock(float remaining, float total)
    {
        if (this._clockLabel != null)
            this._clockLabel.text = RoadTuning.Clock(remaining);
        if (this._clockFill == null)
            return;
        RoadUi.SetBar(this._clockFill, total <= 0f ? 0f : remaining / total, ClockWidth - 6f);
        this._clockFill.color = remaining <= RoadTuning.ClockWarnSeconds ? CourierPalette.Alarm : CourierPalette.Gold;
    }

    /// <summary>The clock just gained time: say so where the player is looking.</summary>
    public void ShowClockBonus(float seconds)
    {
        if (this._clockFill != null)
        {
            Transform host = this._clockFill.transform.parent;
            DOTween.Kill(host);
            host.localScale = Vector3.one;
            host.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.6f);
        }

        if (this._bonusLabel == null)
            return;
        this._bonusLabel.text = "+" + seconds.ToString("0") + "S";
        RectTransform rect = this._bonusLabel.rectTransform;
        DOTween.Kill(rect);
        DOTween.Kill(this._bonusLabel);
        rect.anchoredPosition = new Vector2(0f, 40f);
        this._bonusLabel.alpha = 1f;
        rect.DOAnchorPos(new Vector2(0f, 130f), 0.6f).SetEase(Ease.OutQuad);
        DOTween.To(() => this._bonusLabel.alpha, value => this._bonusLabel.alpha = value, 0f, 0.6f);
    }

    // -- delivered -------------------------------------------------------------
    private void BuildProgress(Transform host)
    {
        this._countCard = RoadUi.Plate(host, "HUD_COUNT", new Vector2(0.255f, 0.735f), Vector2.zero, new Vector2(420f, 170f), this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Base, 0.92f), CourierPalette.Fade(CourierPalette.Lime, 0.8f));
        this._countValue = RoadUi.Label(this._countCard, "HUD_COUNT_VALUE", "00 / 24", new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(380f, 80f), 60f, 44f, CourierPalette.Lime, this._font, TextAlignmentOptions.Center);
        RoadUi.Label(this._countCard, "HUD_COUNT_TAG", "PARCELS", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(380f, 48f), 32f, 30f, CourierPalette.CreamDim, this._font, TextAlignmentOptions.Center);
    }

    public void SetProgress(int delivered, int total)
    {
        if (this._countValue != null)
            this._countValue.text = RoadTuning.Pad(delivered) + " / " + RoadTuning.Pad(total);
        if (this._countCard == null)
            return;
        DOTween.Kill(this._countCard);
        this._countCard.localScale = Vector3.one;
        this._countCard.DOPunchScale(Vector3.one * 0.06f, 0.25f, 6, 0.6f);
    }

    // -- feathers --------------------------------------------------------------
    private void BuildFeathers(Transform host)
    {
        this._featherCard = RoadUi.Plate(host, "HUD_FEATHERS", new Vector2(0.745f, 0.735f), Vector2.zero, new Vector2(420f, 170f), this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Base, 0.92f), CourierPalette.Fade(CourierPalette.Gold, 0.8f));
        float left = -(RoadTuning.FeatherBudget - 1) * 74f * 0.5f;
        for (int i = 0; i < RoadTuning.FeatherBudget; i++)
        {
            this._pips[i] = RoadUi.Block(this._featherCard, "HUD_PIP", new Vector2(0.5f, 1f), new Vector2(left + i * 74f, -56f), new Vector2(60f, 60f), this._art.FeatherLive, Color.white);
            this._pips[i].type = Image.Type.Simple;
        }

        RoadUi.Label(this._featherCard, "HUD_FEATHERS_TAG", "FEATHERS", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(380f, 48f), 32f, 30f, CourierPalette.CreamDim, this._font, TextAlignmentOptions.Center);
    }

    public void SetFeathers(int left)
    {
        for (int i = 0; i < this._pips.Length; i++)
        {
            if (this._pips[i] == null)
                continue;
            bool alive = i < left;
            this._pips[i].sprite = alive ? this._art.FeatherLive : this._art.FeatherLost;
            this._pips[i].color = alive ? Color.white : CourierPalette.Fade(CourierPalette.Surface, 0.85f);
        }
    }

    /// <summary>A feather was just spent: shake the card and punch the pip that died.</summary>
    public void FlashFeatherLoss(int left)
    {
        this.SetFeathers(left);
        if (this._featherCard == null)
            return;
        int index = Mathf.Clamp(left, 0, this._pips.Length - 1);
        if (this._pips[index] != null)
        {
            Transform pip = this._pips[index].transform;
            DOTween.Kill(pip);
            pip.localScale = Vector3.one;
            pip.DOPunchScale(Vector3.one * 0.25f, 0.3f, 8, 0.6f);
        }

        DOTween.Kill(this._featherCard);
        this._featherCard.DOShakePosition(0.25f, 8f, 12, 90f, false, true);
    }

    // -- gesture hint (rule C.6) -----------------------------------------------
    private void BuildHint(Transform host)
    {
        this._hintWide = RoadUi.Plate(host, "HUD_HINT_WIDE", new Vector2(0.5f, 0.105f), Vector2.zero, new Vector2(1020f, 110f), this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Base, 0.94f), CourierPalette.Fade(CourierPalette.Cyan, 0.85f));
        RoadUi.Label(this._hintWide, "HUD_HINT_WIDE_TEXT", "SWIPE OR TAP LEFT / RIGHT TO CHANGE LANE", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 70f), 38f, 32f, CourierPalette.Cream, this._font, TextAlignmentOptions.Center);
        this._hintChip = RoadUi.Plate(host, "HUD_HINT_CHIP", new Vector2(0.5f, 0.105f), Vector2.zero, new Vector2(680f, 86f), this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Base, 0.88f), CourierPalette.Fade(CourierPalette.Cyan, 0.6f));
        RoadUi.Label(this._hintChip, "HUD_HINT_CHIP_TEXT", "TAP LEFT / RIGHT TO CHANGE LANE", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 60f), 34f, 30f, CourierPalette.Fade(CourierPalette.Cream, 0.88f), this._font, TextAlignmentOptions.Center);
        this.SetHintWide(true);
    }

    /// <summary>
    /// The hint never leaves: it only shrinks from a full plate to a chip once the
    /// player has had eleven seconds with it, so a review screenshot taken at any
    /// point in the run still shows how the cart is steered.
    /// </summary>
    public void SetHintWide(bool wide)
    {
        if (this._hintWide != null)
            this._hintWide.parent.gameObject.SetActive(wide);
        if (this._hintChip != null)
            this._hintChip.parent.gameObject.SetActive(!wide);
    }

    private void OnDestroy()
    {
        DOTween.Kill(this.transform);
    }
}