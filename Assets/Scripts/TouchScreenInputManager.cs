using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.Events;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using UnityEngine.EventSystems;

public class TouchScreenInputManager : Singleton<TouchScreenInputManager>
{

    public int touchCount;

    public TextMeshProUGUI debugText;

    public Vector2 primaryTouchPosition;
    public Vector2 dragPosition;

    public Vector2 rotateVector;

    public Mouse mouse;
    public Keyboard keyboard;

    public bool isDoingDrag = false;
    //public bool isDoingMultiTouch = false;

    public UnityEvent OnPrimaryTouchBegan;
    public UnityEvent OnPrimaryTouchBeganWhenSelected;
    //public UnityEvent OnMultiTouchBegan;

    public bool isDoingRotate = false;
    public bool isPinchScaling = false;

#if UNITY_EDITOR
    private bool isMouseWheelScaling = false;
    private float lastMouseWheelTime = 0f;
#endif

    void OnEnable()
    {
#if UNITY_EDITOR
        mouse = Mouse.current;
        keyboard = Keyboard.current;
#endif
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        touchCount = Touch.activeTouches.Count;
#if UNITY_EDITOR
        if (!mouse.leftButton.isPressed) {
            dragPosition = mouse.position.ReadValue();
            isDoingDrag = false;
        } else if (isDoingDrag) {
            dragPosition = mouse.position.ReadValue();
        } else if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) {
            primaryTouchPosition = mouse.position.ReadValue();
            dragPosition = mouse.position.ReadValue();
            isDoingDrag = true;
            if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null) {
                OnPrimaryTouchBeganWhenSelected.Invoke();
            } else {
                OnPrimaryTouchBegan.Invoke();
            }
        }

        if (ScreenRayInteractor.Instance.currentSelectedObject != null) {
            if (keyboard.rightArrowKey.isPressed) {
                AssignmentScript.Instance.ObjectRotation(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, 1f);
            }
            if (keyboard.leftArrowKey.isPressed) {
                AssignmentScript.Instance.ObjectRotation(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, -1f);
            }
            if (keyboard.upArrowKey.isPressed) {
                if (ArtworkSizeManager.Instance != null) {
                    ArtworkSizeManager.Instance.CancelActiveSnap();
                }
                AssignmentScript.Instance.ObjectScaling(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, 1.01f);
            }
            if (keyboard.downArrowKey.isPressed) {
                if (ArtworkSizeManager.Instance != null) {
                    ArtworkSizeManager.Instance.CancelActiveSnap();
                }
                AssignmentScript.Instance.ObjectScaling(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, 0.99f);
            }

            // Snap to nearest A-size preset (A4, A3, A2) when manual scaling key is released
            if (keyboard.upArrowKey.wasReleasedThisFrame || keyboard.downArrowKey.wasReleasedThisFrame) {
                if (ArtworkSizeManager.Instance != null) {
                    ArtworkSizeManager.Instance.SnapToNearestPreset(ScreenRayInteractor.Instance.currentSelectedObject.gameObject);
                }
            }

            // Optional mouse wheel manual scaling with snapping in Editor
            if (mouse != null) {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) {
                    if (ArtworkSizeManager.Instance != null) {
                        ArtworkSizeManager.Instance.CancelActiveSnap();
                    }
                    float scaleFactor = scroll > 0 ? 1.03f : 0.97f;
                    AssignmentScript.Instance.ObjectScaling(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, scaleFactor);
                    isMouseWheelScaling = true;
                    lastMouseWheelTime = Time.time;
                }
                if (isMouseWheelScaling && (Time.time - lastMouseWheelTime > 0.2f)) {
                    isMouseWheelScaling = false;
                    if (ArtworkSizeManager.Instance != null) {
                        ArtworkSizeManager.Instance.SnapToNearestPreset(ScreenRayInteractor.Instance.currentSelectedObject.gameObject);
                    }
                }
            }

            // Also support multi-touch in Editor (if running in Device Simulator with multi-touch enabled)
            if (touchCount > 1) {
                HandleMultiTouchGesture();
            }
        }

        // Snap to nearest A-size preset (A4, A3, A2) when two-finger pinch ends in Editor
        if (isPinchScaling && touchCount < 2) {
            isPinchScaling = false;
            if (ArtworkSizeManager.Instance != null && ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null) {
                ArtworkSizeManager.Instance.SnapToNearestPreset(ScreenRayInteractor.Instance.currentSelectedObject.gameObject);
            }
        }
        if (touchCount < 2) {
            isDoingRotate = false;
        }

#else
        if (touchCount == 1)
        {
            Touch touch = Touch.activeTouches[0];
            if (touch.phase == TouchPhase.Began)
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    primaryTouchPosition = touch.screenPosition;
                    dragPosition = touch.screenPosition;
                    isDoingDrag = true;
                    if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
                    {
                        OnPrimaryTouchBeganWhenSelected.Invoke();
                    }
                    else
                    {
                        OnPrimaryTouchBegan.Invoke();
                    }
                }
            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                if (isDoingDrag)
                {
                    dragPosition = touch.screenPosition;
                }
                else if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    // Finger was dragged off UI onto the scene
                    primaryTouchPosition = touch.screenPosition;
                    dragPosition = touch.screenPosition;
                    isDoingDrag = true;
                    if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
                    {
                        OnPrimaryTouchBeganWhenSelected.Invoke();
                    }
                    else
                    {
                        OnPrimaryTouchBegan.Invoke();
                    }
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                dragPosition = touch.screenPosition;
                isDoingDrag = false;
            }
        }
        else if (touchCount > 1)
        {
            isDoingDrag = false;
            if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
            {
                HandleMultiTouchGesture();
            }
        }
        else
        {
            isDoingDrag = false;
        }

        // Snap to nearest A-size preset (A4, A3, A2) when two-finger pinch ends
        if (isPinchScaling && touchCount < 2)
        {
            isPinchScaling = false;
            if (ArtworkSizeManager.Instance != null && ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
            {
                ArtworkSizeManager.Instance.SnapToNearestPreset(ScreenRayInteractor.Instance.currentSelectedObject.gameObject);
            }
        }
        if (touchCount < 2)
        {
            isDoingRotate = false;
        }
#endif
    }

    /// <summary>
    /// Handles simultaneous two-finger scaling and rotation directly on the screen without needing any UI button held.
    /// Pinching changes the scale factor; twisting the two fingers calculates the signed angle delta and rotates around the wall normal.
    /// </summary>
    private void HandleMultiTouchGesture()
    {
        if (ScreenRayInteractor.Instance == null || ScreenRayInteractor.Instance.currentSelectedObject == null) return;
        if (Touch.activeTouches.Count < 2) return;

        // Skip if either touch is interacting with UI buttons
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Touch touch1 = Touch.activeTouches[0];
        Touch touch2 = Touch.activeTouches[1];

        Vector2 touch1Pos = touch1.screenPosition;
        Vector2 touch2Pos = touch2.screenPosition;

        Vector2 prevTouch1 = touch1Pos - touch1.delta;
        Vector2 prevTouch2 = touch2Pos - touch2.delta;

        // 1. PINCH SCALING
        float prevDistance = (prevTouch1 - prevTouch2).magnitude;
        float currentDistance = (touch1Pos - touch2Pos).magnitude;

        if (prevDistance > 0.001f)
        {
            float scaleFactor = currentDistance / prevDistance;
            if (!float.IsNaN(scaleFactor) && !float.IsInfinity(scaleFactor) && Mathf.Abs(scaleFactor - 1f) > 0.0005f)
            {
                if (ArtworkSizeManager.Instance != null)
                {
                    ArtworkSizeManager.Instance.CancelActiveSnap();
                }
                AssignmentScript.Instance.ObjectScaling(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, scaleFactor);
                isPinchScaling = true;
            }
        }

        // 2. TWO-FINGER TWIST ROTATION
        Vector2 prevDirection = prevTouch2 - prevTouch1;
        Vector2 currentDirection = touch2Pos - touch1Pos;

        // Ensure fingers are at least 10 pixels apart to maintain angle stability
        if (prevDirection.sqrMagnitude > 100f && currentDirection.sqrMagnitude > 100f)
        {
            float angleDelta = Vector2.SignedAngle(prevDirection, currentDirection);
            if (!float.IsNaN(angleDelta) && !float.IsInfinity(angleDelta) && Mathf.Abs(angleDelta) > 0.05f && Mathf.Abs(angleDelta) < 90f)
            {
                AssignmentScript.Instance.ObjectRotation(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, angleDelta);
                isDoingRotate = true;
            }
        }
    }


}



