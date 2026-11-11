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
    public Button sizeA4Button;
    public Button sizeA3Button;
    public Button sizeA2Button;

    private bool wasSelectedLastFrame = false;
    private float lastNavClickTime = 0f;
    private ActiveDrawerPanel activePanel = ActiveDrawerPanel.None;
    public ActiveDrawerPanel CurrentActivePanel => activePanel;

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
        // Permanently hide the redundant rotation button - rotation is now done via 2-finger twist or simulator arrow keys
        if (rotateNavButton != null)
        {
            rotateNavButton.SetActive(false);
        }

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

        activePanel = ActiveDrawerPanel.None;

        // Set initial button active states (unselected mode)
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
        // Only toggle contextual buttons visibility based on selection; positions/sizes are managed by the user in the Unity Editor
        if (sizeNavButton != null) sizeNavButton.gameObject.SetActive(hasSelection);
        if (trashNavButton != null) trashNavButton.gameObject.SetActive(hasSelection);
        if (rotateNavButton != null) rotateNavButton.SetActive(false); // Permanently hidden as rotation is 2-finger / arrow keys
    }

    public void OnArtworkNavClicked()
    {
        if (Time.unscaledTime - lastNavClickTime < 0.25f) return;
        lastNavClickTime = Time.unscaledTime;

        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);

        if (activePanel == ActiveDrawerPanel.Art && objectListDrawer != null && objectListDrawer.IsDrawerOpen)
        {
            // Already open on Art -> toggle closed
            CloseDrawer();
        }
        else
        {
            // Open / switch to Art panel
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
                // If drawer was already open, ensure spawner is armed if no object is selected
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
            // Already open on Size -> toggle closed
            CloseDrawer();
        }
        else
        {
            // Open / switch to Size panel
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
        }

        if (objectListDrawer != null)
        {
            if (!objectListDrawer.IsDrawerOpen)
            {
                objectListDrawer.EnableItemList();
            }
            // Disarm spawner while on size panel so tapping screen doesn't spawn art
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

        CloseDrawer();
        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);
    }
}
