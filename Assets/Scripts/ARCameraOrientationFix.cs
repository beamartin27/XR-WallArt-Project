using UnityEngine;
using UnityEngine.InputSystem;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

/// <summary>
/// Workaround for the AR camera pose arriving rotated by a multiple of 90° around the view axis.
///
/// Observed on iPhone 17 Pro (ARKit XR Plugin 6.3.5, app locked to portrait): the rotation from
/// /ARKit/devicerotation is missing the portrait correction, so the virtual camera is rolled ~90°
/// relative to the camera image. Side walls then render on the ceiling/floor, content swims when
/// the phone moves, and "upright" artwork looks turned 90°.
///
/// How it works: while the phone is held upright (checked with the accelerometer, which does not
/// depend on the AR pose), the camera's roll relative to the AR world's "up" should be ~0°.
/// Any consistent offset is snapped to the nearest 90° and applied to the camera every time the
/// TrackedPoseDriver writes a new pose. Devices without the problem (Android, XR Simulation)
/// measure ~0° and get no correction.
///
/// Added automatically to the AR camera at startup.
/// </summary>
public class ARCameraOrientationFix : MonoBehaviour
{
    /// <summary>Correction currently applied, in degrees around the camera's view axis.</summary>
    public static int CorrectionDegrees { get; private set; }
    /// <summary>True once the correction has been measured and locked in.</summary>
    public static bool IsLocked { get; private set; }

    const int k_SamplesToLock = 15;

    int m_CandidateSteps;
    int m_CandidateCount;
    Quaternion m_LastWrittenRotation;
    bool m_HasWritten;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        foreach (var tpd in FindObjectsByType<TrackedPoseDriver>(FindObjectsSortMode.None))
        {
            if (tpd.GetComponent<Camera>() != null && tpd.GetComponent<ARCameraOrientationFix>() == null)
                tpd.gameObject.AddComponent<ARCameraOrientationFix>();
        }
    }

    void OnEnable()
    {
        CorrectionDegrees = 0;
        IsLocked = false;
        m_CandidateCount = 0;
        m_HasWritten = false;

        if (Accelerometer.current != null && !Accelerometer.current.enabled)
            InputSystem.EnableDevice(Accelerometer.current);

        // Subscribed after the TrackedPoseDriver (which subscribes in its own OnEnable at scene load),
        // so this runs right after it writes the pose, for both normal and before-render updates.
        InputSystem.onAfterUpdate += OnAfterInputUpdate;
    }

    void OnDisable()
    {
        InputSystem.onAfterUpdate -= OnAfterInputUpdate;
    }

    void OnAfterInputUpdate()
    {
        Quaternion raw = transform.localRotation;

        // Skip if the TrackedPoseDriver has not written a new pose since our last correction
        if (m_HasWritten && Quaternion.Angle(raw, m_LastWrittenRotation) < 0.01f)
            return;

        if (!IsLocked)
            Measure(raw);

        if (CorrectionDegrees != 0)
        {
            Quaternion corrected = raw * Quaternion.AngleAxis(CorrectionDegrees, Vector3.forward);
            transform.localRotation = corrected;
            m_LastWrittenRotation = corrected;
            m_HasWritten = true;
        }
    }

    void Measure(Quaternion raw)
    {
        var accel = Accelerometer.current;
        if (accel == null || !accel.enabled)
            return;

        // Gravity in device coordinates: an upright portrait phone reads roughly (0, -1, 0)
        Vector3 g = accel.acceleration.ReadValue();
        bool phoneUpright = g.y < -0.75f && Mathf.Abs(g.x) < 0.3f;
        if (!phoneUpright)
            return;

        // Rotation of the parent (Camera Offset / XR Origin) so the check works in world space
        Quaternion world = transform.parent != null ? transform.parent.rotation * raw : raw;
        Vector3 forward = world * Vector3.forward;
        Vector3 up = world * Vector3.up;

        // The roll is only meaningful when the camera is not pointing straight up or down
        float pitch = 90f - Vector3.Angle(Vector3.up, forward);
        if (Mathf.Abs(pitch) > 50f)
            return;

        Vector3 upRef = Vector3.ProjectOnPlane(Vector3.up, forward);
        float roll = Vector3.SignedAngle(upRef, up, forward);
        int steps = Mathf.RoundToInt(-roll / 90f);
        // Only trust readings that are close to a clean multiple of 90°
        if (Mathf.Abs(-roll - steps * 90f) > 25f)
            return;

        if (steps == m_CandidateSteps)
            m_CandidateCount++;
        else
        {
            m_CandidateSteps = steps;
            m_CandidateCount = 1;
        }

        if (m_CandidateCount >= k_SamplesToLock)
        {
            CorrectionDegrees = ((steps % 4) + 4) % 4 * 90;
            if (CorrectionDegrees > 180) CorrectionDegrees -= 360;
            IsLocked = true;
            Debug.Log($"[ARCameraOrientationFix] Camera roll offset detected; applying {CorrectionDegrees}° correction.");
        }
    }
}
