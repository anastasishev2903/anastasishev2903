using UnityEngine;

/// <summary>
/// Draws a fresh road for every attempt and refuses to hand one over until a
/// greedy courier has been shown to finish it inside the route clock (rule C.11).
/// The randomness comes from a private System.Random seeded per attempt, never
/// from UnityEngine.Random, which is global state shared with the template.
/// </summary>
public sealed class DeliveryPlanner
{
    /// <summary>
    /// Build a road for this attempt. Up to twenty layouts are drawn and measured
    /// against <paramref name = "fallSeconds"/> - the time a wave needs to travel from
    /// the spawn line down to the cart, taken from the camera by the caller. If none
    /// of them can be finished in time, the safety net below is used instead.
    /// </summary>
    public DeliveryPlan Build(int roadIndex, int attempt, float fallSeconds)
    {
        RouteSetup setup = RoadTuning.Route(roadIndex);
        int seed = (roadIndex * 7919) ^ (attempt * 104729);
        System.Random rng = new System.Random(seed);
        DeliveryPlan plan = null;
        for (int i = 0; i < 20; i++)
        {
            DeliveryPlan candidate = this.Draw(roadIndex, attempt, seed, setup, rng);
            if (this.IsDeliverable(candidate, setup, fallSeconds))
            {
                plan = candidate;
                break;
            }
        }

        if (plan == null)
        {
            plan = this.SafetyNet(roadIndex, attempt, seed, setup, rng);
            plan.FromFallback = true;
            this.IsDeliverable(plan, setup, fallSeconds);
        }

        {
#if B_LOGS
            {
                Debug.Log("[road] seed=" + plan.Seed + " parcels=" + plan.Length + " waves=" + plan.Waves.Length + " solved=" + plan.SolvedWaves + " in " + plan.SolvedSeconds.ToString("0.0") + "s" + (plan.FromFallback ? " (safety net)" : string.Empty));
            }
#endif
        }

        return plan;
    }

    // -- layout ----------------------------------------------------------------
    private DeliveryPlan Draw(int roadIndex, int attempt, int seed, RouteSetup setup, System.Random rng)
    {
        DeliveryPlan plan = new DeliveryPlan();
        plan.RoadIndex = roadIndex;
        plan.Attempt = attempt;
        plan.Seed = seed;
        plan.Chain = this.Colours(setup.Parcels, rng);
        int waveCount = Mathf.CeilToInt(setup.RouteSeconds / setup.WaveInterval) + 6;
        DeliveryWave[] waves = new DeliveryWave[waveCount];
        for (int i = 0; i < waveCount; i++)
            waves[i] = this.Wave(i, setup, rng);
        plan.Waves = waves;
        return plan;
    }

    /// <summary>The colour chain the order strip walks, never three alike running.</summary>
    private int[] Colours(int length, System.Random rng)
    {
        int[] chain = new int[length];
        for (int i = 0; i < length; i++)
        {
            int pick = rng.Next(RoadTuning.ParcelKinds);
            if (i >= 2 && chain[i - 1] == pick && chain[i - 2] == pick)
                pick = (pick + 1 + rng.Next(RoadTuning.ParcelKinds - 1)) % RoadTuning.ParcelKinds;
            chain[i] = pick;
        }

        return chain;
    }

    private DeliveryWave Wave(int index, RouteSetup setup, System.Random rng)
    {
        DeliveryWave wave = new DeliveryWave();
        wave.VergeSide = rng.Next(5) == 0 ? (rng.Next(2) == 0 ? -1 : 1) : 0;
        wave.BlockVariant = rng.Next(2);
        int roll = rng.Next(100);
        bool blocksAllowed = index >= setup.BlocksFromWave;
        if (roll < RoadTuning.WeightSingle)
        {
            // SINGLE - one parcel on the road, usually the one the order wants.
            int lane = rng.Next(RoadTuning.Lanes);
            if (rng.Next(100) < 70)
            {
                wave.WantedLane = lane;
            }
            else
            {
                wave.LiarLane = lane;
                wave.LiarOffset = 1 + rng.Next(RoadTuning.ParcelKinds - 1);
            }

            return wave;
        }

        if (roll < RoadTuning.WeightSingle + RoadTuning.WeightPair)
        {
            // PAIR - the wanted parcel and one liar, never sharing a lane.
            int wanted = rng.Next(RoadTuning.Lanes);
            int liar = (wanted + 1 + rng.Next(RoadTuning.Lanes - 1)) % RoadTuning.Lanes;
            wave.WantedLane = wanted;
            wave.LiarLane = liar;
            wave.LiarOffset = 1 + rng.Next(RoadTuning.ParcelKinds - 1);
            return wave;
        }

        if (roll < RoadTuning.WeightSingle + RoadTuning.WeightPair + RoadTuning.WeightGuarded)
        {
            // GUARDED - a parcel with hay or a crate stack standing in another lane.
            int parcelLane = rng.Next(RoadTuning.Lanes);
            if (rng.Next(100) < 60)
            {
                wave.WantedLane = parcelLane;
            }
            else
            {
                wave.LiarLane = parcelLane;
                wave.LiarOffset = 1 + rng.Next(RoadTuning.ParcelKinds - 1);
            }

            if (blocksAllowed)
            {
                int blocked = (parcelLane + 1 + rng.Next(RoadTuning.Lanes - 1)) % RoadTuning.Lanes;
                wave.BlockLanes = new[]
                {
                    blocked
                };
            }

            return wave;
        }

        // BREATHER - open road, or one or two obstacles with nothing to pick up.
        if (blocksAllowed)
        {
            int first = rng.Next(RoadTuning.Lanes);
            if (setup.BlocksMax >= 2 && rng.Next(100) < 35)
            {
                int second = (first + 1 + rng.Next(RoadTuning.Lanes - 1)) % RoadTuning.Lanes;
                wave.BlockLanes = new[]
                {
                    first,
                    second
                };
            }
            else
            {
                wave.BlockLanes = new[]
                {
                    first
                };
            }
        }

        return wave;
    }

    /// <summary>
    /// A layout that cannot be unwinnable: every wave carries the wanted parcel and
    /// an obstacle only ever stands in a lane the parcel is not in. Reached only
    /// when twenty drawn layouts failed the solver, which the weights say should not
    /// happen - it exists so that a bad seed can never ship a dead run.
    /// </summary>
    private DeliveryPlan SafetyNet(int roadIndex, int attempt, int seed, RouteSetup setup, System.Random rng)
    {
        DeliveryPlan plan = new DeliveryPlan();
        plan.RoadIndex = roadIndex;
        plan.Attempt = attempt;
        plan.Seed = seed;
        plan.Chain = this.Colours(setup.Parcels, rng);
        int waveCount = Mathf.CeilToInt(setup.RouteSeconds / setup.WaveInterval) + 6;
        DeliveryWave[] waves = new DeliveryWave[waveCount];
        for (int i = 0; i < waveCount; i++)
        {
            DeliveryWave wave = new DeliveryWave();
            wave.WantedLane = i % RoadTuning.Lanes;
            wave.VergeSide = i % 4 == 0 ? -1 : (i % 4 == 2 ? 1 : 0);
            if (i >= setup.BlocksFromWave && i % 3 == 0)
                wave.BlockLanes = new[]
                {
                    (wave.WantedLane + 1) % RoadTuning.Lanes
                };
            waves[i] = wave;
        }

        plan.Waves = waves;
        return plan;
    }

    // -- solver ----------------------------------------------------------------
    /// <summary>
    /// A greedy courier walks the plan: on every wave it moves into the lane of the
    /// wanted parcel, and on a wave without one it parks where nothing blocks. A
    /// wave is reachable when the fall from the spawn line down to the cart outlasts
    /// the lane changes it takes to meet it - with three lanes that is at most two
    /// slides, far under the fall. The road is accepted only when the courier
    /// finishes with real slack still on the clock.
    /// </summary>
    public bool IsDeliverable(DeliveryPlan plan, RouteSetup setup, float fallSeconds)
    {
        float budget = setup.RouteSeconds - RoadTuning.SolverSlackSeconds;
        int lane = 1;
        int delivered = 0;
        plan.SolvedWaves = plan.Waves.Length;
        plan.SolvedSeconds = setup.RouteSeconds;
        for (int i = 0; i < plan.Waves.Length; i++)
        {
            DeliveryWave wave = plan.Waves[i];
            float arrival = i * setup.WaveInterval + fallSeconds;
            int target = wave.WantedLane >= 0 ? wave.WantedLane : this.OpenLane(wave, lane);
            float slide = Mathf.Abs(target - lane) * RoadTuning.LaneSlideSeconds;
            if (slide > fallSeconds)
                continue;
            lane = target;
            if (wave.WantedLane == lane && !this.Blocked(wave, lane))
                delivered++;
            if (delivered >= plan.Length)
            {
                plan.SolvedWaves = i + 1;
                plan.SolvedSeconds = arrival;
                return arrival <= budget;
            }
        }

        return false;
    }

    private bool Blocked(DeliveryWave wave, int lane)
    {
        for (int i = 0; i < wave.BlockLanes.Length; i++)
        {
            if (wave.BlockLanes[i] == lane)
                return true;
        }

        return false;
    }

    private int OpenLane(DeliveryWave wave, int current)
    {
        if (!this.Blocked(wave, current) && wave.LiarLane != current)
            return current;
        for (int i = 0; i < RoadTuning.Lanes; i++)
        {
            if (!this.Blocked(wave, i) && wave.LiarLane != i)
                return i;
        }

        for (int i = 0; i < RoadTuning.Lanes; i++)
        {
            if (!this.Blocked(wave, i))
                return i;
        }

        return current;
    }
}