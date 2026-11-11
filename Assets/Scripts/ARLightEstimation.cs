using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Photometric registration: reads AR Foundation light estimation from the
/// ARCameraManager every camera frame and applies it to the scene's main
/// directional light and ambient lighting, so lit artwork materials (URP Lit)
/// match the brightness, colour and (where supported) light direction of the
/// real room.
///
/// Attach to the scene's Directional Light. References are found automatically
/// if left empty.
///
/// Platform notes:
///  - ARCore "Basic"  : averageBrightness + colorCorrection
///  - ARCore "HDR"    : main light direction/intensity/colour + spherical harmonics
///  - ARKit (world)   : averageIntensityInLumens + averageColorTemperature
/// </summary>
[RequireComponent(typeof(Light))]
public class ARLightEstimation : MonoBehaviour
{
    public enum EstimationMode
    {
        Auto,              // Android -> EnvironmentalHDR, iOS/other -> Basic
        Basic,             // ambient intensity + ambient colour only
        EnvironmentalHDR   // main light + spherical harmonics (ARCore)
    }

    [Header("References (auto-found if empty)")]
    public ARCameraManager cameraManager;
    public Light mainLight;

    [Header("Estimation")]
    public EstimationMode mode = EstimationMode.Auto;
    [Tooltip("Set false to compare with fixed lighting (for validation screenshots).")]
    public bool estimationEnabled = true;

    [Header("Applying")]
    [Tooltip("Multiplier on the estimated brightness before it is written to the light.")]
    public float intensityMultiplier = 1.0f;
    public float minIntensity = 0.05f;
    public float maxIntensity = 3.0f;
    [Tooltip("Rotate the directional light to the estimated main light direction (HDR only).")]
    public bool applyLightDirection = true;
    [Tooltip("Write ambient colour / spherical harmonics to RenderSettings.")]
    public bool applyAmbient = true;
    [Tooltip("Higher = follows changes faster. 0 = no smoothing.")]
    public float smoothingSpeed = 4.0f;

    [Header("Debug (optional)")]
    public TextMeshProUGUI debugText;

    // Latest estimated values (null = not provided by this device/mode)
    public float? Brightness { get; private set; }
    public float? ColorTemperature { get; private set; }
    public Color? ColorCorrection { get; private set; }
    public Vector3? MainLightDirection { get; private set; }
    public Color? MainLightColor { get; private set; }
    public float? MainLightIntensityLumens { get; private set; }
    public SphericalHarmonicsL2? AmbientSphericalHarmonics { get; private set; }
    public int FramesReceived { get; private set; }

    // Smoothed values actually applied
    float m_AppliedIntensity;
    Color m_AppliedColor = Color.white;
    Quaternion m_TargetRotation;

    // Original lighting, restored when estimation is turned off
    float m_OrigIntensity;
    Color m_OrigColor;
    bool m_OrigUseColorTemperature;
    float m_OrigColorTemperature;
    Quaternion m_OrigRotation;
    AmbientMode m_OrigAmbientMode;
    Color m_OrigAmbientLight;
    SphericalHarmonicsL2 m_OrigAmbientProbe;

    void Awake()
    {
        if (mainLight == null) mainLight = GetComponent<Light>();
        if (cameraManager == null) cameraManager = FindAnyObjectByType<ARCameraManager>();

        m_OrigIntensity = mainLight.intensity;
        m_OrigColor = mainLight.color;
        m_OrigUseColorTemperature = mainLight.useColorTemperature;
        m_OrigColorTemperature = mainLight.colorTemperature;
        m_OrigRotation = mainLight.transform.rotation;
        m_OrigAmbientMode = RenderSettings.ambientMode;
        m_OrigAmbientLight = RenderSettings.ambientLight;
        m_OrigAmbientProbe = RenderSettings.ambientProbe;

        m_AppliedIntensity = m_OrigIntensity;
        m_AppliedColor = m_OrigColor;
        m_TargetRotation = m_OrigRotation;
    }

    void OnEnable()
    {
        if (cameraManager == null)
        {
            Debug.LogWarning("[ARLightEstimation] No ARCameraManager found; light estimation disabled.");
            return;
        }
        cameraManager.requestedLightEstimation = RequestedFeatures();
        cameraManager.frameReceived += OnFrameReceived;
    }

    void OnDisable()
    {
        if (cameraManager != null)
            cameraManager.frameReceived -= OnFrameReceived;
        RestoreOriginalLighting();
    }

    LightEstimation RequestedFeatures()
    {
        EstimationMode m = mode;
        if (m == EstimationMode.Auto)
            m = Application.platform == RuntimePlatform.Android ? EstimationMode.EnvironmentalHDR : EstimationMode.Basic;

        // ARKit world tracking only supports ambient intensity/colour; asking for more
        // can make AR Foundation pick a face-tracking configuration, so keep it basic.
        if (m == EstimationMode.EnvironmentalHDR)
            return LightEstimation.MainLightDirection | LightEstimation.MainLightIntensity |
                   LightEstimation.AmbientSphericalHarmonics;
        return LightEstimation.AmbientIntensity | LightEstimation.AmbientColor;
    }

    void OnFrameReceived(ARCameraFrameEventArgs args)
    {
        ARLightEstimationData le = args.lightEstimation;
        FramesReceived++;

        // Brightness: prefer whichever the device provides
        if (le.averageBrightness.HasValue)
            Brightness = le.averageBrightness.Value;                       // ARCore basic, 0..1
        else if (le.averageIntensityInLumens.HasValue)
            Brightness = le.averageIntensityInLumens.Value / 1000f;        // ARKit, ~1000 = neutral
        else if (le.mainLightIntensityLumens.HasValue)
            Brightness = le.mainLightIntensityLumens.Value / Mathf.PI;     // ARCore HDR
        else
            Brightness = null;

        ColorTemperature = le.averageColorTemperature;
        ColorCorrection = le.colorCorrection;
        MainLightDirection = le.mainLightDirection;
        MainLightColor = le.mainLightColor;
        MainLightIntensityLumens = le.mainLightIntensityLumens;
        AmbientSphericalHarmonics = le.ambientSphericalHarmonics;

        if (estimationEnabled && applyLightDirection && MainLightDirection.HasValue &&
            MainLightDirection.Value.sqrMagnitude > 1e-6f)
        {
            m_TargetRotation = Quaternion.LookRotation(MainLightDirection.Value);
        }

        if (estimationEnabled && applyAmbient)
            ApplyAmbient();
    }

    void ApplyAmbient()
    {
        if (AmbientSphericalHarmonics.HasValue)
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientProbe = AmbientSphericalHarmonics.Value;
        }
        else if (Brightness.HasValue)
        {
            Color tint = ColorCorrection.HasValue ? ColorCorrection.Value : Color.white;
            if (!ColorCorrection.HasValue && ColorTemperature.HasValue)
                tint = Mathf.CorrelatedColorTemperatureToRGB(ColorTemperature.Value);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = tint * Mathf.Clamp01(Brightness.Value);
        }
    }

    void Update()
    {
        if (!estimationEnabled || FramesReceived == 0)
            return;

        float t = smoothingSpeed <= 0f ? 1f : 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);

        if (Brightness.HasValue)
        {
            float target = Mathf.Clamp(Brightness.Value * intensityMultiplier, minIntensity, maxIntensity);
            m_AppliedIntensity = Mathf.Lerp(m_AppliedIntensity, target, t);
            mainLight.intensity = m_AppliedIntensity;
        }

        // Colour: explicit RGB (ARCore) wins over colour temperature (ARKit)
        if (MainLightColor.HasValue || ColorCorrection.HasValue)
        {
            Color target = MainLightColor ?? ColorCorrection.Value;
            target.a = 1f;
            mainLight.useColorTemperature = false;
            m_AppliedColor = Color.Lerp(m_AppliedColor, target, t);
            mainLight.color = m_AppliedColor;
        }
        else if (ColorTemperature.HasValue)
        {
            mainLight.useColorTemperature = true;
            mainLight.color = Color.white;
            mainLight.colorTemperature = Mathf.Lerp(mainLight.colorTemperature, ColorTemperature.Value, t);
        }

        if (applyLightDirection && MainLightDirection.HasValue)
            mainLight.transform.rotation = Quaternion.Slerp(mainLight.transform.rotation, m_TargetRotation, t);

        if (debugText != null)
            debugText.text = DebugString();
    }

    /// <summary>Hook to a UI button to A/B compare estimated vs fixed lighting.</summary>
    public void ToggleEstimation() => SetEstimationEnabled(!estimationEnabled);

    public void SetEstimationEnabled(bool enabledValue)
    {
        estimationEnabled = enabledValue;
        if (!estimationEnabled)
            RestoreOriginalLighting();
        else
        {
            m_AppliedIntensity = mainLight.intensity;
            m_AppliedColor = mainLight.color;
        }
        if (debugText != null)
            debugText.text = DebugString();
    }

    void RestoreOriginalLighting()
    {
        if (mainLight == null) return;
        mainLight.intensity = m_OrigIntensity;
        mainLight.color = m_OrigColor;
        mainLight.useColorTemperature = m_OrigUseColorTemperature;
        mainLight.colorTemperature = m_OrigColorTemperature;
        mainLight.transform.rotation = m_OrigRotation;
        RenderSettings.ambientMode = m_OrigAmbientMode;
        RenderSettings.ambientLight = m_OrigAmbientLight;
        RenderSettings.ambientProbe = m_OrigAmbientProbe;
        m_AppliedIntensity = m_OrigIntensity;
        m_AppliedColor = m_OrigColor;
        m_TargetRotation = m_OrigRotation;
    }

    public string DebugString()
    {
        string F(float? v) => v.HasValue ? v.Value.ToString("0.00") : "-";
        return $"Light estimation: {(estimationEnabled ? "ON" : "OFF")}  ({(cameraManager != null ? cameraManager.currentLightEstimation.ToString() : "no camera")})\n" +
               $"Brightness: {F(Brightness)}  Temp: {F(ColorTemperature)}K\n" +
               $"ColorCorr: {(ColorCorrection.HasValue ? ColorCorrection.Value.ToString("F2") : "-")}\n" +
               $"MainLight dir: {(MainLightDirection.HasValue ? MainLightDirection.Value.ToString("F2") : "-")}  lm: {F(MainLightIntensityLumens)}\n" +
               $"SH: {(AmbientSphericalHarmonics.HasValue ? "yes" : "-")}  Applied I: {mainLight.intensity:0.00}";
    }
}
