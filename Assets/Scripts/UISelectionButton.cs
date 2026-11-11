using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UISelectionButton : MonoBehaviour
{
    public GameObject targetPrefab;
    public ObjectListController objectListController;

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
        if (ScreenRayInteractor.Instance != null)
        {
            if (ScreenRayInteractor.Instance.currentSelectedObject != null)
            {
                // Replace currently selected artwork preserving its exact size, transform & registration
                ScreenRayInteractor.Instance.ReplaceSelectedArtwork(targetPrefab);
            }
            else
            {
                // Arm spawner to place new artwork when tapping on a wall
                ScreenRayInteractor.Instance.SetTargetPrefab(targetPrefab);
            }
        }
        if (objectListController != null)
        {
            objectListController.currentSelectedIndex = i;
        }
    }
}
