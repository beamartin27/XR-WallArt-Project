using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Keeps a placed artwork flush against its wall as the device refines its understanding of the room.
///
/// A wall's position and angle are rough when it is first found and improve over the next seconds.
/// Without this, artwork placed early ends up floating in front of the wall or sunk into it.
/// Only the distance to the wall and the wall's facing are corrected: the artwork never slides
/// sideways or spins, so it stays where the user put it.
///
/// Artwork placed on an estimated surface (before any plane existed) adopts the wall plane
/// once one is detected there.
/// </summary>
[RequireComponent(typeof(ARInteractable))]
public class WallAttachment : MonoBehaviour
{
    const float k_Interval = 0.2f;
    const float k_MinDistance = 0.003f;
    const float k_MinAngle = 0.3f;
    // A bigger change than this means the plane was merged into something else: don't follow it.
    const float k_MaxDistance = 0.25f;
    const float k_MaxAngle = 20f;

    ARInteractable m_Interactable;
    float m_NextCheck;

    void Awake()
    {
        m_Interactable = GetComponent<ARInteractable>();
    }

    void LateUpdate()
    {
        if (Time.unscaledTime < m_NextCheck) return;
        m_NextCheck = Time.unscaledTime + k_Interval;

        if (IsUserManipulating()) return;

        ARPlane plane = ResolvePlane();
        if (plane == null) return;

        Vector3 wallNormal = plane.transform.up.normalized;
        Vector3 anchor = m_Interactable.currentAnchorPointInWorld;
        Vector3 currentNormal = m_Interactable.currentNormalDirectionInWorld;
        if (currentNormal.sqrMagnitude < 1e-6f) return;

        float distance = Vector3.Dot(anchor - plane.transform.position, wallNormal);
        float angle = Vector3.Angle(currentNormal, wallNormal);

        if (Mathf.Abs(distance) < k_MinDistance && angle < k_MinAngle) return;
        if (Mathf.Abs(distance) > k_MaxDistance || angle > k_MaxAngle) return;

        Quaternion turn = Quaternion.FromToRotation(currentNormal, wallNormal);
        Vector3 newAnchor = anchor - wallNormal * distance;
        Vector3 pivotOffset = transform.position - anchor;

        transform.rotation = turn * transform.rotation;
        transform.position = newAnchor + turn * pivotOffset;

        m_Interactable.currentAnchorPointInWorld = newAnchor;
        m_Interactable.currentNormalDirectionInWorld = wallNormal;
    }

    bool IsUserManipulating()
    {
        if (TouchScreenInputManager.Instance != null &&
            (TouchScreenInputManager.Instance.touchCount > 0 || TouchScreenInputManager.Instance.isDoingDrag))
            return true;
        if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.isDragging)
            return true;
        if (ArtworkSizeManager.Instance != null && ArtworkSizeManager.Instance.IsSnapping)
            return true;
        return false;
    }

    ARPlane ResolvePlane()
    {
        ARPlane plane = m_Interactable.currentARPlane != null
            ? m_Interactable.currentARPlane.GetComponent<ARPlane>()
            : null;

        // Follow planes that were merged into a larger one
        int guard = 0;
        while (plane != null && plane.subsumedBy != null && guard++ < 8)
            plane = plane.subsumedBy;

        if (!WallDetection.IsWall(plane))
        {
            plane = WallDetection.FindCoplanarWall(
                m_Interactable.currentAnchorPointInWorld,
                m_Interactable.currentNormalDirectionInWorld);
        }

        if (plane != null && m_Interactable.currentARPlane != plane.gameObject)
            m_Interactable.currentARPlane = plane.gameObject;

        return plane;
    }
}
