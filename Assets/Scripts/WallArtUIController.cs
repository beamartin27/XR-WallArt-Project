using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the bottom navigation UI for XR Wall Art:
/// - Unselected state: Art and Frame buttons are visible, centered at the bottom.
/// - Selected state: Art, Frame, Size and Delete appear in a balanced row.
/// - Art opens the drawer of pictures (built-in artwork plus the user's own photos).
/// - Frame opens the frame styles. With a picture selected it reframes that picture;
///   with nothing selected it sets the frame for the next picture.
/// - Size shows the size presets with their real dimensions.
/// - Delete removes the selected picture.
///
/// With <see cref="responsiveLayout"/> on, the toolbar and drawer are laid out in code so they fit
/// any phone (scaled to the screen width, clear of the notch and home indicator). Turn it off to
/// position everything by hand in the Unity Editor instead.
/// </summary>
public class WallArtUIController : MonoBehaviour
{
    public enum ActiveDrawerPanel
    {
        None,
        Art,
        Size,
        Frame
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

    [Header("Layout")]
    [Tooltip("Lay out the toolbar and drawer in code so they fit every phone screen. " +
             "Turn off to use the positions set in the scene.")]
    public bool responsiveLayout = true;

    private bool wasSelectedLastFrame = false;
    private ARInteractable lastSelected = null;
    private float lastNavClickTime = 0f;
    private ActiveDrawerPanel activePanel = ActiveDrawerPanel.None;
    public ActiveDrawerPanel CurrentActivePanel => activePanel;

    // Built at runtime
    private const float k_BackdropLip = 34f;   // how far the drawer's backdrop rises above the list
    private Canvas canvas;
    private RectTransform toolbar;
    private GameObject framePanel;
    private TextMeshProUGUI frameCaption;
    private readonly List<KeyValuePair<FrameStyle, Button>> frameButtons = new List<KeyValuePair<FrameStyle, Button>>();

    private static ARInteractable Selected =>
        ScreenRayInteractor.Instance != null ? ScreenRayInteractor.Instance.currentSelectedObject : null;

    private void Awake()
    {
        if (objectListDrawer != null) canvas = objectListDrawer.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;

        // Use the font the scene's buttons already use for everything built in code
        if (artworkNavButton != null)
        {
            TextMeshProUGUI sample = artworkNavButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sample != null) WallArtUI.Font = sample.font;
        }

        if (responsiveLayout && canvas != null)
        {
            // Scale the whole UI with the screen width instead of using raw pixels
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(WallArtUI.ReferenceWidth, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0f;
            }

            if (objectListDrawer != null)
            {
                objectListDrawer.SetInsets(WallArtUI.BottomInset, k_BackdropLip + 8f);
            }
        }
    }

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
            TextMeshProUGUI artText = artworkNavButton.GetComponentInChildren<TextMeshProUGUI>();
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

        // Build the parts of the UI that are created in code
        if (canvas != null)
        {
            if (responsiveLayout)
            {
                BuildDrawerBackdrop();
                BuildToolbar();
                RestyleDrawerList();
                RestyleSizePanel();
            }
            BuildFramePanel();
            WallArtCoach.Create(canvas, this);
        }
        if (objectListDrawer != null)
        {
            UserArtworkLibrary.Ensure(objectListDrawer);
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

        if (framePanel != null)
        {
            framePanel.SetActive(false);
        }

        activePanel = ActiveDrawerPanel.None;

        // Set initial button active states (unselected mode)
        UpdateContextualButtons(false);
        RefreshNavHighlight();
        UpdateSizeLabels();
    }

    private void Update()
    {
        ARInteractable selected = Selected;
        bool hasSelection = selected != null;

        if (hasSelection != wasSelectedLastFrame)
        {
            wasSelectedLastFrame = hasSelection;
            UpdateContextualButtons(hasSelection);

            if (!hasSelection)
            {
                // When deselected, close the Size panel if it was open
                if (activePanel == ActiveDrawerPanel.Size)
                {
                    CloseDrawer();
                }
                if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);
            }
        }

        // A different picture (or none) is selected: show its size and frame
        if (selected != lastSelected)
        {
            lastSelected = selected;
            if (hasSelection && ArtworkSizeManager.Instance != null)
            {
                UpdateSizeButtonsHighlight(ArtworkSizeManager.Instance.GetCurrentPreset());
            }
            UpdateSizeLabels();
            RefreshFramePanel();
        }
    }

    private void LateUpdate()
    {
        // The toolbar rides just above the drawer, and never drops into the home-indicator area
        if (toolbar != null && objectListDrawer != null && objectListDrawer.itemListController != null)
        {
            float aboveDrawer = objectListDrawer.ListTopY + k_BackdropLip + 18f;
            float y = Mathf.Max(WallArtUI.BottomInset + 22f, aboveDrawer);
            if (!Mathf.Approximately(toolbar.anchoredPosition.y, y))
            {
                toolbar.anchoredPosition = new Vector2(0f, y);
            }
        }
    }

    private void UpdateContextualButtons(bool hasSelection)
    {
        // Size and Delete only make sense with a picture selected
        if (sizeNavButton != null) sizeNavButton.gameObject.SetActive(hasSelection);
        if (trashNavButton != null) trashNavButton.gameObject.SetActive(hasSelection);
        if (rotateNavButton != null) rotateNavButton.SetActive(false); // Permanently hidden as rotation is 2-finger / arrow keys
    }

    // ------------------------------------------------------------------ Art

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
        if (framePanel != null)
        {
            framePanel.SetActive(false);
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
        RefreshNavHighlight();
    }

    // ------------------------------------------------------------------ Frame

    public void OnFrameNavClicked()
    {
        if (Time.unscaledTime - lastNavClickTime < 0.25f) return;
        lastNavClickTime = Time.unscaledTime;

        if (activePanel == ActiveDrawerPanel.Frame && objectListDrawer != null && objectListDrawer.IsDrawerOpen)
        {
            CloseDrawer();
        }
        else
        {
            ShowFramePanel();
        }
    }

    public void ShowFramePanel()
    {
        if (framePanel == null) return;
        activePanel = ActiveDrawerPanel.Frame;

        if (objectListDrawer != null && objectListDrawer.itemList != null)
        {
            objectListDrawer.itemList.gameObject.SetActive(false);
        }
        if (sizePresetsPanel != null)
        {
            sizePresetsPanel.SetActive(false);
        }
        framePanel.SetActive(true);
        RefreshFramePanel();

        OpenDrawerForOptions();
        RefreshNavHighlight();
    }

    public void OnFrameStyleClicked(FrameStyle style)
    {
        // The choice also becomes the frame for pictures placed from now on
        ArtworkFrame.DefaultStyle = style;

        ARInteractable selected = Selected;
        if (selected != null)
        {
            if (ArtworkFrame.CanFrame(selected.gameObject))
            {
                ArtworkFrame.GetOrAdd(selected.gameObject).Apply(style);
                Haptics.Light();
            }
            else
            {
                WallArtCoach.Toast("This piece cannot be framed", true);
            }
        }
        RefreshFramePanel();
    }

    private void RefreshFramePanel()
    {
        if (framePanel == null) return;

        ARInteractable selected = Selected;
        bool canFrameSelection = selected != null && ArtworkFrame.CanFrame(selected.gameObject);

        FrameStyle current = ArtworkFrame.DefaultStyle;
        if (canFrameSelection)
        {
            ArtworkFrame frame = selected.GetComponent<ArtworkFrame>();
            if (frame != null) current = frame.Style;
        }

        if (frameCaption != null)
        {
            if (selected == null) frameCaption.text = "Frame for the next picture you hang";
            else if (canFrameSelection) frameCaption.text = "Frame for this picture";
            else frameCaption.text = "This piece has no frame";
        }

        foreach (KeyValuePair<FrameStyle, Button> pair in frameButtons)
        {
            bool isActive = pair.Key == current && (selected == null || canFrameSelection);
            Image image = pair.Value.GetComponent<Image>();
            if (image != null) image.color = isActive ? WallArtUI.Accent : WallArtUI.Control;
            pair.Value.interactable = selected == null || canFrameSelection;
        }
    }

    // ------------------------------------------------------------------ Size

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
        if (framePanel != null)
        {
            framePanel.SetActive(false);
        }

        if (sizePresetsPanel != null)
        {
            sizePresetsPanel.SetActive(true);
        }

        UpdateSizeLabels();
        if (ArtworkSizeManager.Instance != null)
        {
            UpdateSizeButtonsHighlight(ArtworkSizeManager.Instance.GetCurrentPreset());
        }

        OpenDrawerForOptions();
        RefreshNavHighlight();
    }

    /// <summary>Opens the drawer for a panel of options (not for picking artwork).</summary>
    private void OpenDrawerForOptions()
    {
        if (objectListDrawer == null) return;

        if (!objectListDrawer.IsDrawerOpen)
        {
            objectListDrawer.EnableItemList();
        }
        // Disarm spawner while on an options panel so touching the wall doesn't hang a picture
        if (ScreenRayInteractor.Instance != null)
        {
            ScreenRayInteractor.Instance.SetTargetPrefab(null);
        }
        objectListDrawer.ClearSelectionHighlight();
    }

    public void CloseDrawer()
    {
        activePanel = ActiveDrawerPanel.None;
        RefreshNavHighlight();

        if (objectListDrawer != null && objectListDrawer.IsDrawerOpen)
        {
            objectListDrawer.DisableItemList(() =>
            {
                // Only tidy up if nothing was reopened while the drawer was sliding shut
                if (activePanel != ActiveDrawerPanel.None) return;
                if (sizePresetsPanel != null) sizePresetsPanel.SetActive(false);
                if (framePanel != null) framePanel.SetActive(false);
            });
        }
        else
        {
            if (sizePresetsPanel != null)
            {
                sizePresetsPanel.SetActive(false);
            }
            if (framePanel != null)
            {
                framePanel.SetActive(false);
            }
        }
    }

    public void OnSizePresetClicked(ArtworkSizePreset preset)
    {
        if (ArtworkSizeManager.Instance != null)
        {
            ArtworkSizeManager.Instance.ApplyPreset(preset);
        }
        Haptics.Light();
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

    /// <summary>
    /// Shows what each size means for the selected picture, in centimetres. The app is about
    /// judging real sizes on a real wall, so the numbers matter more than the paper names.
    /// </summary>
    private void UpdateSizeLabels()
    {
        ARInteractable selected = Selected;
        ArtworkProperties properties = selected != null ? selected.GetComponent<ArtworkProperties>() : null;
        Vector2 baseSize = properties != null ? properties.baseDimensionsMeters : new Vector2(0.297f, 0.420f);

        // Paper names only apply to pictures with paper proportions; photos get plain words
        bool paperProportions = Mathf.Abs(baseSize.x - 0.297f) < 0.005f && Mathf.Abs(baseSize.y - 0.420f) < 0.005f;

        SetSizeLabel(sizeA4Button, paperProportions ? "A4" : "Small", baseSize * ArtworkSizeManager.SCALE_A4);
        SetSizeLabel(sizeA3Button, paperProportions ? "A3" : "Medium", baseSize * ArtworkSizeManager.SCALE_A3);
        SetSizeLabel(sizeA2Button, paperProportions ? "A2" : "Large", baseSize * ArtworkSizeManager.SCALE_A2);
    }

    private static void SetSizeLabel(Button button, string title, Vector2 meters)
    {
        if (button == null) return;
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) return;
        int widthCm = Mathf.RoundToInt(meters.x * 100f);
        int heightCm = Mathf.RoundToInt(meters.y * 100f);
        label.richText = true;
        label.text = title + "\n<size=58%>" + widthCm + " x " + heightCm + " cm</size>";
    }

    // ------------------------------------------------------------------ Delete

    public void OnTrashNavClicked()
    {
        bool hadSelection = Selected != null;
        if (ScreenRayInteractor.Instance != null)
        {
            ScreenRayInteractor.Instance.DeleteSelection();
        }
        if (hadSelection)
        {
            Haptics.Medium();
            WallArtCoach.Toast("Picture removed");
        }

        CloseDrawer();
        if (framePlaceholderPanel != null) framePlaceholderPanel.SetActive(false);
    }

    // ------------------------------------------------------------------ UI built in code

    private void RefreshNavHighlight()
    {
        if (!responsiveLayout) return;
        SetNavActive(artworkNavButton, activePanel == ActiveDrawerPanel.Art);
        SetNavActive(frameNavButton, activePanel == ActiveDrawerPanel.Frame);
        SetNavActive(sizeNavButton, activePanel == ActiveDrawerPanel.Size);
        SetNavActive(trashNavButton, false);
    }

    private static void SetNavActive(Button button, bool isActive)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = isActive ? WallArtUI.Accent : WallArtUI.Control;
    }

    /// <summary>A sheet behind the drawer's contents, so every panel reads as one surface.</summary>
    private void BuildDrawerBackdrop()
    {
        if (objectListDrawer == null || objectListDrawer.itemListController == null) return;
        RectTransform drawer = objectListDrawer.itemListController;
        float listHeight = objectListDrawer.itemList != null && objectListDrawer.itemList.rect.height > 0f
            ? objectListDrawer.itemList.rect.height
            : 360f;

        Image backdrop = WallArtUI.CreateImage("DrawerBackdrop", drawer, WallArtUI.Sheet, 40);
        RectTransform rect = backdrop.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        // From well below the screen edge (covers the home-indicator area) to a lip above the list
        rect.offsetMin = new Vector2(0f, -listHeight * 0.5f - 600f);
        rect.offsetMax = new Vector2(0f, listHeight * 0.5f + k_BackdropLip);
        rect.SetSiblingIndex(0);

        Image grabber = WallArtUI.CreateImage("Grabber", rect, new Color(1f, 1f, 1f, 0.28f), 4);
        grabber.raycastTarget = false;
        WallArtUI.Place(grabber.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -13f), new Vector2(84f, 8f));
    }

    /// <summary>
    /// Gathers the scene's navigation buttons into one centred bar. Hidden buttons take no
    /// space, so the bar is always balanced whether two or four buttons are showing.
    /// </summary>
    private void BuildToolbar()
    {
        Image bar = WallArtUI.CreateImage("Toolbar", canvas.transform, WallArtUI.Bar, 44);
        toolbar = bar.rectTransform;
        toolbar.anchorMin = new Vector2(0.5f, 0f);
        toolbar.anchorMax = new Vector2(0.5f, 0f);
        toolbar.pivot = new Vector2(0.5f, 0f);
        toolbar.anchoredPosition = new Vector2(0f, WallArtUI.BottomInset + 22f);

        HorizontalLayoutGroup layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AdoptNavButton(artworkNavButton, "Art", WallArtUI.Text);
        AdoptNavButton(frameNavButton, "Frame", WallArtUI.Text);
        AdoptNavButton(sizeNavButton, "Size", WallArtUI.Text);
        AdoptNavButton(trashNavButton, "Delete", WallArtUI.Danger);
    }

    private void AdoptNavButton(Button button, string text, Color textColor)
    {
        if (button == null) return;

        RectTransform rect = (RectTransform)button.transform;
        rect.SetParent(toolbar, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        WallArtUI.SetPreferredSize(button.gameObject, 214f, 100f);

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            WallArtUI.SetRounded(image, 32);
            image.preserveAspect = false;
            image.color = WallArtUI.Control;
            button.targetGraphic = image;
        }

        // Same pressed/disabled feedback on every button
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI[] labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI label = labels.Length > 0 ? labels[0] : WallArtUI.CreateLabel("Label", rect, text, 36f, textColor);
        for (int i = 1; i < labels.Length; i++) labels[i].gameObject.SetActive(false);

        label.gameObject.SetActive(true);
        WallArtUI.Stretch(label.rectTransform);
        label.text = text;
        label.fontSize = 36f;
        label.enableAutoSizing = false;
        label.fontStyle = FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.color = textColor;
        label.raycastTarget = false;
    }

    /// <summary>Tidies the artwork list: no background of its own, no scrollbar, even spacing.</summary>
    private void RestyleDrawerList()
    {
        if (objectListDrawer == null || objectListDrawer.itemList == null) return;

        // The backdrop is the background now. Keep the image (it catches drags) but make it invisible.
        Image listBackground = objectListDrawer.itemList.GetComponent<Image>();
        if (listBackground != null)
        {
            Color clear = listBackground.color;
            clear.a = 0f;
            listBackground.color = clear;
        }

        // Phones scroll by swiping; a permanent scrollbar only costs space
        ScrollRect scroll = objectListDrawer.itemList.GetComponent<ScrollRect>();
        if (scroll != null)
        {
            Scrollbar bar = scroll.horizontalScrollbar;
            scroll.horizontalScrollbar = null;
            if (bar != null) bar.gameObject.SetActive(false);
            if (scroll.viewport != null) WallArtUI.Stretch(scroll.viewport);
        }

        if (objectListDrawer.buttonsContent != null)
        {
            HorizontalLayoutGroup layout = objectListDrawer.buttonsContent.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(20, 20, 14, 22);
                layout.spacing = 16f;
            }

            // Thumbnails keep their proportions instead of stretching to the tile
            foreach (UISelectionButton tile in objectListDrawer.buttonsContent.GetComponentsInChildren<UISelectionButton>(true))
            {
                Image image = tile.GetComponent<Image>();
                if (image != null) image.preserveAspect = true;
            }
        }
    }

    /// <summary>Makes the scene's size panel fill the drawer with three large, readable buttons.</summary>
    private void RestyleSizePanel()
    {
        if (sizePresetsPanel == null) return;

        RectTransform panel = sizePresetsPanel.GetComponent<RectTransform>();
        if (panel == null) return;
        panel.localScale = Vector3.one; // sizes below are real canvas units
        WallArtUI.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020f, 340f));

        // The drawer backdrop is the background now
        Image background = sizePresetsPanel.GetComponent<Image>();
        if (background != null) background.enabled = false;

        TextMeshProUGUI caption = WallArtUI.CreateLabel("Caption", panel, "Size on your wall", 28f, WallArtUI.TextDim);
        WallArtUI.Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(900f, 40f));

        Button[] buttons = { sizeA4Button, sizeA3Button, sizeA2Button };
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;

            RectTransform rect = (RectTransform)button.transform;
            rect.localScale = Vector3.one;
            WallArtUI.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2((i - 1) * 332f, -24f), new Vector2(316f, 196f));

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                WallArtUI.SetRounded(image, 30);
                image.preserveAspect = false;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.rectTransform.localScale = Vector3.one;
                WallArtUI.Stretch(label.rectTransform);
                label.fontSize = 50f;
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.Center;
                label.lineSpacing = -10f;
                label.raycastTarget = false;
            }
        }
    }

    /// <summary>Builds the frame picker shown in the drawer.</summary>
    private void BuildFramePanel()
    {
        if (objectListDrawer == null || objectListDrawer.itemListController == null) return;

        RectTransform panel = WallArtUI.CreateRect("FramePresetsPanel", objectListDrawer.itemListController);
        WallArtUI.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 340f));
        framePanel = panel.gameObject;

        frameCaption = WallArtUI.CreateLabel("Caption", panel, "", 28f, WallArtUI.TextDim);
        WallArtUI.Place(frameCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(900f, 40f));

        RectTransform row = WallArtUI.CreateRect("Row", panel);
        WallArtUI.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(1040f, 250f));
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        frameButtons.Clear();
        foreach (FrameStyleInfo info in FrameCatalog.Styles)
        {
            FrameStyle style = info.style;
            Button card = WallArtUI.CreateButton("Frame_" + style, row, null, 0f, 30);
            WallArtUI.SetPreferredSize(card.gameObject, 194f, 240f);
            card.onClick.AddListener(() => OnFrameStyleClicked(style));

            BuildFrameSwatch(card.transform, info);

            TextMeshProUGUI label = WallArtUI.CreateLabel("Label", card.transform, info.displayName, 26f, WallArtUI.Text);
            WallArtUI.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(190f, 40f));

            frameButtons.Add(new KeyValuePair<FrameStyle, Button>(style, card));
        }
    }

    /// <summary>A little picture-in-a-frame so each style can be recognised at a glance.</summary>
    private static void BuildFrameSwatch(Transform parent, FrameStyleInfo info)
    {
        Vector2 size = new Vector2(104f, 132f);
        Color pictureColor = new Color(0.56f, 0.66f, 0.78f, 1f);

        RectTransform root = WallArtUI.CreateRect("Swatch", parent);
        WallArtUI.Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), size);

        bool hasFrame = info.style != FrameStyle.None;
        float border = hasFrame ? Mathf.Clamp(info.moldingWidth * 420f, 7f, 16f) : 0f;
        float mat = hasFrame && info.matWidth > 0f ? 14f : 0f;

        if (hasFrame)
        {
            Image molding = WallArtUI.CreateImage("Molding", root, info.swatch, 5);
            molding.raycastTarget = false;
            WallArtUI.Stretch(molding.rectTransform);
        }
        if (mat > 0f)
        {
            Image matBoard = WallArtUI.CreateImage("Mat", root, new Color(0.96f, 0.95f, 0.92f, 1f));
            matBoard.raycastTarget = false;
            WallArtUI.Stretch(matBoard.rectTransform, border, border, border, border);
        }

        float inset = border + mat + (hasFrame ? 0f : 8f);
        Image picture = WallArtUI.CreateImage("Picture", root, pictureColor, hasFrame ? 0 : 4);
        picture.raycastTarget = false;
        WallArtUI.Stretch(picture.rectTransform, inset, inset, inset, inset);
    }
}
