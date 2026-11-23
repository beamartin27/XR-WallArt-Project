using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UISelectionButton : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    public GameObject targetPrefab;
    public ObjectListController objectListController;

    private ScrollRect parentScrollRect;
    private bool isDraggingArt = false;

    private void Awake()
    {
        parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    /// <summary>
    /// When the user presses down on an artwork thumbnail, immediately arm the spawner,
    /// highlight the thumbnail, and prepare for instant drag onto the wall.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        int index = transform.GetSiblingIndex();

        // If an artwork is currently selected on the wall, replace it immediately
        if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
        {
            ScreenRayInteractor.Instance.ReplaceSelectedArtwork(targetPrefab);
            if (objectListController != null)
            {
                objectListController.ClearSelectionHighlight();
            }
            return;
        }

        // Arm the spawner immediately so the user can drag directly
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
            TouchScreenInputManager.Instance.isDoingDrag = true;
        }

        isDraggingArt = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Upward or diagonal drag towards the wall activates direct artwork placement
        if (Mathf.Abs(eventData.delta.y) >= Mathf.Abs(eventData.delta.x) * 0.4f || eventData.delta.y > 0)
        {
            isDraggingArt = true;
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
        // If the finger moves upward out of the drawer, switch to artwork dragging
        if (!isDraggingArt && eventData.delta.y > 4f)
        {
            isDraggingArt = true;
            if (parentScrollRect != null)
            {
                parentScrollRect.OnEndDrag(eventData);
            }
        }

        if (isDraggingArt)
        {
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
        if (TouchScreenInputManager.Instance != null)
        {
            TouchScreenInputManager.Instance.isDoingDrag = false;
        }
        isDraggingArt = false;
    }

    /// <summary>
    /// Convenient parameterless method for Unity UI Button OnClick.
    /// Automatically infers the button's index from its sibling index in the container.
    /// </summary>
    public void SelectArtwork()
    {
        int index = transform.GetSiblingIndex();
        SetTargetPrefab(index);
    }

    public void SetTargetPrefab(int i)
    {
        int index = (i >= 0) ? i : transform.GetSiblingIndex();

        if (ScreenRayInteractor.Instance != null)
        {
            if (ScreenRayInteractor.Instance.currentSelectedObject != null)
            {
                // Replace currently selected artwork preserving its exact size, transform & registration
                ScreenRayInteractor.Instance.ReplaceSelectedArtwork(targetPrefab);
                if (objectListController != null)
                {
                    objectListController.ClearSelectionHighlight();
                }
            }
            else
            {
                // Arm spawner to place new artwork when dragging or tapping on a wall
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
