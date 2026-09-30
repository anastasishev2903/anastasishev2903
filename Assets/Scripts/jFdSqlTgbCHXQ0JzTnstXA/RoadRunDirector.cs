using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// One run down the delivery road. The road scrolls past, the strip on top says
/// which colour the order wants next, and the player moves the magnet cart between
/// three lanes to meet it. A wrong parcel or a crash costs one of three feathers;
/// so does the route clock running out.
///
/// The whole road is built here from serialized art and camera-derived sizes, so
/// the scene template never carries this game's content (rule C.2), and each of the
/// three result cards is fetched by index and dressed before it is raised (C.3).
/// </summary>
public sealed class RoadRunDirector : MonoBehaviour
{
    private enum RunPhase
    {
        Idle,
        Rolling,
        Running,
        Held,
        Over,
    }

    [SerializeField]
    private Sprite _cart;
    [SerializeField]
    private Sprite[] _parcelSprites = new Sprite[0];
    [SerializeField]
    private Sprite _hayBale;
    [SerializeField]
    private Sprite _crateStack;
    [SerializeField]
    private Sprite _roadTile;
    [SerializeField]
    private Sprite _vergeSign;
    [SerializeField]
    private Sprite _slotFrame;
    [SerializeField]
    private Sprite _platePanel;
    [SerializeField]
    private Sprite _magnetRing;
    [SerializeField]
    private Sprite _featherLive;
    [SerializeField]
    private Sprite _featherLost;
    [SerializeField]
    private Sprite _iconBack;
    [SerializeField]
    private Sprite _iconPause;
    [SerializeField]
    private Sprite _iconClose;
    [SerializeField]
    private Sprite _iconChevron;
    [SerializeField]
    private TMP_FontAsset _font;
    [SerializeField]
    private GameObject _parcelPrefab;
    [SerializeField]
    private GameObject _obstaclePrefab;
    [SerializeField]
    private GameObject _roadTilePrefab;
    [SerializeField]
    private GameObject _vergeSignPrefab;
    [SerializeField]
    private GameObject _cartPrefab;
    [SerializeField]
    private GameObject _magnetRingPrefab;
    private readonly DeliveryPlanner _planner = new DeliveryPlanner();
    private RoadArt _art;
    private RoadKit _kit;
    private RoadMetrics _metrics;
    private RouteSetup _setup;
    private DeliveryPlan _plan;
    private System.Random _rng;
    private Camera _camera;
    private CourierCart _pilot;
    private ParcelStream _stream;
    private RunBoard _board;
    private ResultCardDresser _dresser;
    private RunPhase _phase = RunPhase.Idle;
    private int _roadIndex;
    private int _head;
    private int _delivered;
    private int _wrongPicks;
    private int _crashes;
    private int _feathers;
    private int _chain;
    private int _bestChain;
    private float _clockLeft;
    private float _clockTotal;
    private float _introLeft;
    private float _hintLeft;
    private bool _outOfTime;
    private int _swipeTouch = -1;
    private Vector2 _swipeFrom;
    private float _swipeStarted;
    // The pop buttons sit inside the same screen band the steering taps read, and the
    // order in which UGUI and this component see one frame is undefined. A short lock
    // after a card closes keeps the tap that pressed RESUME from also changing lane.
    private float _steerLockedUntil;
    private void Awake()
    {
        EnhancedTouchSupport.Enable();
        this._camera = Camera.main;
        this._art = new RoadArt
        {
            Cart = this._cart,
            Parcels = this._parcelSprites,
            HayBale = this._hayBale,
            CrateStack = this._crateStack,
            RoadTile = this._roadTile,
            VergeSign = this._vergeSign,
            SlotFrame = this._slotFrame,
            PlatePanel = this._platePanel,
            MagnetRing = this._magnetRing,
            FeatherLive = this._featherLive,
            FeatherLost = this._featherLost,
            IconBack = this._iconBack,
            IconPause = this._iconPause,
            IconClose = this._iconClose,
            IconChevron = this._iconChevron,
        };
        this._kit = new RoadKit
        {
            Parcel = this._parcelPrefab,
            Obstacle = this._obstaclePrefab,
            RoadTile = this._roadTilePrefab,
            VergeSign = this._vergeSignPrefab,
            Cart = this._cartPrefab,
            MagnetRing = this._magnetRingPrefab,
        };
    }

    private void Start()
    {
        this._metrics = RoadMetrics.FromCamera(this._camera);
        this._roadIndex = RunMemory.Road;
        this._setup = RoadTuning.Route(this._roadIndex);
        GameObject cartHost = new GameObject("RoadCart");
        cartHost.transform.SetParent(this.transform, false);
        this._pilot = cartHost.AddComponent<CourierCart>();
        this._pilot.Initialize(this._art, this._kit, this._metrics);
        GameObject streamHost = new GameObject("RoadStream");
        streamHost.transform.SetParent(this.transform, false);
        this._stream = streamHost.AddComponent<ParcelStream>();
        this._stream.Initialize(this._art, this._kit, this._metrics, this._pilot);
        RoadUi.SanitiseTutorials(this._font);
        RoadUi.ThemeSplashSlider();
        this._dresser = this.gameObject.AddComponent<ResultCardDresser>();
        this._dresser.Initialize(this._art, this._font);
        Transform panel = RoadUi.PanelContent(SETTINGS.PANELS.DEFAULT);
        if (panel != null)
        {
            RoadUi.ClearTemplateChrome(panel);
            RectTransform hudHost = RoadUi.Host(panel, "RUN_BOARD");
            this._board = hudHost.gameObject.AddComponent<RunBoard>();
            this._board.Build(hudHost, this._art, this._font, () => this.GoToMenu(), () => this.RaisePause());
        }

        this.BeginRun();
    }

    // -- the run ---------------------------------------------------------------
    private void BeginRun()
    {
        int attempt = RunMemory.TakeAttempt();
        float fall = (this._metrics.SpawnY - this._metrics.CartY) / Mathf.Max(0.01f, this._setup.ScrollSpeed);
        this._plan = this._planner.Build(this._roadIndex, attempt, fall);
        this._rng = new System.Random(this._plan.Seed ^ 0x5F3A);
        this._head = 0;
        this._delivered = 0;
        this._wrongPicks = 0;
        this._crashes = 0;
        this._feathers = RoadTuning.FeatherBudget;
        this._chain = 0;
        this._bestChain = 0;
        this._outOfTime = false;
        this._clockTotal = this._setup.RouteSeconds;
        this._clockLeft = this._setup.RouteSeconds;
        this._introLeft = RoadTuning.IntroSeconds;
        this._hintLeft = RoadTuning.HintWideSeconds;
        this._stream.Begin(this._plan, this._setup, this._rng);
        this._stream.HighlightLane(this._pilot.Lane);
        this._pilot.Introduce();
        if (this._board != null)
        {
            this._board.SetOrder(this._plan, this._head);
            this._board.SetClock(this._clockLeft, this._clockTotal);
            this._board.SetProgress(0, this._plan.Length);
            this._board.SetFeathers(this._feathers);
            this._board.SetHintWide(true);
        }

        this._phase = RunPhase.Rolling;
    }

    private void Update()
    {
        if (this._phase == RunPhase.Idle || this._phase == RunPhase.Over || this._phase == RunPhase.Held)
            return;
        if (BasicController.Instance != null && !BasicController.Instance.IsGameRunning)
            return;
        float dt = Time.deltaTime;
        if (this._hintLeft > 0f)
        {
            this._hintLeft -= dt;
            if (this._hintLeft <= 0f && this._board != null)
                this._board.SetHintWide(false);
        }

        if (this._phase == RunPhase.Rolling)
        {
            this._introLeft -= dt;
            if (this._introLeft <= 0f)
                this._phase = RunPhase.Running;
        }

        this.ReadSteering();
        int picked;
        WaveVerdict verdict = this._stream.Advance(dt, this._plan.ColourAt(this._head), out picked);
        if (verdict == WaveVerdict.Delivered)
            this.Deliver();
        else if (verdict == WaveVerdict.WrongParcel)
            this.MisPick();
        else if (verdict == WaveVerdict.Crash)
            this.Collide();
        if (this._phase != RunPhase.Running)
            return;
        this._clockLeft -= dt;
        if (this._board != null)
            this._board.SetClock(this._clockLeft, this._clockTotal);
        if (this._clockLeft <= 0f)
        {
            this._outOfTime = true;
            this.RaiseLose();
        }
    }

    // -- steering: a horizontal flick, or a tap on the free half of the road ----
    private void ReadSteering()
    {
        if (this._camera == null)
            return;
        int touches = ETouch.activeTouches.Count;
        for (int i = 0; i < touches; i++)
        {
            ETouch touch = ETouch.activeTouches[i];
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                this._swipeTouch = touch.touchId;
                this._swipeFrom = touch.screenPosition;
                this._swipeStarted = Time.unscaledTime;
                continue;
            }

            bool ended = touch.phase == UnityEngine.InputSystem.TouchPhase.Ended;
            if (!ended || touch.touchId != this._swipeTouch)
                continue;
            this._swipeTouch = -1;
            this.Steer(this._swipeFrom, touch.screenPosition, Time.unscaledTime - this._swipeStarted);
        }

        if (touches > 0)
            return;
        Pointer pointer = Pointer.current;
        if (pointer == null)
            return;
        if (pointer.press.wasPressedThisFrame)
        {
            this._swipeFrom = pointer.position.ReadValue();
            this._swipeStarted = Time.unscaledTime;
        }
        else if (pointer.press.wasReleasedThisFrame)
        {
            this.Steer(this._swipeFrom, pointer.position.ReadValue(), Time.unscaledTime - this._swipeStarted);
        }
    }

    /// <summary>
    /// A horizontal flick moves one lane. A short press with no travel counts as a
    /// tap and moves towards the side it landed on - but only inside the band of the
    /// screen the HUD leaves free, so a tap meant for BACK or PAUSE never steers.
    /// </summary>
    private void Steer(Vector2 from, Vector2 to, float seconds)
    {
        if (this._pilot == null || Time.unscaledTime < this._steerLockedUntil)
            return;
        Vector2 travel = to - from;
        bool flick = seconds <= RoadTuning.SwipeSeconds && Mathf.Abs(travel.x) >= RoadTuning.SwipePixels && Mathf.Abs(travel.x) > Mathf.Abs(travel.y) * RoadTuning.SwipeDominance;
        int direction = 0;
        if (flick)
        {
            direction = travel.x < 0f ? -1 : 1;
        }
        else if (Mathf.Abs(travel.x) < RoadTuning.SwipePixels && Mathf.Abs(travel.y) < RoadTuning.SwipePixels)
        {
            float height = Mathf.Max(1f, Screen.height);
            float band = to.y / height;
            if (band < 0.18f || band > 0.7f)
                return;
            direction = to.x < Screen.width * 0.5f ? -1 : 1;
        }

        if (direction == 0)
            return;
        if (this._pilot.Step(direction))
            this._stream.HighlightLane(this._pilot.Lane);
    }

    // -- outcomes --------------------------------------------------------------
    private void Deliver()
    {
        this._delivered++;
        this._chain++;
        this._bestChain = Mathf.Max(this._bestChain, this._chain);
        this._head = Mathf.Min(this._head + 1, this._plan.Length);
        this._pilot.Approve();
        RunMemory.Buzz();
        if (this._board != null)
        {
            this._board.AdvanceStrip(this._plan, this._head);
            this._board.SetProgress(this._delivered, this._plan.Length);
        }

        // A long clean chain hands a spent feather back and buys a little time, so the
        // shrinking resources stay refillable by playing well (rule C.5).
        if (this._chain % RoadTuning.ChainRefund == 0)
        {
            this._feathers = Mathf.Min(RoadTuning.FeatherBudget, this._feathers + 1);
            this._clockLeft = Mathf.Min(this._clockTotal, this._clockLeft + RoadTuning.ChainBonusSeconds);
            if (this._board != null)
            {
                this._board.SetFeathers(this._feathers);
                this._board.SetClock(this._clockLeft, this._clockTotal);
                this._board.ShowClockBonus(RoadTuning.ChainBonusSeconds);
            }
        }

        if (this._delivered >= this._plan.Length)
            this.RaiseWin();
    }

    private void MisPick()
    {
        this._wrongPicks++;
        this.TakeFeather();
    }

    private void Collide()
    {
        this._crashes++;
        this.TakeFeather();
    }

    private void TakeFeather()
    {
        this._chain = 0;
        this._feathers = Mathf.Max(0, this._feathers - 1);
        this._pilot.Reject();
        RunMemory.Buzz();
        if (this._board != null)
            this._board.FlashFeatherLoss(this._feathers);
        if (this._feathers <= 0)
            this.RaiseLose();
    }

    private int Accuracy()
    {
        int attempts = this._delivered + this._wrongPicks + this._crashes;
        if (attempts <= 0)
            return 0;
        return Mathf.RoundToInt(this._delivered * 100f / attempts);
    }

    // -- result cards ----------------------------------------------------------
    private void RaiseWin()
    {
        this._phase = RunPhase.Over;
        this._stream.Clear();
        RunMemory.Record(this._roadIndex, this._delivered, this.Accuracy(), true);
        if (BasicController.Instance != null)
            BasicController.Instance.SetPhysicsRun(false);
        Pop pop = PopsController.Instance.GetPop(SETTINGS.POPS.WIN);
        this._dresser.Dress(pop, "ROUTE DELIVERED", CourierPalette.Lime, this._delivered + " / " + this._plan.Length + " PARCELS", "ACCURACY " + this.Accuracy() + "%\nBEST CHAIN " + this._bestChain, "NEXT ROUTE", () => this.TakeNextRoad(), "RUN AGAIN", () => this.RestartRun(), "MENU", () => this.GoToMenu());
        PopsController.Instance.ShowPop(SETTINGS.POPS.WIN);
    }

    private void RaiseLose()
    {
        this._phase = RunPhase.Over;
        this._stream.Clear();
        RunMemory.Record(this._roadIndex, this._delivered, this.Accuracy(), false);
        if (BasicController.Instance != null)
            BasicController.Instance.SetPhysicsRun(false);
        Pop pop = PopsController.Instance.GetPop(SETTINGS.POPS.LOSE);
        this._dresser.Dress(pop, this._outOfTime ? "OUT OF TIME" : "RUN STOPPED", CourierPalette.Gold, this._delivered + " / " + this._plan.Length + " PARCELS", "ACCURACY " + this.Accuracy() + "%\nBEST CHAIN " + this._bestChain, "RUN AGAIN", () => this.RestartRun(), "MENU", () => this.GoToMenu(), string.Empty, () => this.GoToMenu());
        PopsController.Instance.ShowPop(SETTINGS.POPS.LOSE);
    }

    private void RaisePause()
    {
        if (this._phase == RunPhase.Over || this._phase == RunPhase.Held)
            return;
        this._phase = RunPhase.Held;
        if (BasicController.Instance != null)
            BasicController.Instance.SetPhysicsRun(false);
        Pop pop = PopsController.Instance.GetPop(SETTINGS.POPS.PAUSE);
        this._dresser.Dress(pop, "ROUTE PAUSED", CourierPalette.Cream, RoadTuning.Pad(this._delivered) + " / " + RoadTuning.Pad(this._plan.Length) + " PARCELS", "SWIPE OR TAP LEFT / RIGHT TO CHANGE LANE\nFEATHERS LEFT " + this._feathers + " / " + RoadTuning.FeatherBudget, "RESUME", () => this.ResumeRun(), "RESTART", () => this.RestartRun(), "MENU", () => this.GoToMenu());
        PopsController.Instance.ShowPop(SETTINGS.POPS.PAUSE);
    }

    private void ResumeRun()
    {
        PopsController.Instance.HideAllPops();
        this._phase = RunPhase.Running;
        this._steerLockedUntil = Time.unscaledTime + 0.35f;
        if (BasicController.Instance != null)
            BasicController.Instance.SetPhysicsRun(true);
    }

    private void RestartRun()
    {
        if (BasicController.Instance == null)
            return;
        BasicController.Instance.SetPhysicsRun(true);
        BasicController.Instance.LoadSceneByIndex(SETTINGS.SCENES.SCENE_1);
    }

    private void TakeNextRoad()
    {
        RunMemory.Road = (this._roadIndex + 1) % RoadTuning.Routes.Length;
        this.RestartRun();
    }

    private void GoToMenu()
    {
        if (BasicController.Instance == null)
            return;
        BasicController.Instance.SetPhysicsRun(true);
        BasicController.Instance.LoadSceneByIndex(SETTINGS.SCENES.SCENE_0);
    }

    private void OnDestroy()
    {
        DOTween.Kill(this.transform);
    }
}