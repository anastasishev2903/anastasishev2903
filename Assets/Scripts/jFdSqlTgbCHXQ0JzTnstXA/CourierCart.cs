using DG.Tweening;
using UnityEngine;

/// <summary>
/// The magnet cart at the bottom of the road: which lane it sits in, how it slides
/// between them, and the two reactions the player reads as feedback - a tilt into
/// the turn, and a shake when the wrong parcel sticks to the magnet.
/// </summary>
public sealed class CourierCart : MonoBehaviour
{
    private RoadMetrics _metrics;
    private SpriteRenderer _body;
    private SpriteRenderer _ring;
    private float _ringBase = 1f;
    private int _lane = 1;
    private bool _sliding;
    public int Lane
    {
        get
        {
            return this._lane;
        }
    }

    public void Initialize(RoadArt art, RoadKit kit, RoadMetrics metrics)
    {
        this._metrics = metrics;
        this._lane = 1;
        // The magnet halo sits UNDER the cart, so the cart art is never washed out
        // by it; both live well inside the -19..-1 sprite band (rule C.21).
        this._ring = RoadStage.Place(kit.MagnetRing, this.transform, "CartMagnet", art.MagnetRing, metrics.MagnetSize, new Vector2(metrics.LaneX[1], metrics.CartY), RoadTuning.MagnetOrder, CourierPalette.Fade(CourierPalette.Cyan, 0.38f));
        this._ringBase = 1f;
        this._body = RoadStage.Place(kit.Cart, this.transform, "CourierCart", art.Cart, metrics.CartSize, new Vector2(metrics.LaneX[1], metrics.CartY), RoadTuning.CartOrder, Color.white);
        // The cart art is declared nose UP and horizontally symmetric, so it is never
        // mirrored - only tilted into the turn (rule C.8).
        this._body.transform.localScale = Vector3.one;
        this._body.transform.localRotation = Quaternion.identity;
    }

    /// <summary>Arrival flourish: the cart grows into place from its computed size.</summary>
    public void Introduce()
    {
        if (this._body == null)
            return;
        Transform host = this._body.transform;
        host.localScale = Vector3.one * 0.72f;
        DOTween.Kill(host);
        host.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
    }

    /// <summary>One lane left (-1) or right (+1). A step during a slide is ignored,
    /// so a fast player cannot queue a crossing of the whole road.</summary>
    public bool Step(int direction)
    {
        if (this._metrics == null || this._body == null || this._sliding)
            return false;
        int next = Mathf.Clamp(this._lane + (direction < 0 ? -1 : 1), 0, RoadTuning.Lanes - 1);
        if (next == this._lane)
            return false;
        this._lane = next;
        this._sliding = true;
        Transform host = this._body.transform;
        float tilt = direction < 0 ? 9f : -9f;
        float targetX = this._metrics.LaneX[this._lane];
        DOTween.Kill(host);
        host.DOLocalMoveX(targetX, RoadTuning.LaneSlideSeconds).SetEase(Ease.OutCubic).OnComplete(() => this.SettleSlide());
        host.DOLocalRotate(new Vector3(0f, 0f, tilt), RoadTuning.LaneSlideSeconds * 0.75f).SetEase(Ease.OutCubic).OnComplete(() => this.LevelOut());
        if (this._ring != null)
        {
            Transform ringHost = this._ring.transform;
            DOTween.Kill(ringHost);
            ringHost.DOLocalMoveX(targetX, RoadTuning.LaneSlideSeconds).SetEase(Ease.OutCubic);
        }

        return true;
    }

    private void SettleSlide()
    {
        this._sliding = false;
    }

    private void LevelOut()
    {
        if (this._body == null)
            return;
        this._body.transform.DOLocalRotate(Vector3.zero, RoadTuning.LaneSlideSeconds).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// The magnet pulls in the parcel the order wanted: the halo flares out from its
    /// computed base scale and washes lime, then settles back (rule C.0 - the pulse
    /// is expressed as a multiple of the base size, never as a literal).
    /// </summary>
    public void Approve()
    {
        if (this._ring == null)
            return;
        Transform host = this._ring.transform;
        DOTween.Kill(host);
        DOTween.Kill(this._ring);
        host.localScale = Vector3.one * this._ringBase;
        host.DOScale(this._ringBase * 1.35f, 0.18f).SetEase(Ease.OutQuad).OnComplete(() => this.SettleRing());
        this._ring.color = CourierPalette.Fade(CourierPalette.Lime, 0.85f);
        this._ring.DOColor(CourierPalette.Fade(CourierPalette.Cyan, 0.38f), 0.35f);
    }

    private void SettleRing()
    {
        if (this._ring == null)
            return;
        this._ring.transform.DOScale(this._ringBase, 0.16f).SetEase(Ease.InQuad);
    }

    /// <summary>Shake plus a red wash - the wrong colour, or hay taken head on.</summary>
    public void Reject()
    {
        if (this._body != null)
        {
            Transform host = this._body.transform;
            DOTween.Kill(host);
            host.DOShakePosition(0.3f, 0.12f, 18, 90f, false, true).OnComplete(() => this.Recentre());
        }

        if (this._ring != null)
        {
            DOTween.Kill(this._ring);
            this._ring.color = CourierPalette.Fade(CourierPalette.Alarm, 0.9f);
            this._ring.DOColor(CourierPalette.Fade(CourierPalette.Cyan, 0.38f), 0.4f);
        }
    }

    private void Recentre()
    {
        if (this._body == null || this._metrics == null)
            return;
        this._body.transform.localPosition = new Vector3(this._metrics.LaneX[this._lane], this._metrics.CartY, 0f);
        this._body.transform.localRotation = Quaternion.identity;
        this._sliding = false;
    }

    private void OnDestroy()
    {
        if (this._body != null)
            DOTween.Kill(this._body.transform);
        if (this._ring != null)
        {
            DOTween.Kill(this._ring.transform);
            DOTween.Kill(this._ring);
        }

        DOTween.Kill(this.transform);
    }
}