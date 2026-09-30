using UnityEngine;

public class SetUpAwakeNoDestroyController : MonoBehaviour
{
    public static SetUpAwakeNoDestroyController Instance;
    public bool IsSkipSplashEnabled;
    public bool IsTutorialEnabled;
    public bool IsStoryEnabled;
    public bool IsLevelSelectorEnabled;
    public bool IsTimerEnabled;
    public bool IsCheckScoreEnabled;
    public bool IsBestScoreEnabled;
    public bool IsOnlyWinGameEndEnabled;
    public bool IsLevelIncrementOnWin;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this.gameObject.GetComponent<SetUpAwakeNoDestroyController>();
            DontDestroyOnLoad(this.gameObject);
            this.InitOnAwakeGame();
        }
        else
        {
            this.InitOnAwakeScene();
            Destroy(this.gameObject);
        }
    }

    private void InitOnAwakeGame()
    {
        {
#if !B_LOGS
        {
            Debug.unityLogger.logEnabled = false;
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        }
#endif
        }

        QualitySettings.vSyncCount = 1;
        Application.runInBackground = true;
    //Application.targetFrameRate = 60;
    // Time.fixedDeltaTime = 0.03f; // USE CUSTOM PHYSICS TIME FOR OPTIMIZATION IF NEEDED
    // Add this once at startup to silence the specific assertion
    }

    private void InitOnAwakeScene()
    {
    }
}