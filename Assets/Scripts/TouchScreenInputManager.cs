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

    void OnEnable() {
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
        if (EventSystem.current.IsPointerOverGameObject()) {
            return;
        }
        if (mouse.leftButton.isPressed && isDoingDrag) {
            dragPosition = mouse.position.ReadValue();

        } else if (mouse.leftButton.isPressed && !isDoingDrag) {
            primaryTouchPosition = mouse.position.ReadValue();
            if (ScreenRayInteractor.Instance.currentSelectedObject != null) {
                OnPrimaryTouchBeganWhenSelected.Invoke();
            } else {
                OnPrimaryTouchBegan.Invoke();
            }
            dragPosition = mouse.position.ReadValue();
            isDoingDrag = true;
        } else if (!mouse.leftButton.isPressed) {
            dragPosition = mouse.position.ReadValue();
            isDoingDrag = false;
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

        }

#else
        if (touchCount > 0) {
            if ((touchCount == 1) && (Touch.activeTouches[0].phase == TouchPhase.Began)) {
                primaryTouchPosition = Touch.activeTouches[0].screenPosition;
                if (ScreenRayInteractor.Instance.currentSelectedObject != null) {
                    OnPrimaryTouchBeganWhenSelected.Invoke();
                    isDoingDrag = true;
                } else {
                    OnPrimaryTouchBegan.Invoke();
                }
            } else if ((touchCount == 1) && (Touch.activeTouches[0].phase == TouchPhase.Moved || Touch.activeTouches[0].phase == TouchPhase.Stationary)) {
                dragPosition = Touch.activeTouches[0].screenPosition;
                isDoingDrag = true;
            } else if ((touchCount == 1) && (Touch.activeTouches[0].phase == TouchPhase.Ended)) {
                dragPosition = Touch.activeTouches[0].screenPosition;
                isDoingDrag = false;
            } else if (touchCount > 1) {
                if (ScreenRayInteractor.Instance.currentSelectedObject != null) {
                    Touch touch1 = Touch.activeTouches[0];
                    Touch touch2 = Touch.activeTouches[1];
                    if (EventSystem.current.IsPointerOverGameObject()) {
                        if (isDoingRotate) {
                            float rotateAngle = 0f;
                            Vector2 touch1Delta = touch1.delta;
                            Vector2 touch2Delta = touch2.delta;
                            if (touch1Delta.magnitude > touch2Delta.magnitude) {
                                rotateAngle = (touch1.delta.y / Screen.height) * 360f;
                            } else {
                                rotateAngle = (touch2.delta.y / Screen.height) * 360f;
                            }
                            AssignmentScript.Instance.ObjectRotation(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, rotateAngle);
                        }
                    } else {
                        Vector2 LastFrameTouch1 = touch1.screenPosition - touch1.delta;
                        Vector2 LastFrameTouch2 = touch2.screenPosition - touch2.delta;
                        float LastFrameTouchDelta = (LastFrameTouch1 - LastFrameTouch2).magnitude;
                        float CurrentFrameTouchDelta = (touch1.screenPosition - touch2.screenPosition).magnitude;
                        if (LastFrameTouchDelta > 0.001f) {
                            float scaleFactor = CurrentFrameTouchDelta / LastFrameTouchDelta;
                            if (!float.IsNaN(scaleFactor) && !float.IsInfinity(scaleFactor) && Mathf.Abs(scaleFactor - 1f) > 0.0005f) {
                                if (ArtworkSizeManager.Instance != null) {
                                    ArtworkSizeManager.Instance.CancelActiveSnap();
                                }
                                AssignmentScript.Instance.ObjectScaling(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, scaleFactor);
                                isPinchScaling = true;
                            }
                        }
                    }
                }
            }
        }

        // Snap to nearest A-size preset (A4, A3, A2) when two-finger pinch ends
        if (isPinchScaling && touchCount < 2) {
            isPinchScaling = false;
            if (ArtworkSizeManager.Instance != null && ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null) {
                ArtworkSizeManager.Instance.SnapToNearestPreset(ScreenRayInteractor.Instance.currentSelectedObject.gameObject);
            }
        }
#endif


    }


}
