using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>What a wave did to the run at the moment it reached the cart.</summary>
public enum WaveVerdict
{
    Nothing,
    Delivered,
    WrongParcel,
    Crash,
}

/// <summary>One wave of the road as it exists on screen right now.</summary>
public sealed class LiveWave
{
    public Transform Host;
    public int Index;
    public bool Resolved;
    public int[] ParcelLane = new int[0];
    public int[] ParcelKind = new int[0];
    public bool[] ParcelWanted = new bool[0];
    public SpriteRenderer[] ParcelArt = new SpriteRenderer[0];
    public int[] BlockLane = new int[0];
    public SpriteRenderer[] BlockArt = new SpriteRenderer[0];
}

/// <summary>
/// Pours the road past the camera: it lays an endless surface, drops the planned
/// waves at the spawn line, scrolls everything down at the route's speed, and
/// hands each wave to the director the moment it meets the cart.
///
/// The mercy rule lives here, because only here is the cart's CURRENT lane known.
/// At the instant a wave is built, the lane the cart is standing in never receives
/// an obstacle or a parcel of the wrong colour, so a courier who never moves can
/// never take a strike and the run always lasts the whole route clock (rule C.5).
/// The rule is symmetric: when the mercy roll FAILS and the draw had put the wanted
/// parcel in that same lane, the parcel is pushed into a free lane instead. Without
/// that second half a standing courier still meets the right parcel a third of the
/// time by chance, and the game quietly plays itself.
/// </summary>
public sealed class ParcelStream : MonoBehaviour
{
    private readonly List<LiveWave> _live = new List<LiveWave>();
    private readonly List<SpriteRenderer> _surface = new List<SpriteRenderer>();
    private RoadArt _art;
    private RoadKit _kit;
    private RoadMetrics _metrics;
    private RouteSetup _setup;
    private DeliveryPlan _plan;
    private System.Random _rng;
    private CourierCart _cart;
    private Transform _surfaceHost;
    private Transform _waveHost;
    private SpriteRenderer[] _laneGlow = new SpriteRenderer[RoadTuning.Lanes];
    private int _cursor;
    private float _sinceWave;
    private float _scrolled;
    public void Initialize(RoadArt art, RoadKit kit, RoadMetrics metrics, CourierCart cart)
    {
        this._art = art;
        this._kit = kit;
        this._metrics = metrics;
        this._cart = cart;
        this._surfaceHost = new GameObject("RoadSurface").transform;
        this._surfaceHost.SetParent(this.transform, false);
        this._waveHost = new GameObject("WaveStream").transform;
        this._waveHost.SetParent(this.transform, false);
        // Enough tiles to cover a screen and a half, so the seam never shows.
        int tiles = Mathf.CeilToInt(2f * metrics.HalfHeight / metrics.RoadTileSize.y) + 2;
        for (int i = 0; i < tiles; i++)
        {
            SpriteRenderer tile = RoadStage.Place(kit.RoadTile, this._surfaceHost, "RoadTile", art.RoadTile, metrics.RoadTileSize, new Vector2(0f, metrics.DespawnY + metrics.RoadTileSize.y * (i + 0.5f)), RoadTuning.RoadOrder, CourierPalette.Fade(Color.white, 0.94f));
            this._surface.Add(tile);
        }

        for (int i = 0; i < RoadTuning.Lanes; i++)
        {
            this._laneGlow[i] = RoadStage.Loose(this.transform, "LaneGlow", art.SlotFrame, new Vector2(metrics.LanePitch * 0.96f, 2f * metrics.HalfHeight), new Vector2(metrics.LaneX[i], 0f), RoadTuning.LaneGlowOrder, CourierPalette.Fade(CourierPalette.Cyan, 0.06f));
        }
    }

    public void Begin(DeliveryPlan plan, RouteSetup setup, System.Random rng)
    {
        this._plan = plan;
        this._setup = setup;
        this._rng = rng;
        this._cursor = 0;
        this._sinceWave = setup.WaveInterval * 0.35f;
        this.Clear();
    }

    public void Clear()
    {
        for (int i = 0; i < this._live.Count; i++)
        {
            if (this._live[i].Host != null)
                Destroy(this._live[i].Host.gameObject);
        }

        this._live.Clear();
    }

    /// <summary>Light the lane the cart is in, so the live lane reads at a glance.</summary>
    public void HighlightLane(int lane)
    {
        for (int i = 0; i < this._laneGlow.Length; i++)
        {
            if (this._laneGlow[i] == null)
                continue;
            this._laneGlow[i].color = i == lane ? CourierPalette.Fade(CourierPalette.Gold, 0.15f) : CourierPalette.Fade(CourierPalette.Cyan, 0.05f);
        }
    }

    /// <summary>
    /// Advance the road by one frame and report what the wave that met the cart did.
    /// <paramref name = "head"/> is the colour the order strip is currently asking for.
    /// </summary>
    public WaveVerdict Advance(float deltaTime, int head, out int pickedKind)
    {
        pickedKind = -1;
        if (this._setup == null || this._plan == null)
            return WaveVerdict.Nothing;
        float travel = this._setup.ScrollSpeed * deltaTime;
        this.ScrollRoad(travel);
        this._sinceWave += deltaTime;
        if (this._sinceWave >= this._setup.WaveInterval && this._cursor < this._plan.Waves.Length)
        {
            this._sinceWave -= this._setup.WaveInterval;
            this.Drop(this._plan.Waves[this._cursor], this._cursor, head);
            this._cursor++;
        }

        WaveVerdict verdict = WaveVerdict.Nothing;
        for (int i = this._live.Count - 1; i >= 0; i--)
        {
            LiveWave wave = this._live[i];
            if (wave.Host == null)
            {
                this._live.RemoveAt(i);
                continue;
            }

            Vector3 position = wave.Host.localPosition;
            position.y -= travel;
            wave.Host.localPosition = position;
            if (!wave.Resolved && position.y <= this._metrics.CartY)
            {
                wave.Resolved = true;
                int kind;
                WaveVerdict met = this.Meet(wave, out kind);
                if (met != WaveVerdict.Nothing)
                {
                    verdict = met;
                    pickedKind = kind;
                }
            }

            if (position.y < this._metrics.DespawnY)
            {
                Destroy(wave.Host.gameObject);
                this._live.RemoveAt(i);
            }
        }

        return verdict;
    }

    private void ScrollRoad(float travel)
    {
        float span = this._metrics.RoadTileSize.y;
        this._scrolled += travel;
        for (int i = 0; i < this._surface.Count; i++)
        {
            SpriteRenderer tile = this._surface[i];
            if (tile == null)
                continue;
            float baseY = this._metrics.DespawnY + span * (i + 0.5f);
            float y = baseY - Mathf.Repeat(this._scrolled, span * this._surface.Count);
            while (y < this._metrics.DespawnY)
                y += span * this._surface.Count;
            tile.transform.localPosition = new Vector3(0f, y, 0f);
        }
    }

    // -- one wave --------------------------------------------------------------
    private void Drop(DeliveryWave plan, int index, int head)
    {
        int cartLane = this._cart != null ? this._cart.Lane : 1;
        int wantedLane = plan.WantedLane;
        int liarLane = plan.LiarLane;
        List<int> blocks = new List<int>();
        for (int i = 0; i < plan.BlockLanes.Length; i++)
            blocks.Add(plan.BlockLanes[i]);
        // Nothing that can cost a feather is allowed into the lane the cart stands in.
        blocks.Remove(cartLane);
        if (liarLane == cartLane)
            liarLane = -1;
        if (wantedLane >= 0)
        {
            if (this.Roll() < this._setup.MercyChance)
            {
                wantedLane = cartLane;
                blocks.Remove(cartLane);
            }
            else if (wantedLane == cartLane)
            {
                for (int offset = 1; offset <= RoadTuning.Lanes - 1; offset++)
                {
                    int candidate = (cartLane + offset) % RoadTuning.Lanes;
                    if (candidate == liarLane)
                        continue;
                    wantedLane = candidate;
                    break;
                }

                blocks.Remove(wantedLane);
            }
        }

        GameObject host = new GameObject("RoadWave");
        host.transform.SetParent(this._waveHost, false);
        host.transform.localPosition = new Vector3(0f, this._metrics.SpawnY, 0f);
        LiveWave wave = new LiveWave();
        wave.Host = host.transform;
        wave.Index = index;
        List<int> lanes = new List<int>();
        List<int> kinds = new List<int>();
        List<bool> wanted = new List<bool>();
        List<SpriteRenderer> art = new List<SpriteRenderer>();
        if (wantedLane >= 0)
        {
            lanes.Add(wantedLane);
            kinds.Add(head);
            wanted.Add(true);
            art.Add(this.Parcel(host.transform, wantedLane, head));
        }

        if (liarLane >= 0)
        {
            int kind = (head + plan.LiarOffset) % RoadTuning.ParcelKinds;
            lanes.Add(liarLane);
            kinds.Add(kind);
            wanted.Add(false);
            art.Add(this.Parcel(host.transform, liarLane, kind));
        }

        List<SpriteRenderer> blockArt = new List<SpriteRenderer>();
        for (int i = 0; i < blocks.Count; i++)
            blockArt.Add(this.Block(host.transform, blocks[i], plan.BlockVariant));
        if (plan.VergeSide != 0)
        {
            float edge = this._metrics.HalfWidth - this._metrics.LanePitch * 0.22f;
            SpriteRenderer sign = RoadStage.Place(this._kit.VergeSign, host.transform, "VergeSign", this._art.VergeSign, this._metrics.VergeSize, new Vector2(plan.VergeSide * edge, 0f), RoadTuning.VergeOrder, CourierPalette.Fade(CourierPalette.Cream, 0.92f));
            // The sign art is declared side view facing RIGHT, so the left verge gets a
            // deliberate mirror and the right verge none (rule C.8). flipX keeps the
            // transform scale at 1, which is what rule C.0 asks of a Sliced renderer.
            sign.flipX = plan.VergeSide < 0;
        }

        wave.ParcelLane = lanes.ToArray();
        wave.ParcelKind = kinds.ToArray();
        wave.ParcelWanted = wanted.ToArray();
        wave.ParcelArt = art.ToArray();
        wave.BlockLane = blocks.ToArray();
        wave.BlockArt = blockArt.ToArray();
        this._live.Add(wave);
    }

    private SpriteRenderer Parcel(Transform host, int lane, int kind)
    {
        return RoadStage.Place(this._kit.Parcel, host, "Parcel", this._art.ParcelFace(kind), this._metrics.ParcelSize, new Vector2(this._metrics.LaneX[lane], 0f), RoadTuning.ParcelOrder, Color.white);
    }

    private SpriteRenderer Block(Transform host, int lane, int variant)
    {
        return RoadStage.Place(this._kit.Obstacle, host, "Obstacle", this._art.BlockFace(variant), this._metrics.BlockSize, new Vector2(this._metrics.LaneX[lane], 0f), RoadTuning.BlockOrder, Color.white);
    }

    // -- the moment a wave meets the cart --------------------------------------
    private WaveVerdict Meet(LiveWave wave, out int pickedKind)
    {
        pickedKind = -1;
        int lane = this._cart != null ? this._cart.Lane : 1;
        for (int i = 0; i < wave.BlockLane.Length; i++)
        {
            if (wave.BlockLane[i] != lane)
                continue;
            this.Smash(wave.BlockArt[i]);
            return WaveVerdict.Crash;
        }

        for (int i = 0; i < wave.ParcelLane.Length; i++)
        {
            if (wave.ParcelLane[i] != lane)
                continue;
            pickedKind = wave.ParcelKind[i];
            if (wave.ParcelWanted[i])
            {
                this.Collect(wave.ParcelArt[i]);
                return WaveVerdict.Delivered;
            }

            this.Spoil(wave.ParcelArt[i]);
            return WaveVerdict.WrongParcel;
        }

        return WaveVerdict.Nothing;
    }

    private void Collect(SpriteRenderer parcel)
    {
        if (parcel == null)
            return;
        Transform host = parcel.transform;
        DOTween.Kill(host);
        host.DOScale(Vector3.one * 1.35f, 0.1f).SetEase(Ease.OutQuad).OnComplete(() => this.Vanish(parcel));
    }

    private void Vanish(SpriteRenderer parcel)
    {
        if (parcel == null)
            return;
        parcel.transform.DOScale(0f, 0.08f).SetEase(Ease.InQuad);
    }

    private void Spoil(SpriteRenderer parcel)
    {
        if (parcel == null)
            return;
        DOTween.Kill(parcel);
        parcel.DOColor(CourierPalette.Alarm, 0.09f);
        parcel.transform.DOScale(0f, 0.12f).SetEase(Ease.InQuad).SetDelay(0.09f);
    }

    private void Smash(SpriteRenderer block)
    {
        if (block == null)
            return;
        DOTween.Kill(block);
        block.DOColor(CourierPalette.Alarm, 0.09f).SetLoops(2, LoopType.Yoyo);
        block.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 6, 0.6f);
    }

    private float Roll()
    {
        return this._rng == null ? 0.5f : (float)this._rng.NextDouble();
    }

    private void OnDestroy()
    {
        DOTween.Kill(this.transform);
    }
}