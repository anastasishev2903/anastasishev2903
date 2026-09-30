using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class InputController : MonoBehaviour
{
    private static InputController Instance;
    public BoxCollider2D CameraTouchBounds;
    private void Awake()
    {
        EnhancedTouchSupport.Enable();
        Instance = this.gameObject.GetComponent<InputController>();
    }

    private Touch? GetFirstTouch_FAST()
    {
        if (!BasicController.Instance.IsGameRunning)
            return null;
        foreach (Touch touch in Touch.activeTouches)
            if (!touch.ended)
                if (this.IsTouchInsideCameraTapBounds(touch))
                    return touch;
        return null;
    }

    private Touch? GetFirstTap_FAST()
    {
        if (!BasicController.Instance.IsGameRunning)
            return null;
        foreach (Touch touch in Touch.activeTouches)
            if (touch.ended)
                if (this.IsTouchInsideCameraTapBounds(touch))
                    return touch;
        return null;
    }

    private Touch? GetFirstTouchInBounds2D(Bounds bounds, TouchPhase touchPhase)
    {
        if (!BasicController.Instance.IsGameRunning)
            return null;
        foreach (Touch touch in Touch.activeTouches)
            if (touch.phase == touchPhase)
            {
                Vector3 touchPosition = Camera.main.ScreenToWorldPoint(touch.screenPosition);
                Vector3 touchPositionObject = new(touchPosition.x, touchPosition.y, bounds.center.z);
                if (bounds.Contains(touchPositionObject) && this.IsTouchInsideCameraTapBounds(touch))
                    return touch;
            }

        return null;
    }

    private Touch? GetFirstTapInBounds2D_FAST(Bounds bounds)
    {
        if (!BasicController.Instance.IsGameRunning)
            return null;
        foreach (Touch touch in Touch.activeTouches)
            if (touch.ended)
            {
                Vector3 touchPosition = Camera.main.ScreenToWorldPoint(touch.screenPosition);
                Vector3 touchPositionObject = new(touchPosition.x, touchPosition.y, bounds.center.z);
                if (bounds.Contains(touchPositionObject) && this.IsTouchInsideCameraTapBounds(touch))
                    return touch;
            }

        return null;
    }

    private Touch? GetFirstTouchInBounds2D_FAST(Bounds bounds)
    {
        if (!BasicController.Instance.IsGameRunning)
            return null;
        foreach (Touch touch in Touch.activeTouches)
            if (!touch.ended)
            {
                Vector3 touchPosition = Camera.main.ScreenToWorldPoint(touch.screenPosition);
                Vector3 touchPositionObject = new(touchPosition.x, touchPosition.y, bounds.center.z);
                if (bounds.Contains(touchPositionObject) && this.IsTouchInsideCameraTapBounds(touch))
                    return touch;
            }

        return null;
    }

    private bool IsTouchInsideBounds(Touch? touch, Bounds bounds, TouchPhase touchPhase)
    {
        if (!BasicController.Instance.IsGameRunning)
        {
            touch = null;
            return false;
        }

        if (touch != null)
            if (touch.Value.phase == touchPhase)
            {
                Vector3 touchPosition = Camera.main.ScreenToWorldPoint(touch.Value.screenPosition);
                Vector3 touchPositionObject = new(touchPosition.x, touchPosition.y, bounds.center.z);
                if (bounds.Contains(touchPositionObject) && this.IsTouchInsideCameraTapBounds(touch.Value))
                    return true;
            }

        return false;
    }

    private void UpdateTouch(Touch? touch)
    {
        if (!BasicController.Instance.IsGameRunning)
        {
            touch = null;
            return;
        }

        int touchId = touch.Value.touchId;
        touch = Touch.activeTouches.FirstOrDefault(t => t.touchId == touchId);
        if (!this.IsTouchInsideCameraTapBounds(touch.Value))
            touch = null;
    }

    private bool IsTouchInsideCameraTapBounds(Touch? touch)
    {
        if (!touch.HasValue)
            return false;
        Vector3 touchPosition = Camera.main.ScreenToWorldPoint(touch.Value.screenPosition);
        Vector3 touchPositionCamera = touchPosition;
        touchPositionCamera.z = this.CameraTouchBounds.transform.position.z;
        if (this.CameraTouchBounds.bounds.Contains(touchPositionCamera))
            return true;
        touch = null;
        return false;
    }
}