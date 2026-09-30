using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class SafeAreaHandler : MonoBehaviour
{
    private static ScreenOrientation _lastOrientation = ScreenOrientation.LandscapeLeft;
    private static bool _screenChangeVarsInitialized;
    private static Rect _lastSafeArea = Rect.zero;
    private static Vector2 _lastResolution = Vector2.zero;
    private static readonly List<SafeAreaHandler> _helpers = new();
    private static UnityEvent OnResolutionOrOrientationChanged = new();
    private Canvas _canvas;
    private CanvasScaler _canvasScaler;
    private RectTransform _rectTransform;
    private RectTransform _safeAreaTransform;
    private Vector2 _baseReferenceResolution;
    private void Awake()
    {
        if (!_helpers.Contains(this))
            _helpers.Add(this);
        this._canvas = this.GetComponent<Canvas>();
        this._canvasScaler = this.GetComponent<CanvasScaler>();
        if (this._canvasScaler != null)
            this._baseReferenceResolution = this._canvasScaler.referenceResolution;
        this._rectTransform = this.GetComponent<RectTransform>();
        this._safeAreaTransform = this.transform.Find("SafeArea") as RectTransform;
        if (!_screenChangeVarsInitialized)
        {
            _lastOrientation = Screen.orientation;
            _lastResolution.x = Screen.width;
            _lastResolution.y = Screen.height;
            _lastSafeArea = Screen.safeArea;
            _screenChangeVarsInitialized = true;
        }

        this.ApplySafeArea();
    }

    private void Start()
    {
    }

    private void Update()
    {
        if (_helpers.Count == 0 || _helpers[0] != this)
            return;
        if (Application.isMobilePlatform && Screen.orientation != _lastOrientation)
            OrientationChanged();
        if (Screen.safeArea != _lastSafeArea)
            SafeAreaChanged();
        if (Screen.width != _lastResolution.x || Screen.height != _lastResolution.y)
            ResolutionChanged();
    }

    private void OnDestroy()
    {
        if (_helpers != null && _helpers.Contains(this))
            _helpers.Remove(this);
    }

    private void ApplySafeArea()
    {
        if (this._safeAreaTransform == null)
            return;
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        if (screenWidth <= 0f || screenHeight <= 0f)
            return;
        Rect safeArea = Screen.safeArea;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= screenWidth;
        anchorMin.y /= screenHeight;
        anchorMax.x /= screenWidth;
        anchorMax.y /= screenHeight;
        this._safeAreaTransform.anchorMin = anchorMin;
        this._safeAreaTransform.anchorMax = anchorMax;
        this._safeAreaTransform.offsetMin = Vector2.zero;
        this._safeAreaTransform.offsetMax = Vector2.zero;
        if (this._canvasScaler == null)
            return;
        Vector2 anchorDelta = anchorMax - anchorMin;
        float scaleFactorX = 2f - anchorDelta.x;
        float scaleFactorY = 2f - anchorDelta.y;
        this._canvasScaler.referenceResolution = this._baseReferenceResolution * new Vector2(scaleFactorX, scaleFactorY);
    }

    private static void OrientationChanged()
    {
        _lastOrientation = Screen.orientation;
        _lastResolution.x = Screen.width;
        _lastResolution.y = Screen.height;
        _lastSafeArea = Screen.safeArea;
        ApplySafeAreaToAll();
        OnResolutionOrOrientationChanged.Invoke();
    }

    private static void ResolutionChanged()
    {
        _lastResolution.x = Screen.width;
        _lastResolution.y = Screen.height;
        _lastSafeArea = Screen.safeArea;
        ApplySafeAreaToAll();
        OnResolutionOrOrientationChanged.Invoke();
    }

    private static void ApplySafeAreaToAll()
    {
        for (int i = 0; i < _helpers.Count; i++)
            _helpers[i].ApplySafeArea();
    }

    private static void SafeAreaChanged()
    {
        _lastSafeArea = Screen.safeArea;
        ApplySafeAreaToAll();
    }
}