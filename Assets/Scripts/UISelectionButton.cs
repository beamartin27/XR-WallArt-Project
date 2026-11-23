using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles interaction for an artwork button in the presets drawer:
/// - Tap: Replaces the selected artwork in-place if an object is selected; otherwise arms spawner for tap-to-place.
/// - Drag: Directly drags a NEW artwork into the room with live wall preview and solidification on release,
///   WITHOUT replacing any already selected artwork.
/// </summary>
public class UISelectionButton : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    public GameObject targetPrefab;
    public ObjectListController objectListController;

    private ScrollRect parentScrollRect;
    private bool isDraggingArt = false;
    private bool hasDragged = false;
    private Vector2 pointerDownPosition;

    private void Awake()
    {
        parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    private int GetArtworkIndex()
    {
        if (objectListController != null)
        {
            int idx = objectListController.GetArtworkButtonIndex(gameObject);
            if (idx >= 0) return idx;
        }
        return transform.GetSiblingIndex();
    }

    /// <summary>
    /// When the user presses down on an artwork thumbnail, arm the dragged prefab
    /// and prepare for either tap (replace) or drag (add new artwork).
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        int index = GetArtworkIndex();
        pointerDownPosition = eventData.position;
        hasDragged = false;
        isDraggingArt = false;

        // Arm the spawner with targetPrefab so drag placement preview has the correct model
        if (ScreenRayInteractor.Instance != null)
        {
            ScreenRayInteractor.Instance.SetTargetPrefab(targetPrefab);
        }

        if (objectListController != null)
        {
            objectListController.currentSelectedIndex = index;
        }

        // Initialize drag coordinates
        if (TouchScreenInputManager.Instance != null)
        {
            TouchScreenInputManager.Instance.primaryTouchPosition = eventData.position;
            TouchScreenInputManager.Instance.dragPosition = eventData.position;
            TouchScreenInputManager.Instance.isDoingDrag = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - pointerDownPosition;

        // Upward drag towards the wall activates direct artwork placement
        if (delta.y > 10f || Mathf.Abs(delta.y) >= Mathf.Abs(delta.x) * 0.6f)
        {
            isDraggingArt = true;
            hasDragged = true;
            if (TouchScreenInputManager.Instance != null)
            {
                TouchScreenInputManager.Instance.dragPosition = eventData.position;
                TouchScreenInputManager.Instance.isDoingDrag = true;
            }
        }
        else
        {
            isDraggingArt = false;
            if (parentScrollRect != null)
            {
                parentScrollRect.OnBeginDrag(eventData);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - pointerDownPosition;

        // If the finger moves upward out of the drawer, switch to artwork dragging
        if (!isDraggingArt && delta.y > 15f)
        {
            isDraggingArt = true;
            hasDragged = true;
            if (parentScrollRect != null)
            {
                parentScrollRect.OnEndDrag(eventData);
            }
        }

        if (isDraggingArt)
        {
            hasDragged = true;
            if (TouchScreenInputManager.Instance != null)
            {
                TouchScreenInputManager.Instance.dragPosition = eventData.position;
                TouchScreenInputManager.Instance.isDoingDrag = true;
            }
        }
        else
        {
            if (parentScrollRect != null)
            {
                parentScrollRect.OnDrag(eventData);
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isDraggingArt)
        {
            if (TouchScreenInputManager.Instance != null)
            {
                TouchScreenInputManager.Instance.dragPosition = eventData.position;
                TouchScreenInputManager.Instance.isDoingDrag = false;
            }
            isDraggingArt = false;
        }
        else
        {
            if (parentScrollRect != null)
            {
                parentScrollRect.OnEndDrag(eventData);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDraggingArt)
        {
            if (TouchScreenInputManager.Instance != null)
            {
                TouchScreenInputManager.Instance.isDoingDrag = false;
            }
            isDraggingArt = false;
        }

        // If release was within small distance without dragging, treat as tap
        float dist = Vector2.Distance(eventData.position, pointerDownPosition);
        if (dist < 12f && !hasDragged)
        {
            HandleTap();
        }
    }

    /// <summary>
    /// Convenient parameterless method for Unity UI Button OnClick.
    /// </summary>
    public void SelectArtwork()
    {
        if (!hasDragged)
        {
            HandleTap();
        }
    }

    public void SetTargetPrefab(int i)
    {
        if (!hasDragged)
        {
            HandleTap();
        }
    }

    private void HandleTap()
    {
        int index = GetArtworkIndex();

        if (ScreenRayInteractor.Instance != null)
        {
            if (ScreenRayInteractor.Instance.currentSelectedObject != null)
            {
                // Tapping thumbnail while an artwork is selected replaces it in-place
                ScreenRayInteractor.Instance.ReplaceSelectedArtwork(targetPrefab);
                if (objectListController != null)
                {
                    objectListController.ClearSelectionHighlight();
                }
            }
            else
            {
                // Tapping thumbnail without selection arms spawner for tap-to-place
                ScreenRayInteractor.Instance.SetTargetPrefab(targetPrefab);
                if (objectListController != null)
                {
                    objectListController.currentSelectedIndex = index;
                }
            }
        }
        else if (objectListController != null)
        {
            objectListController.currentSelectedIndex = index;
        }
    }
}
