using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the bottom navigation UI for XR Wall Art:
/// - Unselected state: Only Artwork and Frame buttons are visible, centered at the bottom.
/// - Selected state: Artwork, Frame, Size, Trash, and Rotate buttons appear in a balanced row.
/// - Clicking Artwork toggles the presets drawer.
/// - Clicking Size shows the A4, A3, A2 presets panel.
/// - Clicking Frame shows the frame placeholder.
/// - Clicking Trash deletes the selected artwork.
/// </summary>
public class WallArtUIController : MonoBehaviour
{
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
    public Button sizeA4Button;
    public Button sizeA3Button;
    public Button sizeA2Button;

    [Header("Ribbon Layout & Sizing")]
    public float buttonWidth = 140f;
    public float buttonHeight = 140f;
    public float buttonSpacing = 12f;
    public float buttonsYPosition = 300f;

    private bool wasSelectedLastFrame = false;
    private float lastArtClickTime = 0f;

    private void OnEnable()
    {
        if (ArtworkSizeManager.Instance != null)
        {
            ArtworkSizeManager.Instance.OnPresetChanged += HandlePresetChanged;
        }
    }

    private void OnDisable()
    {
        if (ArtworkSizeManager.Instance != null)
        {
            ArtworkSizeManager.Instance.OnPresetChanged -= HandlePresetChanged;
        }
    }

    private void HandlePresetChanged(ArtworkSizePreset preset)
    {
        UpdateSizeButtonsHighlight(preset);
    }

    private void Start()
    {
        if (artworkNavButton != null)
        {
            artworkNavButton.onClick.RemoveAllListeners();
            artworkNavButton.onClick.AddListener(OnArtworkNavClicked);

            // Ensure the Art button displays "Art"
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
        if (sizeA4Button != null)
        {
            sizeA4Button.onClick.RemoveAllListeners();
            sizeA4Button.onClick.AddListener(() => OnSizePresetClicked(ArtworkSizePreset.A4));
        }

        if (sizeA3Button != null)
        {
            sizeA3Button.onClick.RemoveAllListeners();
            sizeA3Button.onClick.AddListener(() => OnSizePresetClicked(ArtworkSizePreset.A3));
        }

        if (sizeA2Button != null)
        {
            sizeA2Button.onClick.RemoveAllListeners();
            sizeA2Button.onClick.AddListener(() => OnSizePresetClicked(ArtworkSizePreset.A2));
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

        // Apply uniform button sizes and set initial button layout (unselected mode)
        ApplyButtonDimensions();
        UpdateContextualButtons(false);
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
                }
            }
            else
            {
                // When deselected, close contextual panels
                if (sizePresetsPanel != null) sizePresetsPanel.SetActive(false);
                if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);
            }
        }
    }

    public void ApplyButtonDimensions()
    {
        Vector2 size = new Vector2(buttonWidth, buttonHeight);
        if (artworkNavButton != null) artworkNavButton.GetComponent<RectTransform>().sizeDelta = size;
        if (frameNavButton != null) frameNavButton.GetComponent<RectTransform>().sizeDelta = size;
        if (sizeNavButton != null) sizeNavButton.GetComponent<RectTransform>().sizeDelta = size;
        if (trashNavButton != null) trashNavButton.GetComponent<RectTransform>().sizeDelta = size;
        if (rotateNavButton != null) rotateNavButton.GetComponent<RectTransform>().sizeDelta = size;
    }

    private void UpdateContextualButtons(bool hasSelection)
    {
        ApplyButtonDimensions();

        if (sizeNavButton != null) sizeNavButton.gameObject.SetActive(hasSelection);
        if (trashNavButton != null) trashNavButton.gameObject.SetActive(hasSelection);
        if (rotateNavButton != null) rotateNavButton.SetActive(hasSelection);

        float step = buttonWidth + buttonSpacing;

        // Dynamically adjust positions for a centered and balanced bottom bar
        if (artworkNavButton != null && frameNavButton != null)
        {
            RectTransform artRect = artworkNavButton.GetComponent<RectTransform>();
            RectTransform frameRect = frameNavButton.GetComponent<RectTransform>();

            if (hasSelection)
            {
                // 5-button balanced tool ribbon right next to each other
                // Order: [ Rotate ] [ Art ] [ Frame ] [ Size ] [ Trash ]
                if (rotateNavButton != null)
                    rotateNavButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-2f * step, buttonsYPosition);

                artRect.anchoredPosition = new Vector2(-1f * step, buttonsYPosition);
                frameRect.anchoredPosition = new Vector2(0f, buttonsYPosition);

                if (sizeNavButton != null)
                    sizeNavButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(1f * step, buttonsYPosition);

                if (trashNavButton != null)
                    trashNavButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(2f * step, buttonsYPosition);
            }
            else
            {
                // 2-button centered layout right next to each other
                // Order: [ Art ] [ Frame ]
                artRect.anchoredPosition = new Vector2(-step * 0.5f, buttonsYPosition);
                frameRect.anchoredPosition = new Vector2(step * 0.5f, buttonsYPosition);
            }
        }
    }

    public void OnArtworkNavClicked()
    {
        if (Time.unscaledTime - lastArtClickTime < 0.25f) return;
        lastArtClickTime = Time.unscaledTime;

        if (sizePresetsPanel != null) sizePresetsPanel.SetActive(false);
        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);

        if (objectListDrawer != null)
        {
            objectListDrawer.ToggleItemList();
        }
    }

    public void OnFrameNavClicked()
    {
        if (sizePresetsPanel != null) sizePresetsPanel.SetActive(false);
        if (objectListDrawer != null && objectListDrawer.IsDrawerOpen) objectListDrawer.DisableItemList();

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
        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);
        if (objectListDrawer != null && objectListDrawer.IsDrawerOpen) objectListDrawer.DisableItemList();

        if (sizePresetsPanel != null)
        {
            bool willBeActive = !sizePresetsPanel.activeSelf;
            sizePresetsPanel.SetActive(willBeActive);
            if (willBeActive && ArtworkSizeManager.Instance != null)
            {
                UpdateSizeButtonsHighlight(ArtworkSizeManager.Instance.GetCurrentPreset());
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

    public void UpdateSizeButtonsHighlight(ArtworkSizePreset activePreset)
    {
        Color normalBg = new Color(0.18f, 0.20f, 0.25f, 0.95f);
        Color activeBg = new Color(0.16f, 0.65f, 0.42f, 1.0f); // vibrant emerald green
        Color normalText = new Color(0.85f, 0.85f, 0.85f, 1.0f);
        Color activeText = Color.white;

        SetButtonVisual(sizeA4Button, activePreset == ArtworkSizePreset.A4, normalBg, activeBg, normalText, activeText);
        SetButtonVisual(sizeA3Button, activePreset == ArtworkSizePreset.A3, normalBg, activeBg, normalText, activeText);
        SetButtonVisual(sizeA2Button, activePreset == ArtworkSizePreset.A2, normalBg, activeBg, normalText, activeText);
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

        if (sizePresetsPanel != null) sizePresetsPanel.SetActive(false);
        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);
        if (objectListDrawer != null && objectListDrawer.IsDrawerOpen) objectListDrawer.DisableItemList();
    }
}
