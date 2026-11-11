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

    public void SetSelection(ARInteractable target) {
        ClearSelection();
        currentSelectedObject = target;
        currentSelectedObject.SetSelfEmission(true);
    }

    public void DeleteSelection() {
        if (currentSelectedObject != null) {
            string objectName = currentSelectedObject.objectName;
            arObjects.Remove(objectName);
            Destroy(currentSelectedObject.gameObject);
            currentSelectedObject = null;
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
        Ray primaryTouchRay = GetRayFromScreenPosition(TouchScreenInputManager.Instance.primaryTouchPosition);
        RaycastHit hit;
        Physics.Raycast(primaryTouchRay, out hit);
        ARInteractable interactable = FindInteractableRecursivelyUp(hit.collider);
        if (interactable == currentSelectedObject) {
            GameObject currentHitPlane = null;
            Vector3? currentHitPosition = null;
            (currentHitPlane, currentHitPosition) = AssignmentScript.Instance.RaycastDetectionForARPlane(TouchScreenInputManager.Instance.primaryTouchPosition);
            if (currentHitPlane != null) {
                draggingStartWorldPosition = currentHitPosition;
                objectStartPosition = interactable.GetComponent<ARInteractable>().currentAnchorPointInWorld;
                isDragging = true;
                dragLockTimer = dragLockTime;
            }
        } else {
            if (interactable != null)
                SetSelection(interactable);
        }

    }

    private Ray GetRayFromScreenPosition(Vector2 screenPosition) {
        Vector3 ScreenToWorldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, mainCamera.nearClipPlane));
        Vector3 RayDirection = ScreenToWorldPosition - mainCamera.transform.position;
        Ray ray = new Ray(mainCamera.transform.position, RayDirection);
        return ray;
    }

    public void OnPrimayTouchBegan() {
        if (EventSystem.current.IsPointerOverGameObject()) {
            return;
        }
        Vector2 screenPosition = TouchScreenInputManager.Instance.primaryTouchPosition;
        if (PerformScreenRayCast(screenPosition)) {
            return;
        }
        if (currentChosenPrefab == null) {  
            return;
        }
        string objectName = currentChosenPrefab.GetComponent<ARInteractable>().objectName;
        if (arObjects.Contains(objectName)) {
            return;
        }
        arObjects.Add(objectName);
        GameObject currentHitPlane = null;
        Vector3? currentHitPosition = null;
        (currentHitPlane, currentHitPosition) = AssignmentScript.Instance.RaycastDetectionForARPlane(screenPosition);
        GameObject generatedARObject = Instantiate(currentChosenPrefab, (Vector3)currentHitPosition, Quaternion.identity);
        AssignmentScript.Instance.GeometricRegistration(generatedARObject, (Vector3)currentHitPosition, currentHitPlane);
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
                GameObject draggingCurrentHitPlane = null;
                Vector3? draggingCurrentWorldPosition = null;
                (draggingCurrentHitPlane, draggingCurrentWorldPosition) = AssignmentScript.Instance.RaycastDetectionForARPlane(dragScreenPosition);
                if (draggingCurrentHitPlane != null) {

                    Vector3 draggingDelta = (Vector3)draggingCurrentWorldPosition - (Vector3)draggingStartWorldPosition;
                    Vector3 currentAnchorPointInWorld = objectStartPosition + draggingDelta;
                    
                    Vector3 currentPlaneUpDirection = draggingCurrentHitPlane.transform.up;
                    
                    AssignmentScript.Instance.GeometricRegistration(currentSelectedObject.gameObject, currentAnchorPointInWorld, draggingCurrentHitPlane);

                }
            }
        } else {
            isDragging = false;
            draggingStartWorldPosition = null;
        }
    }
    internal void SetTargetPrefab(GameObject targetPrefab) {
        currentChosenPrefab = targetPrefab;
    }

    // Update is called once per frame
    void Update()
    {
        if (spawnLockTimer > 0.0f) {
            spawnLockTimer -= Time.deltaTime;
        }
        if (EventSystem.current.IsPointerOverGameObject()) {
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
                    ClearSelection();
                }
            }
        } 
    }
}
