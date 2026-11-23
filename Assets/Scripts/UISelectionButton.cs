using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// An artwork thumbnail in the drawer.
///
///  - Drag it up onto a wall: hangs a new copy there.
///  - Tap it: arms it, so the next touch on a wall places it. If a picture on the wall is
///    selected, tapping swaps that picture for this one instead.
///  - Drag sideways: scrolls the list (and never changes anything on the wall).
/// </summary>
public class UISelectionButton : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    public GameObject targetPrefab;
    public ObjectListController objectListController;

    private ScrollRect parentScrollRect;
    private bool isDraggingArt = false;
    private bool pressedWithSelection = false;
    private bool gestureWasDrag = false;
    private int lastClickFrame = -1;

    private void Awake()
    {
        parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    private static bool HasWallSelection =>
        ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null;

    /// <summary>
    /// When the user presses down on an artwork thumbnail, arm the spawner, highlight the
    /// thumbnail, and prepare for instant drag onto the wall. With a picture selected on the
    /// wall nothing is decided yet: a tap will swap it, a drag will hang a new one.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        gestureWasDrag = false;
        isDraggingArt = false;
        pressedWithSelection = HasWallSelection;

        if (!pressedWithSelection)
        {
            ArmSpawner();
        }

        // Initialize drag coordinates
        if (TouchScreenInputManager.Instance != null)
        {
            TouchScreenInputManager.Instance.primaryTouchPosition = eventData.position;
            TouchScreenInputManager.Instance.dragPosition = eventData.position;
            TouchScreenInputManager.Instance.isDoingDrag = true;
        }
    }

    private void ArmSpawner()
    {
        // Arm the spawner immediately so the user can drag directly
        if (ScreenRayInteractor.Instance != null)
        {
            ScreenRayInteractor.Instance.SetTargetPrefab(targetPrefab);
        }

        if (objectListController != null)
        {
            objectListController.currentSelectedIndex = transform.GetSiblingIndex();
        }
    }

    private void BeginArtDrag(PointerEventData eventData)
    {
        isDraggingArt = true;

        // Dragging a thumbnail out while a picture is selected hangs a new one; it does not replace
        if (pressedWithSelection)
        {
            pressedWithSelection = false;
            if (ScreenRayInteractor.Instance != null) ScreenRayInteractor.Instance.ClearSelection();
            ArmSpawner();
        }

        if (TouchScreenInputManager.Instance != null)
        {
            TouchScreenInputManager.Instance.dragPosition = eventData.position;
            TouchScreenInputManager.Instance.isDoingDrag = true;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        gestureWasDrag = true;

        // Upward or diagonal drag towards the wall activates direct artwork placement
        if (Mathf.Abs(eventData.delta.y) >= Mathf.Abs(eventData.delta.x) * 0.4f || eventData.delta.y > 0)
        {
            BeginArtDrag(eventData);
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
        // If the user started scrolling horizontally but then pulls up/out of the box, transition to artwork dragging
        if (!isDraggingArt && eventData.delta.y > 4f)
        {
            if (parentScrollRect != null)
            {
                parentScrollRect.OnEndDrag(eventData);
            }
            BeginArtDrag(eventData);
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
    /// </summary>
    public void SelectArtwork()
    {
        SetTargetPrefab(-1);
    }

    /// <summary>
    /// Click handler. The index argument is ignored (kept so existing Button OnClick bindings
    /// still work): the tile's position in the list is always used.
    /// </summary>
    public void SetTargetPrefab(int i)
    {
        // A click is also reported after a scroll or a drag-out that ends on the tile: ignore those.
        if (gestureWasDrag) return;
        // The prefab's own OnClick binding and the drawer's listener can both fire for one tap.
        if (lastClickFrame == Time.frameCount) return;
        lastClickFrame = Time.frameCount;

        int index = transform.GetSiblingIndex();

        if (ScreenRayInteractor.Instance != null)
        {
            if (ScreenRayInteractor.Instance.currentSelectedObject != null)
            {
                // Replace currently selected artwork preserving its exact size, transform & registration
                ScreenRayInteractor.Instance.ReplaceSelectedArtwork(targetPrefab);
                Haptics.Light();
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
