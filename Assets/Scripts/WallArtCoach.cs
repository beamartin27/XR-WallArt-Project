using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// The hint at the top of the screen. It always tells the user the one thing to do next -
/// scan a wall, pick a picture, drop it, edit it - and shows short confirmations ("Photo added").
/// It also tints the detected walls while the user is placing or moving a picture, so they can
/// see where the app has recognised a wall, and hides them again afterwards.
/// </summary>
public class WallArtCoach : MonoBehaviour
{
    public static WallArtCoach Instance { get; private set; }

    const float k_PlaneAlphaVisible = 0.12f; // subtle: translucent overlays read stronger in linear colour space
    static readonly Color k_PlaneTint = new Color(0.35f, 0.9f, 0.65f);
    static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

    RectTransform m_Pill;
    Image m_PillImage;
    TextMeshProUGUI m_Label;
    CanvasGroup m_Group;
    WallArtUIController m_Controller;

    string m_Message = "";
    string m_Toast;
    float m_ToastUntil;
    bool m_ToastIsWarning;
    float m_NextEvaluate;

    bool m_WallInView;
    bool m_EverFoundWall;
    int m_PlacedCount;
    bool m_EditTipShown;
    float m_EditTipUntil;

    Material m_PlaneMaterial;
    Color m_PlaneBaseColor = Color.white;
    float m_PlaneAlphaHidden = 0.02f;
    float m_PlaneAlpha;

    public static WallArtCoach Create(Canvas canvas, WallArtUIController controller)
    {
        if (Instance != null) return Instance;
        RectTransform root = WallArtUI.CreateRect("Coach", canvas.transform);
        var coach = root.gameObject.AddComponent<WallArtCoach>();
        coach.m_Controller = controller;
        coach.Build(root);
        Instance = coach;
        return coach;
    }

    void Build(RectTransform root)
    {
        // Full-width strip at the top; the pill is centred inside and sizes itself to the text
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(0f, 90f);
        // Below the notch / Dynamic Island, and below the small "AR debug" chip in the corner
        root.anchoredPosition = new Vector2(0f, -(WallArtUI.TopInset + 76f));

        m_Group = root.gameObject.AddComponent<CanvasGroup>();
        m_Group.blocksRaycasts = false;
        m_Group.interactable = false;
        m_Group.alpha = 0f;

        m_PillImage = WallArtUI.CreateImage("Pill", root, WallArtUI.Bar, 40);
        m_PillImage.raycastTarget = false;
        m_Pill = m_PillImage.rectTransform;
        WallArtUI.Place(m_Pill, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400f, 80f));

        var layout = m_Pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 18, 18);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = m_Pill.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        m_Label = WallArtUI.CreateLabel("Text", m_Pill, "", 34f, WallArtUI.Text);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (m_PlaneMaterial != null) Destroy(m_PlaneMaterial);
    }

    /// <summary>Shows a short confirmation that takes priority over the current hint.</summary>
    public static void Toast(string message, bool warning = false, float seconds = 2.8f)
    {
        if (Instance == null || string.IsNullOrEmpty(message)) return;
        Instance.m_Toast = message;
        Instance.m_ToastIsWarning = warning;
        Instance.m_ToastUntil = Time.unscaledTime + seconds;
        Instance.m_NextEvaluate = 0f;
    }

    /// <summary>Called when a picture has been hung on the wall.</summary>
    public void NotifyPlaced()
    {
        m_PlacedCount++;
        if (!m_EditTipShown)
        {
            m_EditTipShown = true;
            m_EditTipUntil = Time.unscaledTime + 7f;
        }
    }

    void Update()
    {
        if (Time.unscaledTime >= m_NextEvaluate)
        {
            m_NextEvaluate = Time.unscaledTime + 0.2f;
            Evaluate();
        }

        // Fade the pill in and out
        bool show = !string.IsNullOrEmpty(m_Label.text) && m_Label.text == CurrentText() && CurrentText().Length > 0;
        float target = show ? 1f : 0f;
        m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, target, Time.unscaledDeltaTime * 5f);
        if (m_Group.alpha <= 0.01f && m_Label.text != CurrentText()) ApplyText();

        UpdatePlaneTint();
    }

    string CurrentText()
    {
        if (m_Toast != null && Time.unscaledTime < m_ToastUntil) return m_Toast;
        return m_Message;
    }

    void ApplyText()
    {
        bool toast = m_Toast != null && Time.unscaledTime < m_ToastUntil;
        m_Label.text = CurrentText();
        m_PillImage.color = toast ? (m_ToastIsWarning ? new Color(0.55f, 0.2f, 0.18f, 0.92f) : new Color(0.10f, 0.42f, 0.28f, 0.94f)) : WallArtUI.Bar;
    }

    void Evaluate()
    {
        if (m_Toast != null && Time.unscaledTime >= m_ToastUntil) m_Toast = null;

        ScreenRayInteractor interactor = ScreenRayInteractor.Instance;
        TouchScreenInputManager input = TouchScreenInputManager.Instance;

        bool hasSelection = interactor != null && interactor.currentSelectedObject != null;
        bool armed = interactor != null && interactor.currentChosenPrefab != null;
        bool touching = input != null && input.isDoingDrag;
        bool previewOnWall = interactor != null && interactor.isPreviewActive
            && interactor.previewInstance != null && interactor.previewInstance.activeSelf;
        bool drawerOpen = m_Controller != null && m_Controller.CurrentActivePanel == WallArtUIController.ActiveDrawerPanel.Art;

        // Is there a wall in front of the phone right now?
        int wallCount = WallDetection.WallCount;
        m_WallInView = WallDetection.Raycast(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f)).valid;
        if (wallCount > 0 || m_WallInView) m_EverFoundWall = true;

        bool sessionStarting = ARSession.state != ARSessionState.SessionTracking && ARSession.state != ARSessionState.None
            && !Application.isEditor;

        string message;
        if (armed && touching)
            message = previewOnWall ? "Let go to hang it here" : "Drag it onto a wall";
        else if (armed)
            message = m_WallInView || wallCount > 0 ? "Drag the picture onto your wall" : "Point your phone at a wall";
        else if (hasSelection)
            message = Time.unscaledTime < m_EditTipUntil || m_PlacedCount <= 1
                ? "Drag to move  ·  Pinch to resize  ·  Twist to turn"
                : "";
        else if (sessionStarting)
            message = "Starting camera...";
        else if (!m_EverFoundWall)
            message = "Move your phone slowly to scan a wall";
        else if (m_PlacedCount == 0)
            message = drawerOpen ? "Drag a picture up onto your wall" : "Wall found. Tap Art to pick a picture";
        else
            message = drawerOpen ? "Drag a picture up onto your wall" : "";

        m_Message = message;

        // First message appears without waiting for a fade-out
        if (m_Group.alpha <= 0.01f) ApplyText();
    }

    /// <summary>
    /// Detected walls are normally invisible. Show them softly while the user is choosing where
    /// to hang or moving a picture, so it is clear where a wall has been recognised.
    /// </summary>
    void UpdatePlaneTint()
    {
        ARPlaneManager manager = WallDetection.PlaneManager;
        if (manager == null) return;

        ScreenRayInteractor interactor = ScreenRayInteractor.Instance;
        bool placing = interactor != null &&
            (interactor.currentChosenPrefab != null || interactor.isDragging || interactor.isPreviewActive);
        bool drawerOpen = m_Controller != null && m_Controller.CurrentActivePanel == WallArtUIController.ActiveDrawerPanel.Art;
        bool show = placing || (drawerOpen && (interactor == null || interactor.currentSelectedObject == null));

        // All planes share one runtime copy of the plane material so a single colour change fades them all
        if (Time.frameCount % 15 == 0 || m_PlaneMaterial == null)
        {
            foreach (ARPlane plane in manager.trackables)
            {
                MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                Material[] materials = renderer.sharedMaterials;

                // The plane prefab has a shadow receiver plus a see-through fill; tint the fill
                int fill = -1;
                bool alreadyTinted = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    if (materials[i] == m_PlaneMaterial) { alreadyTinted = true; break; }
                    if (fill < 0 && materials[i].HasProperty(k_BaseColor)) fill = i;
                }
                if (alreadyTinted || fill < 0) continue;

                if (m_PlaneMaterial == null)
                {
                    m_PlaneMaterial = new Material(materials[fill]) { name = "WallHighlight (runtime)" };
                    m_PlaneBaseColor = materials[fill].GetColor(k_BaseColor);
                    m_PlaneAlphaHidden = m_PlaneBaseColor.a;
                    m_PlaneAlpha = m_PlaneAlphaHidden;
                }
                materials[fill] = m_PlaneMaterial;
                renderer.sharedMaterials = materials;
            }
        }

        if (m_PlaneMaterial == null) return;

        float targetAlpha = show ? k_PlaneAlphaVisible : m_PlaneAlphaHidden;
        if (Mathf.Approximately(m_PlaneAlpha, targetAlpha)) return;
        m_PlaneAlpha = Mathf.MoveTowards(m_PlaneAlpha, targetAlpha, Time.unscaledDeltaTime * 0.6f);
        float blend = Mathf.InverseLerp(m_PlaneAlphaHidden, k_PlaneAlphaVisible, m_PlaneAlpha);
        Color color = Color.Lerp(m_PlaneBaseColor, k_PlaneTint, blend);
        color.a = m_PlaneAlpha;
        m_PlaneMaterial.SetColor(k_BaseColor, color);
    }
}
