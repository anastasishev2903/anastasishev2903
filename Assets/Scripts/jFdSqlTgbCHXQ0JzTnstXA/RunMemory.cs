using UnityEngine;

/// <summary>
/// The little that survives between runs: the road the courier picked, the best
/// delivery count and accuracy on each of them, and an attempt counter that makes
/// the generator draw a different road every time the game scene opens.
/// </summary>
public static class RunMemory
{
    private const string RoadKey = "road.pick";
    private const string TryKey = "road.try";
    private const string BestKey = "road.best.";
    private const string AimKey = "road.aim.";
    private const string DoneKey = "road.done.";
    public static int Road
    {
        get
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(RoadKey, 0), 0, RoadTuning.Routes.Length - 1);
        }

        set
        {
            PlayerPrefs.SetInt(RoadKey, Mathf.Clamp(value, 0, RoadTuning.Routes.Length - 1));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Hand out the next attempt number and remember it, so two entries
    /// into the game scene never draw the same road (rule C.11).</summary>
    public static int TakeAttempt()
    {
        int next = PlayerPrefs.GetInt(TryKey, 0) + 1;
        PlayerPrefs.SetInt(TryKey, next);
        PlayerPrefs.Save();
        return next;
    }

    public static int BestDelivered(int road)
    {
        return PlayerPrefs.GetInt(BestKey + road, 0);
    }

    public static int BestAccuracy(int road)
    {
        return PlayerPrefs.GetInt(AimKey + road, 0);
    }

    public static bool Finished(int road)
    {
        return PlayerPrefs.GetInt(DoneKey + road, 0) == 1;
    }

    public static void Record(int road, int delivered, int accuracy, bool finished)
    {
        if (delivered > BestDelivered(road))
            PlayerPrefs.SetInt(BestKey + road, delivered);
        if (accuracy > BestAccuracy(road))
            PlayerPrefs.SetInt(AimKey + road, accuracy);
        if (finished)
            PlayerPrefs.SetInt(DoneKey + road, 1);
        PlayerPrefs.Save();
    }

    /// <summary>The best delivery count across every road, for the menu card.</summary>
    public static int BestDeliveredAnywhere()
    {
        int best = 0;
        for (int i = 0; i < RoadTuning.Routes.Length; i++)
            best = Mathf.Max(best, BestDelivered(i));
        return best;
    }

    public static int BestAccuracyAnywhere()
    {
        int best = 0;
        for (int i = 0; i < RoadTuning.Routes.Length; i++)
            best = Mathf.Max(best, BestAccuracy(i));
        return best;
    }

    /// <summary>A short haptic tick. This game carries no sound at all (rule C.20),
    /// so touch is the only feedback channel besides the screen.</summary>
    public static void Buzz()
    {
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            {
                try
                {
                    Handheld.Vibrate();
                }
                catch (System.Exception)
                {
                }
            }
#endif
        }
    }
}