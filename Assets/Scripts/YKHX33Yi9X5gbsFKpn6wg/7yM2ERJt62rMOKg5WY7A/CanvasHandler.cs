using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Canvas))]
public class CanvasHandler : MonoBehaviour
{
    private static readonly List<CanvasHandler> helpers = new();
    private static UnityEvent OnResolutionOrOrientationChanged = new();
    private static bool screenChangeVarsInitialized;
    private static ScreenOrientation lastOrientation = ScreenOrientation.LandscapeLeft;
    private static Vector2 lastResolution = Vector2.zero;
    private static Rect lastSafeArea = Rect.zero;
    private Canvas canvas;
    private RectTransform rectTransform;
    private RectTransform safeAreaTransform;
    private void Awake()
    {
        if (!helpers.Contains(this))
            helpers.Add(this);
        this.canvas = this.GetComponent<Canvas>();
        this.rectTransform = this.GetComponent<RectTransform>();
        this.safeAreaTransform = this.transform.Find("SafeArea") as RectTransform;
        if (!screenChangeVarsInitialized)
        {
            lastOrientation = Screen.orientation;
            lastResolution.x = Screen.width;
            lastResolution.y = Screen.height;
            lastSafeArea = Screen.safeArea;
            screenChangeVarsInitialized = true;
        }

        this.ApplySafeArea();
    }

    private void Update()
    {
        if (helpers[0] != this)
            return;
        if (Application.isMobilePlatform && Screen.orientation != lastOrientation)
            OrientationChanged();
        if (Screen.safeArea != lastSafeArea)
            SafeAreaChanged();
        if (Screen.width != lastResolution.x || Screen.height != lastResolution.y)
            ResolutionChanged();
    }

    private void OnDestroy()
    {
        if (helpers != null && helpers.Contains(this))
            helpers.Remove(this);
    }

    private void ApplySafeArea()
    {
        if (this.safeAreaTransform == null)
            return;
        Rect safeArea = Screen.safeArea;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= this.canvas.pixelRect.width;
        anchorMin.y /= this.canvas.pixelRect.height;
        anchorMax.x /= this.canvas.pixelRect.width;
        anchorMax.y /= this.canvas.pixelRect.height;
        this.safeAreaTransform.anchorMin = anchorMin;
        this.safeAreaTransform.anchorMax = anchorMax;
    }

    private static void OrientationChanged()
    {
        lastOrientation = Screen.orientation;
        lastResolution.x = Screen.width;
        lastResolution.y = Screen.height;
        OnResolutionOrOrientationChanged.Invoke();
    }

    private static void ResolutionChanged()
    {
        lastResolution.x = Screen.width;
        lastResolution.y = Screen.height;
        OnResolutionOrOrientationChanged.Invoke();
    }

    private static void SafeAreaChanged()
    {
        lastSafeArea = Screen.safeArea;
        for (int i = 0; i < helpers.Count; i++)
            helpers[i].ApplySafeArea();
    }
}