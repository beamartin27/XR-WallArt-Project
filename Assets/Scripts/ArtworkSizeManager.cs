using System;
using System.Collections;
using UnityEngine;

public enum ArtworkSizePreset
{
    A5 = 0,
    A4 = 1,
    A3 = 2,
    A2 = 3,
    A1 = 4
}

/// <summary>
/// Manages realistic ISO standard artwork dimensions (A5, A4, A3, A2, A1) and orientation (Portrait, Landscape):
/// - A5: 148mm x 210mm (0.5000x scale relative to A3)
/// - A4: 210mm x 297mm (0.7071x scale relative to A3)
/// - A3: 297mm x 420mm (1.0000x baseline)
/// - A2: 420mm x 594mm (1.4142x scale relative to A3)
/// - A1: 594mm x 841mm (2.0000x scale relative to A3)
/// Maintains the artwork anchor flush on the vertical wall when resizing.
/// Provides preset orientation switching (Portrait / Landscape) without angle snapping on manual rotation.
/// Supports smooth magnetic snapping to nearest A-size on manual scale release.
/// </summary>
public class ArtworkSizeManager : MonoBehaviour
{
    private static ArtworkSizeManager _instance;
    public static ArtworkSizeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ArtworkSizeManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ArtworkSizeManager");
                    _instance = go.AddComponent<ArtworkSizeManager>();
                }
            }
            return _instance;
        }
    }

    public const float SCALE_A5 = 0.5000f;
    public const float SCALE_A4 = 0.7071f;
    public const float SCALE_A3 = 1.0000f;
    public const float SCALE_A2 = 1.4142f;
    public const float SCALE_A1 = 2.0000f;

    public event Action<ArtworkSizePreset> OnPresetChanged;
    public event Action<ArtworkOrientation> OnOrientationChanged;

    public ArtworkOrientation defaultOrientation = ArtworkOrientation.Portrait;

    private Coroutine activeSnapCoroutine = null;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public float GetScaleMultiplier(ArtworkSizePreset preset)
    {
        return preset switch
        {
            ArtworkSizePreset.A5 => SCALE_A5,
            ArtworkSizePreset.A4 => SCALE_A4,
            ArtworkSizePreset.A3 => SCALE_A3,
            ArtworkSizePreset.A2 => SCALE_A2,
            ArtworkSizePreset.A1 => SCALE_A1,
            _ => 1.0f
        };
    }

    public ArtworkSizePreset GetNearestPreset(float currentScale)
    {
        // Midpoint between A5 (0.5000) and A4 (0.7071) is ~0.6035
        // Midpoint between A4 (0.7071) and A3 (1.0000) is ~0.8536
        // Midpoint between A3 (1.0000) and A2 (1.4142) is ~1.2071
        // Midpoint between A2 (1.4142) and A1 (2.0000) is ~1.7071
        if (currentScale < 0.6035f)
        {
            return ArtworkSizePreset.A5;
        }
        else if (currentScale < 0.8536f)
        {
            return ArtworkSizePreset.A4;
        }
        else if (currentScale < 1.2071f)
        {
            return ArtworkSizePreset.A3;
        }
        else if (currentScale < 1.7071f)
        {
            return ArtworkSizePreset.A2;
        }
        else
        {
            return ArtworkSizePreset.A1;
        }
    }

    public void ApplyPreset(ArtworkSizePreset preset)
    {
        if (ScreenRayInteractor.Instance == null || ScreenRayInteractor.Instance.currentSelectedObject == null)
        {
            Debug.LogWarning("ArtworkSizeManager: No artwork currently selected to resize.");
            return;
        }

        ARInteractable selected = ScreenRayInteractor.Instance.currentSelectedObject;
        ApplyPresetToObject(selected.gameObject, preset);
        OnPresetChanged?.Invoke(preset);
    }

    public void ApplyPresetToObject(GameObject arObject, ArtworkSizePreset preset)
    {
        if (arObject == null) return;
        ARInteractable interactable = arObject.GetComponent<ARInteractable>();
        if (interactable == null) return;

        if (activeSnapCoroutine != null)
        {
            StopCoroutine(activeSnapCoroutine);
            activeSnapCoroutine = null;
        }

        float targetScale = GetScaleMultiplier(preset);
        float currentScale = arObject.transform.localScale.x;
        if (Mathf.Approximately(currentScale, 0f)) currentScale = 1f;

        float scaleRatio = targetScale / currentScale;
        Vector3 anchorPoint = interactable.currentAnchorPointInWorld;

        // Offset from anchor to current pivot
        Vector3 offsetFromAnchor = arObject.transform.position - anchorPoint;

        // Uniform scaling preserves aspect ratio
        arObject.transform.localScale = Vector3.one * targetScale;

        // Reposition so the anchor remains locked to the wall
        arObject.transform.position = anchorPoint + offsetFromAnchor * scaleRatio;

        ArtworkProperties props = arObject.GetComponent<ArtworkProperties>();
        if (props != null)
        {
            props.currentPreset = preset;
        }

        OnPresetChanged?.Invoke(preset);
    }

    /// <summary>
    /// Sets orientation (Portrait or Landscape) for the selected artwork.
    /// Does NOT introduce any snapping on manual rotation gestures.
    /// </summary>
    public void SetOrientation(ArtworkOrientation orientation)
    {
        defaultOrientation = orientation;
        if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
        {
            SetOrientationForObject(ScreenRayInteractor.Instance.currentSelectedObject.gameObject, orientation);
        }
        OnOrientationChanged?.Invoke(orientation);
    }

    /// <summary>
    /// Rotates the specified artwork on the wall to match Portrait (vertical) or Landscape (horizontal).
    /// Preserves wall registration and anchor positioning.
    /// </summary>
    public void SetOrientationForObject(GameObject arObject, ArtworkOrientation orientation)
    {
        if (arObject == null) return;
        ARInteractable interactable = arObject.GetComponent<ARInteractable>();
        if (interactable == null) return;

        Vector3 planeNormal = interactable.currentNormalDirectionInWorld;
        if (planeNormal.sqrMagnitude < 1e-4f)
        {
            planeNormal = arObject.transform.forward;
        }
        planeNormal.Normalize();

        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, planeNormal);
        if (wallUp.sqrMagnitude < 1e-4f)
        {
            wallUp = Vector3.ProjectOnPlane(Vector3.forward, planeNormal);
        }
        wallUp.Normalize();

        Vector3 wallRight = Vector3.Cross(planeNormal, wallUp).normalized;

        Vector3 currentUp = Vector3.ProjectOnPlane(arObject.transform.up, planeNormal).normalized;
        Vector3 targetUp = (orientation == ArtworkOrientation.Portrait) ? wallUp : wallRight;

        float deltaAngle = Vector3.SignedAngle(currentUp, targetUp, planeNormal);
        AssignmentScript.Instance.ObjectRotation(arObject, deltaAngle);

        ArtworkProperties props = arObject.GetComponent<ArtworkProperties>();
        if (props != null)
        {
            props.currentOrientation = orientation;
        }

        OnOrientationChanged?.Invoke(orientation);
    }

    public ArtworkOrientation GetCurrentOrientation()
    {
        if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
        {
            ArtworkProperties props = ScreenRayInteractor.Instance.currentSelectedObject.GetComponent<ArtworkProperties>();
            if (props != null)
            {
                return props.currentOrientation;
            }
        }
        return defaultOrientation;
    }

    /// <summary>
    /// Cancels any in-progress smooth scaling snap.
    /// </summary>
    public void CancelActiveSnap()
    {
        if (activeSnapCoroutine != null)
        {
            StopCoroutine(activeSnapCoroutine);
            activeSnapCoroutine = null;
        }
    }

    /// <summary>
    /// Snaps the object to the nearest A-size preset (A5, A4, A3, A2, or A1).
    /// </summary>
    public void SnapToNearestPreset(GameObject arObject, bool smooth = true)
    {
        if (arObject == null) return;
        ARInteractable interactable = arObject.GetComponent<ARInteractable>();
        if (interactable == null) return;

        float currentScale = arObject.transform.localScale.x;
        ArtworkSizePreset nearestPreset = GetNearestPreset(currentScale);
        float targetScale = GetScaleMultiplier(nearestPreset);

        if (smooth)
        {
            CancelActiveSnap();
            activeSnapCoroutine = StartCoroutine(SmoothScaleRoutine(interactable, targetScale, nearestPreset, 0.18f));
        }
        else
        {
            ApplyPresetToObject(arObject, nearestPreset);
        }
    }

    /// <summary>
    /// Steps up (+1) or down (-1) between A-size presets.
    /// </summary>
    public void StepSize(GameObject arObject, int direction)
    {
        if (arObject == null) return;
        ArtworkSizePreset current = GetCurrentPreset();
        int newIndex = Mathf.Clamp((int)current + direction, 0, 4);
        ArtworkSizePreset targetPreset = (ArtworkSizePreset)newIndex;
        ApplyPreset(targetPreset);
    }

    private IEnumerator SmoothScaleRoutine(ARInteractable interactable, float targetScale, ArtworkSizePreset targetPreset, float duration)
    {
        if (interactable == null) yield break;
        GameObject arObject = interactable.gameObject;

        float startScale = arObject.transform.localScale.x;
        if (Mathf.Approximately(startScale, 0f)) startScale = 1f;

        Vector3 anchorPoint = interactable.currentAnchorPointInWorld;
        Vector3 initialOffset = arObject.transform.position - anchorPoint;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (arObject == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.Sin(t * Mathf.PI * 0.5f);

            float currentTarget = Mathf.Lerp(startScale, targetScale, t);
            float scaleRatio = currentTarget / startScale;

            arObject.transform.localScale = Vector3.one * currentTarget;
            arObject.transform.position = anchorPoint + initialOffset * scaleRatio;

            yield return null;
        }

        if (arObject != null)
        {
            float scaleRatio = targetScale / startScale;
            arObject.transform.localScale = Vector3.one * targetScale;
            arObject.transform.position = anchorPoint + initialOffset * scaleRatio;

            ArtworkProperties props = arObject.GetComponent<ArtworkProperties>();
            if (props != null)
            {
                props.currentPreset = targetPreset;
            }
            OnPresetChanged?.Invoke(targetPreset);
        }

        activeSnapCoroutine = null;
    }

    public ArtworkSizePreset GetCurrentPreset()
    {
        if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
        {
            ArtworkProperties props = ScreenRayInteractor.Instance.currentSelectedObject.GetComponent<ArtworkProperties>();
            if (props != null)
            {
                return props.currentPreset;
            }

            float x = ScreenRayInteractor.Instance.currentSelectedObject.transform.localScale.x;
            return GetNearestPreset(x);
        }
        return ArtworkSizePreset.A3;
    }
}
