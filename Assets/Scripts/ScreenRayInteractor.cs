using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class ScreenRayInteractor : Singleton<ScreenRayInteractor>
{
    public Camera mainCamera;

    public Material outlineMaterial;
    
    public ARInteractable currentSelectedObject;

    private Vector3? draggingStartWorldPosition;
    private Vector3 objectStartPosition;

    public bool isDragging = false;

    public bool isSpawnerActivated = false;
    public GameObject currentChosenPrefab;

    [Header("Placement Preview")]
    public GameObject previewInstance = null;
    private WallHit previewHit;
    public bool isPreviewActive = false;

    public float spawnLockTime = 0.3f;
    private float spawnLockTimer = 0.0f;

    public float dragLockTime = 0.05f;
    private float dragLockTimer = 0.0f;

    public List<string> arObjects = new List<string>();


    public void ClearSelection() {
        if (currentSelectedObject != null) {
            currentSelectedObject.SetSelfEmission(false);
            currentSelectedObject = null;
        }
    }

    public void CancelPlacementPreview() {
        if (previewInstance != null) {
            Destroy(previewInstance);
            previewInstance = null;
        }
        isPreviewActive = false;
        previewHit = default;
    }

    public void SetSelection(ARInteractable target) {
        if (target == null) return;
        bool changed = currentSelectedObject != target;
        ClearSelection();
        currentSelectedObject = target;
        currentSelectedObject.SetSelfEmission(true);
        if (changed) Haptics.Light();
    }

    public void DeleteSelection() {
        if (currentSelectedObject != null) {
            string objectName = currentSelectedObject.objectName;
            arObjects.Remove(objectName);
            Destroy(currentSelectedObject.gameObject);
            currentSelectedObject = null;
        }
    }

    public void ReplaceSelectedArtwork(GameObject newPrefab) {
        if (currentSelectedObject == null || newPrefab == null) return;

        Transform oldTransform = currentSelectedObject.transform;
        Vector3 pos = oldTransform.position;
        Quaternion rot = oldTransform.rotation;
        Vector3 scale = oldTransform.localScale;
        GameObject plane = currentSelectedObject.currentARPlane;
        Vector3 anchorPoint = currentSelectedObject.currentAnchorPointInWorld;
        Vector3 normal = currentSelectedObject.currentNormalDirectionInWorld;
        string oldName = currentSelectedObject.objectName;

        ArtworkFrame oldFrame = currentSelectedObject.GetComponent<ArtworkFrame>();
        FrameStyle frameStyle = oldFrame != null ? oldFrame.Style : ArtworkFrame.DefaultStyle;

        ArtworkProperties oldProps = currentSelectedObject.GetComponent<ArtworkProperties>();
        ArtworkSizePreset oldPreset = oldProps != null ? oldProps.currentPreset : ArtworkSizeManager.Instance.GetNearestPreset(scale.x);

        arObjects.Remove(oldName);

        // Turn off outline before destroying
        currentSelectedObject.SetSelfEmission(false);
        Destroy(currentSelectedObject.gameObject);

        // Instantiate new artwork at the exact same position, rotation, and scale
        GameObject newObj = Instantiate(newPrefab, pos, rot);
        newObj.SetActive(true);
        newObj.transform.localScale = scale;

        // The new picture keeps the frame the old one had
        if (ArtworkFrame.CanFrame(newObj)) {
            ArtworkFrame.GetOrAdd(newObj).Apply(frameStyle);
        }

        ArtworkProperties newProps = newObj.GetComponent<ArtworkProperties>();
        if (newProps != null) {
            newProps.currentPreset = oldPreset;
        }

        ARInteractable newInteractable = newObj.GetComponent<ARInteractable>();
        if (newInteractable != null) {
            string baseName = newPrefab.GetComponent<ARInteractable>() != null ? newPrefab.GetComponent<ARInteractable>().objectName : "Artwork";
            string instanceName = baseName + "_" + newObj.GetInstanceID();
            newInteractable.objectName = instanceName;
            newInteractable.currentAnchorPointInWorld = anchorPoint;
            newInteractable.currentNormalDirectionInWorld = normal;
            newInteractable.currentARPlane = plane;
            arObjects.Add(instanceName);
            newObj.name = instanceName;
            if (newObj.GetComponent<WallAttachment>() == null) newObj.AddComponent<WallAttachment>();
            SetSelection(newInteractable);
        }
    }
    void Start()
    {

    }

    public ARInteractable FindInteractableRecursivelyUp(Collider collider) {
        GameObject currentObject = collider.gameObject;
        ARInteractable interactable = currentObject.GetComponent<ARInteractable>();
        while (interactable == null) {
            if (currentObject.transform.parent != null) {
                currentObject = currentObject.transform.parent.gameObject;
                interactable = currentObject.GetComponent<ARInteractable>();
            } else {
                break;
            }
        }
        return interactable;
    }

    public void OnPrimaryTouchBeganWhenSelected() {
        if (EventSystem.current.IsPointerOverGameObject()) {
            return;
        }
        Ray primaryTouchRay = GetRayFromScreenPosition(TouchScreenInputManager.Instance.primaryTouchPosition);
        RaycastHit hit;
        if (Physics.Raycast(primaryTouchRay, out hit)) {
            ARInteractable interactable = FindInteractableRecursivelyUp(hit.collider);
            if (interactable == currentSelectedObject) {
                WallHit startHit = WallDetection.Raycast(TouchScreenInputManager.Instance.primaryTouchPosition, mainCamera);
                if (startHit.valid) {
                    draggingStartWorldPosition = startHit.point;
                    objectStartPosition = interactable.currentAnchorPointInWorld;
                    isDragging = true;
                    dragLockTimer = dragLockTime;
                }
            } else if (interactable != null) {
                SetSelection(interactable);
            } else {
                ClearSelection();
            }
        } else {
            ClearSelection();
        }
    }

    private Ray GetRayFromScreenPosition(Vector2 screenPosition) {
        Vector3 ScreenToWorldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, mainCamera.nearClipPlane));
        Vector3 RayDirection = ScreenToWorldPosition - mainCamera.transform.position;
        Ray ray = new Ray(mainCamera.transform.position, RayDirection);
        return ray;
    }

    public void OnPrimayTouchBegan() {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
            return;
        }
        Vector2 screenPosition = TouchScreenInputManager.Instance.primaryTouchPosition;
        if (PerformScreenRayCast(screenPosition)) {
            return;
        }
        if (currentChosenPrefab != null) {
            UpdatePlacementPreview();
        }
    }

    private void UpdatePlacementPreview() {
        if (currentChosenPrefab == null) return;

        Vector2 dragScreenPos = TouchScreenInputManager.Instance.dragPosition;

        // If touch is over UI, hide preview until dragged onto the scene
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
            if (previewInstance != null) {
                previewInstance.SetActive(false);
            }
            return;
        }

        WallHit hit = WallDetection.Raycast(dragScreenPos, mainCamera);

        if (hit.valid) {
            if (previewInstance == null) {
                previewInstance = Instantiate(currentChosenPrefab, hit.point, Quaternion.identity);
                previewInstance.SetActive(true);
                previewInstance.name = "Artwork_Preview";

                // New pictures get the frame the user last chose
                if (ArtworkFrame.CanFrame(previewInstance)) {
                    ArtworkFrame.GetOrAdd(previewInstance).Apply(ArtworkFrame.DefaultStyle);
                }

                // Disable colliders on the preview so it doesn't block rays or trigger physics
                foreach (var col in previewInstance.GetComponentsInChildren<Collider>()) {
                    col.enabled = false;
                }

                // Show emission outline to indicate dynamic preview
                ARInteractable interactable = previewInstance.GetComponent<ARInteractable>();
                if (interactable != null) {
                    interactable.SetSelfEmission(true);
                }

                isPreviewActive = true;
            }

            // Smoothly update registration and position flush against the detected wall
            AssignmentScript.Instance.GeometricRegistration(previewInstance, hit.point, hit.normal, hit.plane);
            previewInstance.SetActive(true);
            previewHit = hit;
        }
    }

    private void SolidifyPlacementPreview() {
        if (previewInstance != null && previewHit.valid) {
            GameObject finalObject = previewInstance;
            Vector3 finalPos = previewHit.point;
            Vector3 finalNormal = previewHit.normal;
            GameObject finalPlane = previewHit.plane; // null when hung on an estimated surface

            // Null out preview references FIRST so no cleanup routine or selection change can touch finalObject
            previewInstance = null;
            isPreviewActive = false;
            previewHit = default;
            currentChosenPrefab = null;
            isSpawnerActivated = false;

            finalObject.SetActive(true);

            // Re-enable colliders so the placed artwork can be selected and manipulated
            foreach (var col in finalObject.GetComponentsInChildren<Collider>()) {
                col.enabled = true;
            }

            // Firmly lock geometric registration at the release point on the wall
            AssignmentScript.Instance.GeometricRegistration(finalObject, finalPos, finalNormal, finalPlane);

            ARInteractable interactable = finalObject.GetComponent<ARInteractable>();
            if (interactable != null) {
                string baseName = "Artwork";
                ArtworkProperties props = finalObject.GetComponent<ArtworkProperties>();
                if (props != null && !string.IsNullOrEmpty(props.artworkTitle)) {
                    baseName = props.artworkTitle;
                }
                string instanceName = baseName + "_" + finalObject.GetInstanceID();
                interactable.objectName = instanceName;
                interactable.currentAnchorPointInWorld = finalPos;
                interactable.currentNormalDirectionInWorld = finalNormal;
                interactable.currentARPlane = finalPlane;
                arObjects.Add(instanceName);
                finalObject.name = instanceName;

                // Keep it flush with the wall as the device refines where the wall is
                if (finalObject.GetComponent<WallAttachment>() == null) finalObject.AddComponent<WallAttachment>();

                // Select the newly placed artwork (turns on emission outline)
                SetSelection(interactable);
            }

            // Tactile bounce pop animation on solidification
            StartCoroutine(SolidifyPopAnimation(finalObject.transform));
            Haptics.Medium();
            if (WallArtCoach.Instance != null) WallArtCoach.Instance.NotifyPlaced();

            // Clear the selection highlight on the Art button in the drawer!
            if (ObjectListController.Instance != null) {
                ObjectListController.Instance.ClearSelectionHighlight();
            }
        } else {
            CancelPlacementPreview();
        }
    }

    private IEnumerator SolidifyPopAnimation(Transform target) {
        if (target == null) yield break;
        Vector3 baseScale = target.localScale;
        float elapsed = 0f;
        float duration = 0.15f;
        while (elapsed < duration) {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Slight overshoot pop: 1.0 -> 1.08 -> 1.0
            float scaleMultiplier = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
            target.localScale = baseScale * scaleMultiplier;
            yield return null;
        }
        if (target != null) target.localScale = baseScale;
    }

    public bool PerformScreenRayCast(Vector2 screenPosition) {
        Ray ray = GetRayFromScreenPosition(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit)) {
            if (hit.collider.gameObject.tag == "ARObject") {
                
                if (currentSelectedObject == null) {
                    SetSelection(FindInteractableRecursivelyUp(hit.collider));
                    return true;
                }

            }
        }
        return false;
    }

    public void DraggingUpdate() {
        if (TouchScreenInputManager.Instance.isDoingDrag) {
            Vector2 dragScreenPosition = TouchScreenInputManager.Instance.dragPosition;

            if (isDragging) {
                WallHit dragHit = WallDetection.Raycast(dragScreenPosition, mainCamera);

                if (dragHit.valid && draggingStartWorldPosition.HasValue)
                {
                    // Offset between the object's anchor and initial touch point
                    Vector3 grabOffset =
                        objectStartPosition - draggingStartWorldPosition.Value;

                    // Normal of the wall currently detected
                    Vector3 planeNormal = dragHit.normal;

                    // Keep the offset parallel to the new wall
                    Vector3 offsetAlongPlane =
                        Vector3.ProjectOnPlane(grabOffset, planeNormal);

                    // Place the object's anchor on the detected wall
                    Vector3 targetAnchor = dragHit.point + offsetAlongPlane;

                    AssignmentScript.Instance.GeometricRegistration(
                        currentSelectedObject.gameObject,
                        targetAnchor,
                        planeNormal,
                        dragHit.plane
                    );
                }
            }
        } else {
            isDragging = false;
            draggingStartWorldPosition = null;
        }
    }
    internal void SetTargetPrefab(GameObject targetPrefab) {
        CancelPlacementPreview();
        currentChosenPrefab = targetPrefab;
        isSpawnerActivated = (targetPrefab != null);
    }

    // Update is called once per frame
    void Update()
    {
        if (spawnLockTimer > 0.0f) {
            spawnLockTimer -= Time.deltaTime;
        }

        // 1. Dynamic Placement Preview Mode (placing new artwork)
        if (currentChosenPrefab != null) {
            if (TouchScreenInputManager.Instance.isDoingDrag) {
                UpdatePlacementPreview();
            } else {
                if (isPreviewActive) {
                    // Let go over the wall: hang it. Let go back over the UI (preview hidden): cancel,
                    // but stay armed so the user can try again.
                    if (previewInstance != null && previewInstance.activeSelf) {
                        SolidifyPlacementPreview();
                    } else {
                        CancelPlacementPreview();
                    }
                }
            }
            return;
        }

        // 2. Already selected object dragging mode
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
            return;
        }
        if (currentSelectedObject == null) {
            return;
        }
        if (isDragging) {
            dragLockTimer -= Time.deltaTime;
            if (dragLockTimer <= 0.0f) {
                DraggingUpdate();
            } else {
                if (!TouchScreenInputManager.Instance.isDoingDrag) {
                    isDragging = false;
                    dragLockTimer = 0.0f;
                    // Tapped the selected object: keep selection active
                }
            }
        } 
    }
}
