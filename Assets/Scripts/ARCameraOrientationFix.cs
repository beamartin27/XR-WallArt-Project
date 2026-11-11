using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
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
/// depend on the AR pose) and ARKit is tracking, the camera's roll relative to the AR world's
/// "up" should be ~0°. Any consistent offset is snapped to the nearest 90° and applied to the
/// camera every time the TrackedPoseDriver writes a new pose. Devices without the problem
/// (Android, XR Simulation) measure ~0° and get no correction.
///
/// The measurement keeps running for the whole session: a new offset only replaces the current
/// one after it has been seen consistently, so a bad early reading (e.g. the placeholder pose
/// before tracking starts) cannot lock in a wrong correction.
///
/// Added automatically to the AR camera at startup.
/// </summary>
public class ARCameraOrientationFix : MonoBehaviour
{
    /// <summary>Correction currently applied, in degrees around the camera's view axis.</summary>
    public static int CorrectionDegrees { get; private set; }
    /// <summary>True once the current correction has been confirmed by consistent measurements.</summary>
    public static bool IsConfirmed { get; private set; }
    /// <summary>Most recent measured roll offset (degrees), or NaN if the last frame could not be measured.</summary>
    public static float LastMeasuredRoll { get; private set; } = float.NaN;

    // Consecutive frames that must agree before a correction is confirmed or changed
    const int k_FramesToConfirm = 20;
    // A reading must be within this many degrees of a multiple of 90° to count
    const float k_SnapTolerance = 25f;

    int m_CurrentSteps;        // applied correction in quarter turns (0..3)
    int m_CandidateSteps = -1; // a different correction being considered
    int m_CandidateFrames;
    int m_ConfirmFrames;
    int m_LastMeasuredFrame = -1;

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
        IsConfirmed = false;
        LastMeasuredRoll = float.NaN;
        m_CurrentSteps = 0;
        m_CandidateSteps = -1;
        m_CandidateFrames = 0;
        m_ConfirmFrames = 0;
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

        // At most one measurement per rendered frame (onAfterUpdate can fire several times)
        if (Time.frameCount != m_LastMeasuredFrame)
        {
            m_LastMeasuredFrame = Time.frameCount;
            Measure(raw);
        }

        if (CorrectionDegrees != 0)
        {
            Quaternion corrected = raw * Quaternion.AngleAxis(CorrectionDegrees, Vector3.forward);
            transform.localRotation = corrected;
            m_LastWrittenRotation = corrected;
            m_HasWritten = true;
        }
        else
        {
            m_HasWritten = false;
        }
    }

    void Measure(Quaternion raw)
    {
        LastMeasuredRoll = float.NaN;

        // Only trust the pose once ARKit/ARCore is actually tracking. Before that the camera sits at
        // its default rotation, which would read as "no offset".
        if (ARSession.state != ARSessionState.SessionTracking)
            return;
        if (Quaternion.Angle(raw, Quaternion.identity) < 0.5f)
            return;

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
        LastMeasuredRoll = roll;

        int steps = Mathf.RoundToInt(-roll / 90f);
        if (Mathf.Abs(-roll - steps * 90f) > k_SnapTolerance)
            return;
        steps = ((steps % 4) + 4) % 4;

        if (steps == m_CurrentSteps)
        {
            // Current correction agrees with what we measure
            m_CandidateSteps = -1;
            m_CandidateFrames = 0;
            if (!IsConfirmed && ++m_ConfirmFrames >= k_FramesToConfirm)
                IsConfirmed = true;
            return;
        }

        // A different correction is measured: switch only after it is seen consistently
        m_ConfirmFrames = 0;
        if (steps == m_CandidateSteps)
            m_CandidateFrames++;
        else
        {
            m_CandidateSteps = steps;
            m_CandidateFrames = 1;
        }

        if (m_CandidateFrames >= k_FramesToConfirm)
        {
            m_CurrentSteps = steps;
            CorrectionDegrees = steps == 3 ? -90 : steps * 90;
            IsConfirmed = true;
            m_CandidateSteps = -1;
            m_CandidateFrames = 0;
            Debug.Log($"[ARCameraOrientationFix] Camera roll offset detected; applying {CorrectionDegrees}° correction.");
        }
    }
}
