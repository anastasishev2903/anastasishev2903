using UnityEngine;

/// <summary>
/// One road of the delivery job: how many parcels it asks for, how fast the road
/// scrolls, how often a wave arrives, how long the route clock runs, and how
/// generous a wave is towards a courier who never changes lane.
/// </summary>
public sealed class RouteSetup
{
    public string Caption;
    public int Parcels;
    public float ScrollSpeed;
    public float WaveInterval;
    public float RouteSeconds;
    public float MercyChance;
    public int BlocksFromWave;
    public int BlocksMax;
}

/// <summary>
/// Every number a run is built from. Nothing here is a pixel count for the world:
/// the layout is derived from the camera at runtime (rule C.0) and only the ratios
/// live here, so the same values hold on any aspect ratio.
/// </summary>
public static class RoadTuning
{
    // -- sorting orders --------------------------------------------------------
    // The background canvas draws at -30 and the pops at +10, so every world sprite
    // has to sit strictly between them (rule C.21). No two layers that can overlap
    // share a number.
    public const int RoadOrder = -18;
    public const int LaneGlowOrder = -16;
    public const int VergeOrder = -14;
    public const int BlockOrder = -8;
    public const int ParcelOrder = -7;
    public const int MagnetOrder = -5;
    public const int CartOrder = -4;
    public const int MenuFanOrder = -12;
    public const int MenuCartOrder = -6;
    public const int MenuParcelOrder = -5;
    // -- world layout, all as fractions of the camera --------------------------
    public const float RoadFraction = 0.86f;
    public const float ParcelFraction = 0.58f;
    public const float CartFraction = 0.80f;
    public const float BlockFraction = 0.66f;
    public const float VergeFraction = 0.40f;
    public const float MagnetFraction = 1.20f;
    public const float RoadTileFraction = 0.50f;
    public const float CartDepthFraction = 0.55f;
    public const float SpawnMargin = 0.6f;
    // -- run rules -------------------------------------------------------------
    public const int Lanes = 3;
    public const int ParcelKinds = 4;
    public const int FeatherBudget = 3;
    public const int OrderWindow = 5;
    /// <summary>A chain this long hands one spent feather back (rule C.5: the
    /// shrinking resource has to be refillable by playing well).</summary>
    public const int ChainRefund = 6;
    /// <summary>Seconds the route clock gains with every refunded chain.</summary>
    public const float ChainBonusSeconds = 2f;
    public const float LaneSlideSeconds = 0.16f;
    public const float IntroSeconds = 1f;
    public const float HintWideSeconds = 11f;
    public const float ClockWarnSeconds = 15f;
    // Swipe recognition: a horizontal flick beats a vertical one, and one flick is
    // exactly one lane - the queue never stacks up.
    public const float SwipePixels = 45f;
    public const float SwipeSeconds = 0.28f;
    public const float SwipeDominance = 1.2f;
    // -- wave shapes (per cent, the remainder is a breather wave) ---------------
    public const int WeightSingle = 35;
    public const int WeightPair = 30;
    public const int WeightGuarded = 25;
    /// <summary>How much slack a greedy courier must still have on the clock
    /// before a generated route is accepted.</summary>
    public const float SolverSlackSeconds = 12f;
    public static readonly string[] ParcelNames =
    {
        "GOLD",
        "RED",
        "CYAN",
        "LIME"
    };
    public static readonly RouteSetup[] Routes =
    {
        new RouteSetup
        {
            Caption = "FARM LANE",
            Parcels = 24,
            ScrollSpeed = 3.2f,
            WaveInterval = 1.4f,
            RouteSeconds = 100f,
            MercyChance = 0.24f,
            BlocksFromWave = 5,
            BlocksMax = 1,
        },
        new RouteSetup
        {
            Caption = "SUNSET MILE",
            Parcels = 30,
            ScrollSpeed = 3.7f,
            WaveInterval = 1.22f,
            RouteSeconds = 105f,
            MercyChance = 0.22f,
            BlocksFromWave = 3,
            BlocksMax = 2,
        },
        new RouteSetup
        {
            Caption = "STORM RIDGE",
            Parcels = 36,
            ScrollSpeed = 4.2f,
            WaveInterval = 1.08f,
            RouteSeconds = 110f,
            MercyChance = 0.15f,
            BlocksFromWave = 1,
            BlocksMax = 2,
        },
    };
    public static RouteSetup Route(int index)
    {
        return Routes[Mathf.Clamp(index, 0, Routes.Length - 1)];
    }

    /// <summary>mm:ss with two digits a side, so the read-out never reflows.</summary>
    public static string Clock(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = whole / 60;
        int rest = whole % 60;
        return (minutes < 10 ? "0" : string.Empty) + minutes + ":" + (rest < 10 ? "0" : string.Empty) + rest;
    }

    /// <summary>Two-digit counter, so "07 / 24" keeps its width for a whole run.</summary>
    public static string Pad(int value)
    {
        return value < 10 ? "0" + value : value.ToString();
    }
}