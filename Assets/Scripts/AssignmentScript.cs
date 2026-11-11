using System.Collections;
using TMPro;
using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class AssignmentScript : Singleton<AssignmentScript> {

    // Task 1: Raycasting

    public (GameObject, Vector3?) RaycastDetectionForARPlane(Vector2 screenPosition) {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return (null, null);
        }

        // Camera position = ray origin
        Vector3 cameraPosition = mainCamera.transform.position;

        // Convert the 2D touch position into a 3D point
        // on the camera's near clipping plane
        Vector3 cameraSpacePosition = new Vector3(
            screenPosition.x,
            screenPosition.y,
            mainCamera.nearClipPlane
        );

        Vector3 worldSpacePosition = mainCamera.ScreenToWorldPoint(cameraSpacePosition);

        // Direction from camera towards the touched point
        Vector3 rayDirection = (worldSpacePosition - cameraPosition).normalized;

        Ray dragRay = new Ray(cameraPosition, rayDirection);

        // Detect every collider along the ray
        RaycastHit[] hits = Physics.RaycastAll(dragRay);

        GameObject closestARPlane = null;
        Vector3? closestHitPoint = null;
        float closestDistance = Mathf.Infinity;

        
        foreach (RaycastHit hit in hits)
        {
            ARPlane plane = hit.collider.GetComponentInParent<ARPlane>();

            // Only accept detected vertical AR planes
            if (plane == null || plane.alignment != PlaneAlignment.Vertical)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestARPlane = plane.gameObject;
                closestHitPoint = hit.point;
            }
        }


        return (closestARPlane, closestHitPoint);
    }

    // Task 2: Geometric Registration

    public void GeometricRegistration(GameObject arObject, Vector3 hitPoint, GameObject arPlane) {
        ARInteractable interactable = arObject.GetComponent<ARInteractable>();

        if (interactable == null || arPlane == null)
        {
            return;
        }

        // Virtual object's plane definition in local/object space
        Vector3 objectAnchor = interactable.AnchorPointOfObject;

        Vector3 objectNormal = interactable.NormalDirectionOfObject.normalized;

        // Normal of the detected AR plane in world space
        Vector3 planeNormal = arPlane.transform.up.normalized;

        // Rotate the object so its normal matches the wall normal while keeping it upright.
        // (FromToRotation alone leaves the roll around the normal undefined: when the two
        // normals are nearly opposite it picks an arbitrary axis, so posters came out tilted.)

        // The object's own "up" direction, perpendicular to its normal (local space)
        Vector3 objectUp = Vector3.ProjectOnPlane(Vector3.up, objectNormal);
        if (objectUp.sqrMagnitude < 1e-6f)
            objectUp = Vector3.ProjectOnPlane(Vector3.forward, objectNormal);
        objectUp.Normalize();

        // Keep the object's current up direction (preserves rotation the user applied),
        // flattened onto the wall. A freshly spawned object is upright, so it stays upright.
        Vector3 currentUpWorld = arObject.transform.TransformDirection(objectUp);
        Vector3 upOnPlane = Vector3.ProjectOnPlane(currentUpWorld, planeNormal);
        if (upOnPlane.sqrMagnitude < 1e-4f)
            upOnPlane = Vector3.ProjectOnPlane(Vector3.up, planeNormal);
        if (upOnPlane.sqrMagnitude < 1e-4f)
            upOnPlane = Vector3.ProjectOnPlane(Vector3.forward, planeNormal);
        upOnPlane.Normalize();

        // Map (objectNormal, objectUp) in object space onto (planeNormal, upOnPlane) in world space
        Quaternion objectFrame = Quaternion.LookRotation(objectNormal, objectUp);
        Quaternion worldFrame = Quaternion.LookRotation(planeNormal, upOnPlane);
        arObject.transform.rotation = worldFrame * Quaternion.Inverse(objectFrame);

        // After rotating, find where the object's anchor currently is
        Vector3 anchorWorld = arObject.transform.TransformPoint(objectAnchor);

        // Translate the object so its anchor coincides with hitPoint
        Vector3 translation = hitPoint - anchorWorld;

        arObject.transform.position += translation;

        // Store registration information for later manipulation
        interactable.currentAnchorPointInWorld = hitPoint;
        interactable.currentNormalDirectionInWorld = planeNormal;
        interactable.currentARPlane = arPlane;
    }

    // Task 3: Object Rotation and Scaling

    public void ObjectRotation(GameObject arObject, float deltaAngle) {
        ARInteractable interactable = arObject.GetComponent<ARInteractable>();

        if (interactable == null)
            return;

        Vector3 anchorPoint = interactable.currentAnchorPointInWorld;

        Vector3 rotationAxis = interactable.currentNormalDirectionInWorld.normalized;

        arObject.transform.RotateAround(
            anchorPoint,
            rotationAxis,
            deltaAngle
        );
    }
    public void ObjectScaling(GameObject arObject, float scaleRate) {
        ARInteractable interactable = arObject.GetComponent<ARInteractable>();

        if (interactable == null)
            return;

        Vector3 anchorPoint = interactable.currentAnchorPointInWorld;

        // Vector from the anchor to the object's current pivot
        Vector3 offsetFromAnchor = arObject.transform.position - anchorPoint;

        // Scale the object
        arObject.transform.localScale *= scaleRate;

        // Scale the pivot's offset by the same amount
        arObject.transform.position = anchorPoint + offsetFromAnchor * scaleRate;
    }
}