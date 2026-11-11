using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

/// <summary>
/// On-screen AR diagnostics panel (tap to collapse/expand). Shows whether the virtual camera's
/// orientation matches the real phone (camera roll vs world up), the state of
/// <see cref="ARCameraOrientationFix"/>, which input device drives the camera pose,
/// and what planes have been detected. Useful as evidence when validating tracking.
/// Created automatically at startup.
/// </summary>
public class ARDebugHUD : MonoBehaviour
{
    readonly StringBuilder m_Text = new StringBuilder();
    GUIStyle m_Style;
    float m_NextUpdate;
    bool m_Visible = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("ARDebugHUD");
        DontDestroyOnLoad(go);
        go.AddComponent<ARDebugHUD>();
    }

    void Update()
    {
        if (Time.unscaledTime < m_NextUpdate) return;
        m_NextUpdate = Time.unscaledTime + 0.25f;
        Rebuild();
    }

    void Rebuild()
    {
        m_Text.Clear();
        Camera cam = Camera.main;
        m_Text.AppendLine($"Screen: {Screen.orientation} {Screen.width}x{Screen.height}");

        if (cam != null)
        {
            Transform t = cam.transform;
            // Roll of the virtual camera relative to the AR world's "up".
            // Hold the phone upright (portrait) facing a wall: this should read close to 0.
            Vector3 upRef = Vector3.ProjectOnPlane(Vector3.up, t.forward);
            float roll = upRef.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(upRef, t.up, t.forward) : float.NaN;
            float pitch = 90f - Vector3.Angle(Vector3.up, t.forward);
            m_Text.AppendLine($"Cam pos: {t.position.ToString("F2")}");
            m_Text.AppendLine($"Cam ROLL vs world up: {roll:0}°   pitch: {pitch:0}°");
            var accel = Accelerometer.current;
            float measured = ARCameraOrientationFix.LastMeasuredRoll;
            m_Text.AppendLine($"Orientation fix: {ARCameraOrientationFix.CorrectionDegrees}° " +
                              $"({(ARCameraOrientationFix.IsConfirmed ? "confirmed" : "measuring - hold phone upright")})" +
                              $"  raw roll: {(float.IsNaN(measured) ? "-" : measured.ToString("0") + "°")}");
            m_Text.AppendLine($"AR session: {ARSession.state}  accel: {(accel != null && accel.enabled ? accel.acceleration.ReadValue().ToString("F2") : "n/a")}");

            var tpd = cam.GetComponent<TrackedPoseDriver>();
            if (tpd != null)
            {
                var rotAction = tpd.rotationInput.action;
                if (rotAction != null)
                {
                    m_Text.Append("Rot controls: ");
                    foreach (var c in rotAction.controls) m_Text.Append(c.path).Append("  ");
                    m_Text.AppendLine();
                    m_Text.AppendLine($"Rot active: {(rotAction.activeControl != null ? rotAction.activeControl.path : "-")}  enabled: {rotAction.enabled}");
                }
            }
        }

        m_Text.Append("Devices: ");
        foreach (var d in InputSystem.devices)
            if (d.layout.Contains("AR") || d.layout.Contains("XR") || d.layout.Contains("HMD") || d.layout.Contains("Handheld"))
                m_Text.Append(d.layout).Append("  ");
        m_Text.AppendLine();

        int vertical = 0, horizontal = 0, other = 0;
        float worstWallTilt = 0f;
        foreach (var p in FindObjectsByType<ARPlane>(FindObjectsSortMode.None))
        {
            if (p.alignment == PlaneAlignment.Vertical)
            {
                vertical++;
                // A real wall's normal should be ~90° from world up
                float tilt = Mathf.Abs(90f - Vector3.Angle(p.transform.up, Vector3.up));
                worstWallTilt = Mathf.Max(worstWallTilt, tilt);
            }
            else if (p.alignment == PlaneAlignment.HorizontalUp || p.alignment == PlaneAlignment.HorizontalDown) horizontal++;
            else other++;
        }
        m_Text.AppendLine($"Planes  vertical: {vertical}  horizontal: {horizontal}  other: {other}");
        m_Text.AppendLine($"Wall normals off-horizontal (max): {worstWallTilt:0}°");
        m_Text.Append("(tap here to hide)");
    }

    void OnGUI()
    {
        if (m_Style == null)
        {
            m_Style = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.RoundToInt(Screen.height / 70f),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            m_Style.normal.textColor = Color.yellow;
        }
        float w = Screen.width * 0.94f;
        var rect = new Rect(Screen.width * 0.03f, Screen.height * 0.06f, w, m_Visible ? Screen.height * 0.24f : Screen.height * 0.03f);
        if (GUI.Button(rect, m_Visible ? m_Text.ToString() : "AR debug (tap to show)", m_Style))
            m_Visible = !m_Visible;
    }
}
