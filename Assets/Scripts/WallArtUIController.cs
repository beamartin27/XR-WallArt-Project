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

    [Header("Y Position of Bottom Ribbon")]
    public float buttonsYPosition = 300f;

    private bool wasSelectedLastFrame = false;

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
            artworkNavButton.onClick.AddListener(OnArtworkNavClicked);
        }

        if (frameNavButton != null)
        {
            frameNavButton.onClick.AddListener(OnFrameNavClicked);
        }

        if (sizeNavButton != null)
        {
            sizeNavButton.onClick.AddListener(OnSizeNavClicked);
        }

        if (trashNavButton != null)
        {
            trashNavButton.onClick.AddListener(OnTrashNavClicked);
        }

        // Setup Size Preset Buttons
        if (sizeA4Button != null)
        {
            sizeA4Button.onClick.AddListener(() => OnSizePresetClicked(ArtworkSizePreset.A4));
        }

        if (sizeA3Button != null)
        {
            sizeA3Button.onClick.AddListener(() => OnSizePresetClicked(ArtworkSizePreset.A3));
        }

        if (sizeA2Button != null)
        {
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

        // Set initial button layout (unselected mode)
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

    private void UpdateContextualButtons(bool hasSelection)
    {
        if (sizeNavButton != null) sizeNavButton.gameObject.SetActive(hasSelection);
        if (trashNavButton != null) trashNavButton.gameObject.SetActive(hasSelection);
        if (rotateNavButton != null) rotateNavButton.SetActive(hasSelection);

        // Dynamically adjust positions for a centered and balanced bottom bar
        if (artworkNavButton != null && frameNavButton != null)
        {
            RectTransform artRect = artworkNavButton.GetComponent<RectTransform>();
            RectTransform frameRect = frameNavButton.GetComponent<RectTransform>();

            if (hasSelection)
            {
                // 5-button balanced tool ribbon across the bottom
                if (rotateNavButton != null)
                    rotateNavButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-360f, buttonsYPosition);

                artRect.anchoredPosition = new Vector2(-180f, buttonsYPosition);
                frameRect.anchoredPosition = new Vector2(0f, buttonsYPosition);

                if (sizeNavButton != null)
                    sizeNavButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(180f, buttonsYPosition);

                if (trashNavButton != null)
                    trashNavButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(360f, buttonsYPosition);
            }
            else
            {
                // 2-button centered layout when nothing is selected
                artRect.anchoredPosition = new Vector2(-130f, buttonsYPosition);
                frameRect.anchoredPosition = new Vector2(130f, buttonsYPosition);
            }
        }
    }

    public void OnArtworkNavClicked()
    {
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
