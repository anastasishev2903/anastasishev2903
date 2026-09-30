using UnityEngine;

/// <summary>
/// One wave of the road as the planner drew it, before the spawner knows which
/// lane the cart is standing in. Lanes run 0..2 and -1 means "nothing here".
/// The wrong parcel is stored as an OFFSET from the colour the order currently
/// asks for, so a wave keeps its shape while the head of the order moves on.
/// </summary>
public sealed class DeliveryWave
{
    /// <summary>Lane carrying the parcel the order wants next, or -1.</summary>
    public int WantedLane = -1;
    /// <summary>Lane carrying a parcel of the wrong colour, or -1.</summary>
    public int LiarLane = -1;
    /// <summary>1..3 - how far the wrong colour sits from the wanted one.</summary>
    public int LiarOffset = 1;
    /// <summary>Lanes blocked by hay or crates. Length 0, 1 or 2.</summary>
    public int[] BlockLanes = new int[0];
    /// <summary>Which obstacle art this wave uses: 0 hay bale, 1 crate stack.</summary>
    public int BlockVariant;
    /// <summary>A roadside sign on the left verge (-1), the right (1) or neither (0).</summary>
    public int VergeSide;
}

/// <summary>
/// A whole road: the colour chain the order strip walks through, and the waves
/// that roll down it. Both come out of one seeded planner, so two attempts on the
/// same road differ in layout and in decoys, not only in the HUD numbers.
/// </summary>
public sealed class DeliveryPlan
{
    public int RoadIndex;
    public int Attempt;
    public int Seed;
    public int[] Chain = new int[0];
    public DeliveryWave[] Waves = new DeliveryWave[0];
    /// <summary>Waves the greedy courier needed to finish, for the log line.</summary>
    public int SolvedWaves;
    /// <summary>Seconds the greedy courier needed, for the log line.</summary>
    public float SolvedSeconds;
    /// <summary>True when the layout came from the safety net, not the planner.</summary>
    public bool FromFallback;
    public int Length
    {
        get
        {
            return this.Chain.Length;
        }
    }

    public int ColourAt(int index)
    {
        if (this.Chain.Length == 0)
            return 0;
        return this.Chain[Mathf.Clamp(index, 0, this.Chain.Length - 1)];
    }
}