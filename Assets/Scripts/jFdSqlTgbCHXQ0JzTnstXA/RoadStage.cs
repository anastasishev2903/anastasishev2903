using UnityEngine;

/// <summary>
/// Every sprite the road is drawn from, handed round in one piece so that no
/// component ever has to look an asset up by name. A director fills it from its
/// own serialized fields, which the scene assigns by guid.
/// </summary>
public sealed class RoadArt
{
    public Sprite Cart;
    public Sprite[] Parcels = new Sprite[0];
    public Sprite HayBale;
    public Sprite CrateStack;
    public Sprite RoadTile;
    public Sprite VergeSign;
    public Sprite LaneFan;
    public Sprite Emblem;
    public Sprite FeatherLive;
    public Sprite FeatherLost;
    public Sprite SlotFrame;
    public Sprite PlatePanel;
    public Sprite MagnetRing;
    public Sprite IconBack;
    public Sprite IconPause;
    public Sprite IconClose;
    public Sprite IconChevron;
    public Sprite TutLane;
    public Sprite TutOrder;
    public Sprite TutFeather;
    /// <summary>The parcel face for a colour id, falling back to the first one so a
    /// missing asset never leaves an invisible pickup on the road.</summary>
    public Sprite ParcelFace(int kind)
    {
        if (this.Parcels == null || this.Parcels.Length == 0)
            return null;
        return this.Parcels[Mathf.Clamp(kind, 0, this.Parcels.Length - 1)];
    }

    public Sprite BlockFace(int variant)
    {
        return variant == 0 ? this.HayBale : this.CrateStack;
    }
}

/// <summary>The spawnable pieces, authored as prefabs so each one arrives already
/// Sliced, already sized and already on the right sorting order.</summary>
public sealed class RoadKit
{
    public GameObject Parcel;
    public GameObject Obstacle;
    public GameObject RoadTile;
    public GameObject VergeSign;
    public GameObject Cart;
    public GameObject MagnetRing;
}

/// <summary>
/// The road geometry, derived from the camera on every run (rule C.0). Nothing
/// here is a pixel count: on a taller phone the road simply gets longer and the
/// parcels keep their share of a lane.
/// </summary>
public sealed class RoadMetrics
{
    public float HalfHeight = 5f;
    public float HalfWidth = 2.3077f;
    public float RoadWidth;
    public float LanePitch;
    public float[] LaneX = new float[RoadTuning.Lanes];
    public Vector2 ParcelSize;
    public Vector2 CartSize;
    public Vector2 BlockSize;
    public Vector2 VergeSize;
    public Vector2 MagnetSize;
    public Vector2 RoadTileSize;
    public float CartY;
    public float SpawnY;
    public float DespawnY;
    public static RoadMetrics FromCamera(Camera cam)
    {
        RoadMetrics m = new RoadMetrics();
        m.HalfHeight = cam == null ? 5f : cam.orthographicSize;
        float aspect = cam == null || cam.aspect <= 0f ? 9f / 19.5f : cam.aspect;
        m.HalfWidth = m.HalfHeight * aspect;
        m.RoadWidth = 2f * m.HalfWidth * RoadTuning.RoadFraction;
        m.LanePitch = m.RoadWidth / RoadTuning.Lanes;
        m.LaneX[0] = -m.LanePitch;
        m.LaneX[1] = 0f;
        m.LaneX[2] = m.LanePitch;
        // Content sprites stay SQUARE: the generated art is square, and drawing a
        // square png into a rectangle turns a parcel into an envelope (rule F.2a).
        float parcel = m.LanePitch * RoadTuning.ParcelFraction;
        m.ParcelSize = new Vector2(parcel, parcel);
        float cart = m.LanePitch * RoadTuning.CartFraction;
        m.CartSize = new Vector2(cart, cart);
        float block = m.LanePitch * RoadTuning.BlockFraction;
        m.BlockSize = new Vector2(block, block);
        float verge = m.LanePitch * RoadTuning.VergeFraction;
        m.VergeSize = new Vector2(verge, verge);
        float magnet = m.LanePitch * RoadTuning.MagnetFraction;
        m.MagnetSize = new Vector2(magnet, magnet);
        // The road tile is a nine-sliced strip: stretching it is its whole purpose.
        m.RoadTileSize = new Vector2(2f * m.HalfWidth, m.HalfHeight * RoadTuning.RoadTileFraction);
        m.CartY = -m.HalfHeight * RoadTuning.CartDepthFraction;
        m.SpawnY = m.HalfHeight + RoadTuning.SpawnMargin;
        m.DespawnY = -m.HalfHeight - RoadTuning.SpawnMargin;
        return m;
    }
}

/// <summary>
/// Thin factory for world art. Every renderer it hands back is Sliced with an
/// explicit size at scale 1, so what the camera maths asked for is exactly what
/// appears on screen.
/// </summary>
public static class RoadStage
{
    public static SpriteRenderer Place(GameObject prefab, Transform parent, string label, Sprite sprite, Vector2 size, Vector2 position, int order, Color tint)
    {
        if (prefab == null)
            return Loose(parent, label, sprite, size, position, order, tint);
        GameObject go = Object.Instantiate(prefab, parent);
        go.name = label;
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<SpriteRenderer>();
        if (sprite != null)
            renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.sortingOrder = order;
        renderer.color = tint;
        return renderer;
    }

    public static SpriteRenderer Loose(Transform parent, string label, Sprite sprite, Vector2 size, Vector2 position, int order, Color tint)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = Vector3.one;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.sortingOrder = order;
        renderer.color = tint;
        return renderer;
    }

    public static void Tint(SpriteRenderer renderer, Color tint)
    {
        if (renderer != null)
            renderer.color = tint;
    }
}