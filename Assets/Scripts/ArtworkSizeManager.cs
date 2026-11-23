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
/// Manages realistic artwork dimensions across both standard ISO A-series (A5, A4, A3, A2, A1)
/// and non-standard/general aspect ratios (Small, Medium, Big), preserving aspect ratio:
/// - A5: 148mm x 210mm (0.5000x scale relative to A3)
/// - A4: 210mm x 297mm (0.7071x scale relative to A3)
/// - A3: 297mm x 420mm (1.0000x baseline)
/// - A2: 420mm x 594mm (1.4142x scale relative to A3)
/// - A1: 594mm x 841mm (2.0000x scale relative to A3)
/// - General Small: 0.7071x scale
/// - General Medium: 1.0000x baseline
/// - General Big: 1.4142x scale
/// Maintains the artwork anchor flush on the vertical wall when resizing.
/// Provides preset orientation switching (Portrait / Landscape) respecting natural aspect ratio without angle snapping on manual rotation.
/// Supports smooth magnetic snapping to nearest preset on manual scale release.
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

    public const float SCALE_SMALL = 0.7071f;
    public const float SCALE_MEDIUM = 1.0000f;
    public const float SCALE_BIG = 1.4142f;

    public event Action<ArtworkSizePreset> OnPresetChanged;
    public event Action<GeneralSizePreset> OnGeneralPresetChanged;
    public event Action<ArtworkOrientation> OnOrientationChanged;

    public ArtworkOrientation defaultOrientation = ArtworkOrientation.Portrait;

    private Coroutine activeSnapCoroutine = null;

    /// <summary>True while an artwork is animating to its snapped size.</summary>
    public bool IsSnapping => activeSnapCoroutine != null;

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

    public float GetScaleMultiplier(GeneralSizePreset preset)
    {
        return preset switch
        {
            GeneralSizePreset.Small => SCALE_SMALL,
            GeneralSizePreset.Medium => SCALE_MEDIUM,
            GeneralSizePreset.Big => SCALE_BIG,
            _ => 1.0f
        };
    }

    public ArtworkSizePreset GetNearestPreset(float currentScale)
    {
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

    public GeneralSizePreset GetNearestGeneralPreset(float currentScale)
    {
        if (currentScale < 0.8536f)
        {
            return GeneralSizePreset.Small;
        }
        else if (currentScale < 1.2071f)
        {
            return GeneralSizePreset.Medium;
        }
        else
        {
            return GeneralSizePreset.Big;
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

    public void ApplyGeneralPreset(GeneralSizePreset preset)
    {
        if (ScreenRayInteractor.Instance == null || ScreenRayInteractor.Instance.currentSelectedObject == null)
        {
            Debug.LogWarning("ArtworkSizeManager: No artwork currently selected to resize.");
            return;
        }

        ARInteractable selected = ScreenRayInteractor.Instance.currentSelectedObject;
        ApplyGeneralPresetToObject(selected.gameObject, preset);
        OnGeneralPresetChanged?.Invoke(preset);
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

        Vector3 offsetFromAnchor = arObject.transform.position - anchorPoint;
        arObject.transform.localScale = Vector3.one * targetScale;
        arObject.transform.position = anchorPoint + offsetFromAnchor * scaleRatio;

        ArtworkProperties props = arObject.GetComponent<ArtworkProperties>();
        if (props != null)
        {
            props.currentPreset = preset;
        }

        OnPresetChanged?.Invoke(preset);
    }

    public void ApplyGeneralPresetToObject(GameObject arObject, GeneralSizePreset preset)
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

        Vector3 offsetFromAnchor = arObject.transform.position - anchorPoint;
        arObject.transform.localScale = Vector3.one * targetScale;
        arObject.transform.position = anchorPoint + offsetFromAnchor * scaleRatio;

        ArtworkProperties props = arObject.GetComponent<ArtworkProperties>();
        if (props != null)
        {
            props.currentGeneralPreset = preset;
        }

        OnGeneralPresetChanged?.Invoke(preset);
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
    /// Rotates the specified artwork on the wall to match Portrait (tall) or Landscape (wide).
    /// Respects whether the original source image is wide or tall.
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

        ArtworkProperties props = arObject.GetComponent<ArtworkProperties>();
        bool isNaturalLandscape = props != null && props.IsNaturalLandscape;

        // If an image is naturally wide (landscape photo), upright is its natural Landscape orientation;
        // rotating it 90 deg makes its longer dimension vertical (Portrait).
        // If an image is naturally tall (portrait artwork), upright is its natural Portrait orientation;
        // rotating it 90 deg makes its longer dimension horizontal (Landscape).
        Vector3 targetUp;
        if (isNaturalLandscape)
        {
            targetUp = (orientation == ArtworkOrientation.Portrait) ? wallRight : wallUp;
        }
        else
        {
            targetUp = (orientation == ArtworkOrientation.Portrait) ? wallUp : wallRight;
        }

        Vector3 currentUp = Vector3.ProjectOnPlane(arObject.transform.up, planeNormal).normalized;
        float deltaAngle = Vector3.SignedAngle(currentUp, targetUp, planeNormal);
        AssignmentScript.Instance.ObjectRotation(arObject, deltaAngle);

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

    public void CancelActiveSnap()
    {
        if (activeSnapCoroutine != null)
        {
            StopCoroutine(activeSnapCoroutine);
            activeSnapCoroutine = null;
        }
    }

    /// <summary>
    /// Snaps the object to the nearest size preset (A-series for standard posters, Small/Med/Big for general).
    /// </summary>
    public void SnapToNearestPreset(GameObject arObject, bool smooth = true)
    {
        if (arObject == null) return;
        ARInteractable interactable = arObject.GetComponent<ARInteractable>();
        if (interactable == null) return;

        ArtworkProperties props = arObject.GetComponent<ArtworkProperties>();
        bool isGeneral = props != null && props.ActiveSizingSystem == SizingSystem.GeneralScale;

        float currentScale = arObject.transform.localScale.x;

        if (isGeneral)
        {
            GeneralSizePreset nearest = GetNearestGeneralPreset(currentScale);
            float targetScale = GetScaleMultiplier(nearest);
            if (smooth)
            {
                CancelActiveSnap();
                activeSnapCoroutine = StartCoroutine(SmoothScaleGeneralRoutine(interactable, targetScale, nearest, 0.18f));
            }
            else
            {
                ApplyGeneralPresetToObject(arObject, nearest);
            }
        }
        else
        {
            ArtworkSizePreset nearest = GetNearestPreset(currentScale);
            float targetScale = GetScaleMultiplier(nearest);
            if (smooth)
            {
                CancelActiveSnap();
                activeSnapCoroutine = StartCoroutine(SmoothScaleRoutine(interactable, targetScale, nearest, 0.18f));
            }
            else
            {
                ApplyPresetToObject(arObject, nearest);
            }
        }
    }

    public void StepSize(GameObject arObject, int direction)
    {
        if (arObject == null) return;
        ArtworkProperties props = arObject.GetComponent<ArtworkProperties>();
        if (props != null && props.ActiveSizingSystem == SizingSystem.GeneralScale)
        {
            int newIndex = Mathf.Clamp((int)props.currentGeneralPreset + direction, 0, 2);
            ApplyGeneralPreset((GeneralSizePreset)newIndex);
        }
        else
        {
            ArtworkSizePreset current = GetCurrentPreset();
            int newIndex = Mathf.Clamp((int)current + direction, 0, 4);
            ApplyPreset((ArtworkSizePreset)newIndex);
        }
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

    private IEnumerator SmoothScaleGeneralRoutine(ARInteractable interactable, float targetScale, GeneralSizePreset targetPreset, float duration)
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
                props.currentGeneralPreset = targetPreset;
            }
            OnGeneralPresetChanged?.Invoke(targetPreset);
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

    public GeneralSizePreset GetCurrentGeneralPreset()
    {
        if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null)
        {
            ArtworkProperties props = ScreenRayInteractor.Instance.currentSelectedObject.GetComponent<ArtworkProperties>();
            if (props != null)
            {
                return props.currentGeneralPreset;
            }

            float x = ScreenRayInteractor.Instance.currentSelectedObject.transform.localScale.x;
            return GetNearestGeneralPreset(x);
        }
        return GeneralSizePreset.Medium;
    }
}
