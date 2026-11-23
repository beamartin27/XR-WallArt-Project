using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the bottom navigation UI for XR Wall Art:
/// - Unselected state: Only Artwork and Frame buttons are visible, centered at the bottom.
/// - Selected state: Artwork, Frame, Size, and Trash buttons appear in a balanced row.
/// - Clicking Artwork toggles the presets drawer with '+' add button and posters.
/// - Clicking Size shows the A5, A4, A3, A2, A1 presets and Portrait/Landscape orientation toggles.
/// - Clicking Frame shows the frame placeholder.
/// - Clicking Trash deletes the selected artwork.
/// </summary>
public class WallArtUIController : MonoBehaviour
{
    public enum ActiveDrawerPanel
    {
        None,
        Art,
        Size
    }

    [Header("Primary Navigation Buttons")]
    public Button artworkNavButton;
    public Button frameNavButton;
    public Button sizeNavButton;
    public Button trashNavButton;
    public GameObject rotateNavButton;

    [Header("Sub-Panels / Drawers")]
    public ObjectListController objectListDrawer;
    public GameObject sizePresetsPanel;
    public GameObject framePlaceholderPanel;

    [Header("Size Preset Buttons")]
    public Button sizeA5Button;
    public Button sizeA4Button;
    public Button sizeA3Button;
    public Button sizeA2Button;
    public Button sizeA1Button;

    [Header("Orientation Preset Buttons")]
    public Button portraitButton;
    public Button landscapeButton;

    private bool wasSelectedLastFrame = false;
    private float lastNavClickTime = 0f;
    private ActiveDrawerPanel activePanel = ActiveDrawerPanel.None;
    public ActiveDrawerPanel CurrentActivePanel => activePanel;

    private void OnEnable()
    {
        if (ArtworkSizeManager.Instance != null)
        {
            ArtworkSizeManager.Instance.OnPresetChanged += HandlePresetChanged;
            ArtworkSizeManager.Instance.OnOrientationChanged += HandleOrientationChanged;
        }
    }

    private void OnDisable()
    {
        if (ArtworkSizeManager.Instance != null)
        {
            ArtworkSizeManager.Instance.OnPresetChanged -= HandlePresetChanged;
            ArtworkSizeManager.Instance.OnOrientationChanged -= HandleOrientationChanged;
        }
    }

    private void HandlePresetChanged(ArtworkSizePreset preset)
    {
        UpdateSizeButtonsHighlight(preset);
    }

    private void HandleOrientationChanged(ArtworkOrientation orientation)
    {
        UpdateOrientationButtonsHighlight(orientation);
    }

    private void Start()
    {
        // Permanently hide redundant rotation button - rotation is done via 2-finger twist or simulator arrow keys
        if (rotateNavButton != null)
        {
            rotateNavButton.SetActive(false);
        }

        // Initialize CustomPosterManager to ensure startup upload dialog and '+' drawer button
        if (CustomPosterManager.Instance != null) { }

        // Configure smaller button scales, size presets (A5..A1), and orientation toggles (Portrait/Landscape)
        EnsureSizePresetsAndOrientationButtons();

        if (artworkNavButton != null)
        {
            artworkNavButton.onClick.RemoveAllListeners();
            artworkNavButton.onClick.AddListener(OnArtworkNavClicked);

            TMPro.TextMeshProUGUI artText = artworkNavButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (artText != null)
            {
                artText.text = "Art";
            }
        }

        if (frameNavButton != null)
        {
            frameNavButton.onClick.RemoveAllListeners();
            frameNavButton.onClick.AddListener(OnFrameNavClicked);
        }

        if (sizeNavButton != null)
        {
            sizeNavButton.onClick.RemoveAllListeners();
            sizeNavButton.onClick.AddListener(OnSizeNavClicked);
        }

        if (trashNavButton != null)
        {
            trashNavButton.onClick.RemoveAllListeners();
            trashNavButton.onClick.AddListener(OnTrashNavClicked);
        }

        // Setup Size Preset Buttons
        SetupSizeButton(sizeA5Button, ArtworkSizePreset.A5);
        SetupSizeButton(sizeA4Button, ArtworkSizePreset.A4);
        SetupSizeButton(sizeA3Button, ArtworkSizePreset.A3);
        SetupSizeButton(sizeA2Button, ArtworkSizePreset.A2);
        SetupSizeButton(sizeA1Button, ArtworkSizePreset.A1);

        // Setup Orientation Preset Buttons
        if (portraitButton != null)
        {
            portraitButton.onClick.RemoveAllListeners();
            portraitButton.onClick.AddListener(() => OnOrientationPresetClicked(ArtworkOrientation.Portrait));
        }

        if (landscapeButton != null)
        {
            landscapeButton.onClick.RemoveAllListeners();
            landscapeButton.onClick.AddListener(() => OnOrientationPresetClicked(ArtworkOrientation.Landscape));
        }

        // Initialize sub-panels closed
        if (sizePresetsPanel != null)
        {
            sizePresetsPanel.SetActive(false);
        }

        if (framePlaceholderPanel != null)
        {
            framePlaceholderPanel.SetActive(false);
        }

        activePanel = ActiveDrawerPanel.None;

        // Set initial button active states (unselected mode)
        UpdateContextualButtons(false);
    }

    private void EnsureSizePresetsAndOrientationButtons()
    {
        // Apply smaller, sleek scaling to bottom navigation buttons
        Vector3 smallerScale = new Vector3(1.5f, 1.5f, 1f);
        if (artworkNavButton != null) artworkNavButton.transform.localScale = smallerScale;
        if (frameNavButton != null) frameNavButton.transform.localScale = smallerScale;
        if (trashNavButton != null) trashNavButton.transform.localScale = smallerScale;
        if (sizeNavButton != null)
        {
            sizeNavButton.transform.localScale = smallerScale;
            RectTransform srt = sizeNavButton.GetComponent<RectTransform>();
            if (srt != null && artworkNavButton != null)
            {
                RectTransform artRt = artworkNavButton.GetComponent<RectTransform>();
                if (artRt != null)
                {
                    srt.anchoredPosition = new Vector2(srt.anchoredPosition.x, artRt.anchoredPosition.y);
                }
            }
        }

        if (sizePresetsPanel == null) return;

        // Scale down size presets panel and expand dimensions for 2-row layout
        sizePresetsPanel.transform.localScale = smallerScale;
        RectTransform panelRt = sizePresetsPanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.sizeDelta = new Vector2(640f, 170f);
        }

        Vector2 sizeBtnDimensions = new Vector2(90f, 55f);
        float topRowY = 32f;
        float bottomRowY = -35f;

        if (sizeA4Button != null)
        {
            RectTransform rt = sizeA4Button.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(-100f, topRowY); rt.sizeDelta = sizeBtnDimensions; }
            TMPro.TextMeshProUGUI tmp = sizeA4Button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.fontSize = 22;
        }

        if (sizeA3Button != null)
        {
            RectTransform rt = sizeA3Button.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(0f, topRowY); rt.sizeDelta = sizeBtnDimensions; }
            TMPro.TextMeshProUGUI tmp = sizeA3Button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.fontSize = 22;
        }

        if (sizeA2Button != null)
        {
            RectTransform rt = sizeA2Button.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = new Vector2(100f, topRowY); rt.sizeDelta = sizeBtnDimensions; }
            TMPro.TextMeshProUGUI tmp = sizeA2Button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.fontSize = 22;
        }

        // Create A5 button if null
        if (sizeA5Button == null && sizeA4Button != null)
        {
            GameObject a5Obj = Instantiate(sizeA4Button.gameObject, sizePresetsPanel.transform);
            a5Obj.name = "Button_A5";
            RectTransform rt = a5Obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-200f, topRowY);
            rt.sizeDelta = sizeBtnDimensions;
            TMPro.TextMeshProUGUI tmp = a5Obj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) { tmp.text = "A5"; tmp.fontSize = 22; }
            sizeA5Button = a5Obj.GetComponent<Button>();
        }

        // Create A1 button if null
        if (sizeA1Button == null && sizeA2Button != null)
        {
            GameObject a1Obj = Instantiate(sizeA2Button.gameObject, sizePresetsPanel.transform);
            a1Obj.name = "Button_A1";
            RectTransform rt = a1Obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(200f, topRowY);
            rt.sizeDelta = sizeBtnDimensions;
            TMPro.TextMeshProUGUI tmp = a1Obj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) { tmp.text = "A1"; tmp.fontSize = 22; }
            sizeA1Button = a1Obj.GetComponent<Button>();
        }

        // Create Portrait button if null
        if (portraitButton == null && sizeA3Button != null)
        {
            GameObject portraitObj = Instantiate(sizeA3Button.gameObject, sizePresetsPanel.transform);
            portraitObj.name = "Button_Portrait";
            RectTransform rt = portraitObj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-110f, bottomRowY);
            rt.sizeDelta = new Vector2(170f, 50f);
            TMPro.TextMeshProUGUI tmp = portraitObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) { tmp.text = "Portrait"; tmp.fontSize = 20; }
            portraitButton = portraitObj.GetComponent<Button>();
        }

        // Create Landscape button if null
        if (landscapeButton == null && sizeA3Button != null)
        {
            GameObject landscapeObj = Instantiate(sizeA3Button.gameObject, sizePresetsPanel.transform);
            landscapeObj.name = "Button_Landscape";
            RectTransform rt = landscapeObj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(110f, bottomRowY);
            rt.sizeDelta = new Vector2(170f, 50f);
            TMPro.TextMeshProUGUI tmp = landscapeObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) { tmp.text = "Landscape"; tmp.fontSize = 20; }
            landscapeButton = landscapeObj.GetComponent<Button>();
        }
    }

    private void SetupSizeButton(Button btn, ArtworkSizePreset preset)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnSizePresetClicked(preset));
    }

    private void Update()
    {
        bool hasSelection = ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject != null;

        if (hasSelection != wasSelectedLastFrame)
        {
            wasSelectedLastFrame = hasSelection;
            UpdateContextualButtons(hasSelection);

            if (hasSelection)
            {
                if (ArtworkSizeManager.Instance != null)
                {
                    UpdateSizeButtonsHighlight(ArtworkSizeManager.Instance.GetCurrentPreset());
                    UpdateOrientationButtonsHighlight(ArtworkSizeManager.Instance.GetCurrentOrientation());
                }
            }
            else
            {
                // When deselected, close the Size panel if it was open
                if (activePanel == ActiveDrawerPanel.Size)
                {
                    CloseDrawer();
                }
                if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);
            }
        }
    }

    private void UpdateContextualButtons(bool hasSelection)
    {
        if (sizeNavButton != null) sizeNavButton.gameObject.SetActive(hasSelection);
        if (trashNavButton != null) trashNavButton.gameObject.SetActive(hasSelection);
        if (rotateNavButton != null) rotateNavButton.SetActive(false);
    }

    public void OnArtworkNavClicked()
    {
        if (Time.unscaledTime - lastNavClickTime < 0.25f) return;
        lastNavClickTime = Time.unscaledTime;

        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);

        if (activePanel == ActiveDrawerPanel.Art && objectListDrawer != null && objectListDrawer.IsDrawerOpen)
        {
            CloseDrawer();
        }
        else
        {
            ShowArtPanel();
        }
    }

    public void ShowArtPanel()
    {
        activePanel = ActiveDrawerPanel.Art;

        if (sizePresetsPanel != null)
        {
            sizePresetsPanel.SetActive(false);
        }

        if (objectListDrawer != null)
        {
            if (objectListDrawer.itemList != null)
            {
                objectListDrawer.itemList.gameObject.SetActive(true);
            }

            if (!objectListDrawer.IsDrawerOpen)
            {
                objectListDrawer.EnableItemList();
            }
            else
            {
                if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject == null)
                {
                    ScreenRayInteractor.Instance.isSpawnerActivated = true;
                }
            }
        }
    }

    public void OnFrameNavClicked()
    {
        CloseDrawer();

        if (framePlaceholderPanel != null)
        {
            framePlaceholderPanel.SetActive(!framePlaceholderPanel.activeSelf);
        }
        else
        {
            Debug.Log("Frame selection: Feature coming soon!");
        }
    }

    public void OnSizeNavClicked()
    {
        if (Time.unscaledTime - lastNavClickTime < 0.25f) return;
        lastNavClickTime = Time.unscaledTime;

        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);

        if (activePanel == ActiveDrawerPanel.Size && objectListDrawer != null && objectListDrawer.IsDrawerOpen)
        {
            CloseDrawer();
        }
        else
        {
            ShowSizePanel();
        }
    }

    public void ShowSizePanel()
    {
        activePanel = ActiveDrawerPanel.Size;

        if (objectListDrawer != null && objectListDrawer.itemList != null)
        {
            objectListDrawer.itemList.gameObject.SetActive(false);
        }

        if (sizePresetsPanel != null)
        {
            sizePresetsPanel.SetActive(true);
        }

        if (ArtworkSizeManager.Instance != null)
        {
            UpdateSizeButtonsHighlight(ArtworkSizeManager.Instance.GetCurrentPreset());
            UpdateOrientationButtonsHighlight(ArtworkSizeManager.Instance.GetCurrentOrientation());
        }

        if (objectListDrawer != null)
        {
            if (!objectListDrawer.IsDrawerOpen)
            {
                objectListDrawer.EnableItemList();
            }
            if (ScreenRayInteractor.Instance != null)
            {
                ScreenRayInteractor.Instance.isSpawnerActivated = false;
            }
        }
    }

    public void CloseDrawer()
    {
        activePanel = ActiveDrawerPanel.None;

        if (objectListDrawer != null && objectListDrawer.IsDrawerOpen)
        {
            objectListDrawer.DisableItemList(() =>
            {
                if (sizePresetsPanel != null)
                {
                    sizePresetsPanel.SetActive(false);
                }
            });
        }
        else
        {
            if (sizePresetsPanel != null)
            {
                sizePresetsPanel.SetActive(false);
            }
        }
    }

    public void OnSizePresetClicked(ArtworkSizePreset preset)
    {
        if (ArtworkSizeManager.Instance != null)
        {
            ArtworkSizeManager.Instance.ApplyPreset(preset);
        }
        UpdateSizeButtonsHighlight(preset);
    }

    public void OnOrientationPresetClicked(ArtworkOrientation orientation)
    {
        if (ArtworkSizeManager.Instance != null)
        {
            ArtworkSizeManager.Instance.SetOrientation(orientation);
        }
        UpdateOrientationButtonsHighlight(orientation);
    }

    public void UpdateSizeButtonsHighlight(ArtworkSizePreset activePreset)
    {
        Color normalBg = new Color(0.18f, 0.20f, 0.25f, 0.95f);
        Color activeBg = new Color(0.16f, 0.65f, 0.42f, 1.0f); // vibrant emerald green
        Color normalText = new Color(0.85f, 0.85f, 0.85f, 1.0f);
        Color activeText = Color.white;

        SetButtonVisual(sizeA5Button, activePreset == ArtworkSizePreset.A5, normalBg, activeBg, normalText, activeText);
        SetButtonVisual(sizeA4Button, activePreset == ArtworkSizePreset.A4, normalBg, activeBg, normalText, activeText);
        SetButtonVisual(sizeA3Button, activePreset == ArtworkSizePreset.A3, normalBg, activeBg, normalText, activeText);
        SetButtonVisual(sizeA2Button, activePreset == ArtworkSizePreset.A2, normalBg, activeBg, normalText, activeText);
        SetButtonVisual(sizeA1Button, activePreset == ArtworkSizePreset.A1, normalBg, activeBg, normalText, activeText);
    }

    public void UpdateOrientationButtonsHighlight(ArtworkOrientation activeOrientation)
    {
        Color normalBg = new Color(0.18f, 0.20f, 0.25f, 0.95f);
        Color activeBg = new Color(0.20f, 0.50f, 0.85f, 1.0f); // crisp modern blue
        Color normalText = new Color(0.85f, 0.85f, 0.85f, 1.0f);
        Color activeText = Color.white;

        SetButtonVisual(portraitButton, activeOrientation == ArtworkOrientation.Portrait, normalBg, activeBg, normalText, activeText);
        SetButtonVisual(landscapeButton, activeOrientation == ArtworkOrientation.Landscape, normalBg, activeBg, normalText, activeText);
    }

    private void SetButtonVisual(Button btn, bool isActive, Color normalBg, Color activeBg, Color normalText, Color activeText)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = isActive ? activeBg : normalBg;
        }

        TMPro.TextMeshProUGUI txt = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (txt != null)
        {
            txt.color = isActive ? activeText : normalText;
            txt.fontStyle = isActive ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
        }
    }

    public void OnTrashNavClicked()
    {
        if (ScreenRayInteractor.Instance != null)
        {
            ScreenRayInteractor.Instance.DeleteSelection();
        }

        CloseDrawer();
        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);
    }
}
