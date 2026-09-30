using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The menu side of the road: an abstract mark on the splash, the magnet cart
/// waiting at the mouth of three lanes, the best run so far, the road switch and
/// the two buttons.
///
/// PLAY is the scene template's own load button - this view only draws its face,
/// so the scene change stays exactly where the template put it and can never fire
/// twice.
/// </summary>
public sealed class MenuRoadView : MonoBehaviour
{
    [SerializeField]
    private Sprite _cart;
    [SerializeField]
    private Sprite[] _parcelSprites = new Sprite[0];
    [SerializeField]
    private Sprite _laneFan;
    [SerializeField]
    private Sprite _emblem;
    [SerializeField]
    private Sprite _slotFrame;
    [SerializeField]
    private Sprite _platePanel;
    [SerializeField]
    private Sprite _featherLive;
    [SerializeField]
    private Sprite _iconClose;
    [SerializeField]
    private Sprite _iconChevron;
    [SerializeField]
    private Sprite _tutLane;
    [SerializeField]
    private Sprite _tutOrder;
    [SerializeField]
    private Sprite _tutFeather;
    [SerializeField]
    private TMP_FontAsset _font;
    private RoadArt _art;
    private Camera _camera;
    private RoadMetrics _metrics;
    private SpriteRenderer _cartBody;
    private SpriteRenderer _load;
    private TextMeshProUGUI _objective;
    private RouteChooser _chooser;
    private HowToSheet _howTo;
    private void Awake()
    {
        this._camera = Camera.main;
        this._art = new RoadArt
        {
            Cart = this._cart,
            Parcels = this._parcelSprites,
            LaneFan = this._laneFan,
            Emblem = this._emblem,
            SlotFrame = this._slotFrame,
            PlatePanel = this._platePanel,
            FeatherLive = this._featherLive,
            IconClose = this._iconClose,
            IconChevron = this._iconChevron,
            TutLane = this._tutLane,
            TutOrder = this._tutOrder,
            TutFeather = this._tutFeather,
        };
    }

    private void Start()
    {
        this._metrics = RoadMetrics.FromCamera(this._camera);
        RoadUi.SanitiseTutorials(this._font);
        this.BuildRoadside();
        this.DressSplash();
        this.BuildMenu();
        // Last, so the loading bar ends up above the shade this game just laid over
        // the splash body.
        RoadUi.ThemeSplashSlider();
    }

    // -- the world behind the menu ---------------------------------------------
    private void BuildRoadside()
    {
        float fanSide = this._metrics.HalfWidth * 2f * 0.7f;
        RoadStage.Loose(this.transform, "MenuLaneFan", this._art.LaneFan, new Vector2(fanSide, fanSide), new Vector2(0f, 2.3f), RoadTuning.MenuFanOrder, CourierPalette.Fade(Color.white, 0.9f));
        float cartSide = this._metrics.LanePitch * 1.15f;
        this._cartBody = RoadStage.Loose(this.transform, "MenuCart", this._art.Cart, new Vector2(cartSide, cartSide), new Vector2(0f, 1.5f), RoadTuning.MenuCartOrder, Color.white);
        float parcelSide = cartSide * 0.34f;
        this._load = RoadStage.Loose(this.transform, "MenuParcel", this._art.ParcelFace(0), new Vector2(parcelSide, parcelSide), new Vector2(0f, 1.5f + cartSide * 0.22f), RoadTuning.MenuParcelOrder, Color.white);
        // The cart breathes for a few cycles and then settles - never an endless loop,
        // which would keep the window busy and stall the capture run.
        Transform host = this._cartBody.transform;
        DOTween.Kill(host);
        host.DOLocalMoveY(1.5f + cartSide * 0.07f, 1.4f).SetEase(Ease.InOutSine).SetLoops(6, LoopType.Yoyo);
    }

    private void DressSplash()
    {
        Transform splash = RoadUi.PanelContent(SETTINGS.PANELS.SPLASH);
        if (splash == null)
            return;
        // The splash is deliberately darker than the menu, and carries an abstract
        // mark only - a magnet arc around a diamond parcel. No lettering on it at all.
        RoadUi.Sheet(splash, "SPLASH_SHADE", this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Deep, 0.74f), false);
        Image mark = RoadUi.Block(splash, "SPLASH_MARK", new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(420f, 420f), this._art.Emblem, Color.white);
        mark.type = Image.Type.Simple;
        // Just above the template's loading bar, which sits at 0.05-0.10 of the panel.
        RoadUi.Label(splash, "SPLASH_TAG", "LOADING", new Vector2(0.5f, 0.17f), Vector2.zero, new Vector2(600f, 64f), 36f, 32f, CourierPalette.Cream, this._font, TextAlignmentOptions.Center);
        Transform host = mark.transform;
        host.localScale = Vector3.one * 0.84f;
        DOTween.Kill(host);
        host.DOScale(1f, 0.45f).SetEase(Ease.OutBack).OnComplete(() => this.NudgeMark(host));
    }

    private void NudgeMark(Transform host)
    {
        if (host == null)
            return;
        host.DOPunchScale(Vector3.one * 0.06f, 0.6f, 6, 0.6f);
    }

    // -- the menu proper -------------------------------------------------------
    private void BuildMenu()
    {
        Transform content = RoadUi.PanelContent(SETTINGS.PANELS.DEFAULT);
        if (content == null)
            return;
        this.BuildBestCard(content);
        this._objective = RoadUi.Label(content, "MENU_OBJECTIVE", string.Empty, new Vector2(0.5f, 0.545f), Vector2.zero, new Vector2(1000f, 90f), 44f, 36f, CourierPalette.Cream, this._font, TextAlignmentOptions.Center);
        RoadUi.Label(content, "MENU_SUBTITLE", "3 LANES - 3 FEATHERS - ONE CLOCK", new Vector2(0.5f, 0.495f), Vector2.zero, new Vector2(1000f, 60f), 32f, 30f, CourierPalette.Fade(CourierPalette.Cyan, 0.9f), this._font, TextAlignmentOptions.Center);
        GameObject chooserHost = new GameObject("MenuRoadChooser");
        chooserHost.transform.SetParent(this.transform, false);
        this._chooser = chooserHost.AddComponent<RouteChooser>();
        this._chooser.Build(content, this._art, this._font, new Vector2(0.5f, 0.405f), road => this.OnRoadPicked(road));
        this.DressPlayButton(content);
        // The capture harness reaches the game by tapping the primary CTA's wording as
        // a SUBSTRING, so this secondary caption must not repeat any of it - otherwise
        // it swallows the tap and the run never leaves the menu.
        Button howTo = RoadUi.Cta(content, "MENU_HOWTO_BTN", "GAME RULES", new Vector2(0.5f, 0.175f), Vector2.zero, new Vector2(580f, 116f), this._art.PlatePanel, null, CourierPalette.Surface, CourierPalette.Cyan, CourierPalette.Cyan, this._font, 42f);
        GameObject sheetHost = new GameObject("MenuHowTo");
        sheetHost.transform.SetParent(this.transform, false);
        this._howTo = sheetHost.AddComponent<HowToSheet>();
        this._howTo.Build(content, this._art, this._font);
        howTo.onClick.AddListener(() => this.OpenHowTo());
    }

    private void BuildBestCard(Transform content)
    {
        RectTransform card = RoadUi.Plate(content, "MENU_BEST", new Vector2(0.5f, 0.862f), Vector2.zero, new Vector2(920f, 220f), this._art.PlatePanel, CourierPalette.Fade(CourierPalette.Base, 0.92f), CourierPalette.Fade(CourierPalette.Gold, 0.85f));
        RoadUi.Label(card, "MENU_BEST_TAG_L", "BEST DELIVERED", new Vector2(0.27f, 1f), new Vector2(0f, -42f), new Vector2(380f, 46f), 32f, 30f, CourierPalette.CreamDim, this._font, TextAlignmentOptions.Center);
        RoadUi.Label(card, "MENU_BEST_VALUE_L", RunMemory.BestDeliveredAnywhere().ToString(), new Vector2(0.27f, 0f), new Vector2(0f, 62f), new Vector2(380f, 96f), 72f, 52f, CourierPalette.Gold, this._font, TextAlignmentOptions.Center);
        RoadUi.Block(card, "MENU_BEST_RULE", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 130f), null, CourierPalette.Fade(CourierPalette.Cyan, 0.4f));
        RoadUi.Label(card, "MENU_BEST_TAG_R", "BEST ACCURACY", new Vector2(0.73f, 1f), new Vector2(0f, -42f), new Vector2(380f, 46f), 32f, 30f, CourierPalette.CreamDim, this._font, TextAlignmentOptions.Center);
        RoadUi.Label(card, "MENU_BEST_VALUE_R", RunMemory.BestAccuracyAnywhere() + "%", new Vector2(0.73f, 0f), new Vector2(0f, 62f), new Vector2(380f, 96f), 72f, 52f, CourierPalette.Lime, this._font, TextAlignmentOptions.Center);
    }

    /// <summary>
    /// Draw this game's face over the template's load button. The face carries the
    /// raycast and the template button keeps the handler, so UGUI bubbles the click
    /// up to it and the scene loads exactly once.
    /// </summary>
    private void DressPlayButton(Transform content)
    {
        LoadSceneButton target = content.GetComponentInChildren<LoadSceneButton>(true);
        if (target == null)
            return;
        RectTransform host = target.GetComponent<RectTransform>();
        if (host == null)
            return;
        Vector2 size = host.rect.size;
        if (size.x < 200f || size.y < 80f)
            size = new Vector2(760f, 178f);
        Image rim = RoadUi.Block(host, "PLAY_RIM", new Vector2(0.5f, 0.5f), Vector2.zero, size, this._art.PlatePanel, CourierPalette.Ink);
        rim.raycastTarget = true;
        Image fill = RoadUi.Block(host, "PLAY_FILL", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(RoadUi.RimPixels * 2f, RoadUi.RimPixels * 2f), this._art.PlatePanel, CourierPalette.Gold);
        fill.raycastTarget = true;
        Image glyph = RoadUi.Block(host, "PLAY_ICON", new Vector2(0.5f, 0.5f), new Vector2(-(size.x * 0.5f - 66f), 0f), new Vector2(48f, 48f), this._art.IconChevron, CourierPalette.Base);
        glyph.type = Image.Type.Simple;
        RoadUi.Label(host, "PLAY_TEXT", "PLAY", new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(size.x - 170f, size.y - 40f), 64f, 52f, CourierPalette.Base, this._font, TextAlignmentOptions.Center);
        host.localScale = Vector3.one;
    }

    // -- reactions -------------------------------------------------------------
    private void OnRoadPicked(int road)
    {
        RouteSetup setup = RoadTuning.Route(road);
        if (this._objective != null)
            this._objective.text = "DELIVER " + setup.Parcels + " PARCELS IN ORDER";
        // The chosen road shows on the cart itself, not only on the chip: the parcel
        // it carries takes that road's colour (rule C.7).
        if (this._load != null)
        {
            int kind = road % RoadTuning.ParcelKinds;
            this._load.sprite = this._art.ParcelFace(kind);
            Transform host = this._load.transform;
            DOTween.Kill(host);
            host.localScale = Vector3.one;
            host.DOPunchScale(Vector3.one * 0.18f, 0.35f, 6, 0.6f);
        }
    }

    private void OpenHowTo()
    {
        RunMemory.Buzz();
        if (this._howTo != null)
            this._howTo.Open();
    }

    private void OnDestroy()
    {
        if (this._cartBody != null)
            DOTween.Kill(this._cartBody.transform);
        if (this._load != null)
            DOTween.Kill(this._load.transform);
        DOTween.Kill(this.transform);
    }
}