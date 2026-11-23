using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages user-uploaded custom poster images:
/// - Displays a startup modal dialog allowing users to upload multiple pictures on app launch.
/// - Adds a '+' button to the artwork drawer to upload additional posters at runtime.
/// - Dynamically generates 3D framed poster prefabs with correct aspect ratio preservation.
/// - Inserts custom posters at the start of the artwork drawer so they appear before built-in presets.
/// </summary>
public class CustomPosterManager : MonoBehaviour
{
    private static CustomPosterManager _instance;
    public static CustomPosterManager Instance => _instance;

    [Header("Base Poster Template")]
    [Tooltip("Base prefab used as template (defaults to first artwork prefab in drawer if null)")]
    public GameObject basePosterTemplate;

    [Header("Startup Dialog Options")]
    public bool showStartupDialogOnLaunch = true;

    private GameObject startupDialogObject;
    private TextMeshProUGUI startupStatusText;
    private Button startupContinueButton;
    private GameObject plusDrawerButton;

    private List<string> pendingStartupFiles = new List<string>();
    private List<GameObject> createdCustomPrefabs = new List<GameObject>();
    private int customPosterCounter = 0;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Auto-detect base template if not assigned
        if (basePosterTemplate == null && ObjectListController.Instance != null && ObjectListController.Instance.buttonsContent != null)
        {
            UISelectionButton btn = ObjectListController.Instance.buttonsContent.GetComponentInChildren<UISelectionButton>();
            if (btn != null && btn.targetPrefab != null)
            {
                basePosterTemplate = btn.targetPrefab;
            }
        }

        // Setup the '+' button at index 0 of the artwork drawer
        SetupPlusButtonInDrawer();

        // Check if there are any existing images in persistent user folder
        CheckPersistentUserFolder();

        // Display the startup upload dialog if enabled
        if (showStartupDialogOnLaunch)
        {
            CreateOrShowStartupDialog();
        }
    }

    private void CheckPersistentUserFolder()
    {
        string dir = Path.Combine(Application.persistentDataPath, "UserPosters");
        if (Directory.Exists(dir))
        {
            string[] files = Directory.GetFiles(dir, "*.*");
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    LoadAndCreatePoster(file, false);
                }
            }
        }
    }

    /// <summary>
    /// Sets up the '+' button at the very beginning of the artwork drawer content.
    /// </summary>
    public void SetupPlusButtonInDrawer()
    {
        if (ObjectListController.Instance == null || ObjectListController.Instance.buttonsContent == null) return;
        Transform content = ObjectListController.Instance.buttonsContent.transform;

        // Check if already created
        Transform existing = content.Find("AddPosterButton");
        if (existing != null)
        {
            plusDrawerButton = existing.gameObject;
            plusDrawerButton.transform.SetSiblingIndex(0);
            return;
        }

        GameObject plusBtnObj = new GameObject("AddPosterButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        plusBtnObj.transform.SetParent(content, false);
        plusBtnObj.transform.SetSiblingIndex(0);
        plusDrawerButton = plusBtnObj;

        RectTransform rt = plusBtnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(170f, 0f);

        Image img = plusBtnObj.GetComponent<Image>();
        img.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

        Button btn = plusBtnObj.GetComponent<Button>();
        btn.onClick.AddListener(OnDrawerPlusClicked);

        // Icon/Text child
        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(plusBtnObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "+\nAdd Poster";
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.35f, 0.85f, 0.65f, 1f); // fresh mint accent
        tmp.fontStyle = FontStyles.Bold;

        ObjectListController.Instance.RegisterAllButtons();
    }

    private void OnDrawerPlusClicked()
    {
        List<string> files = NativeFilePickerHelper.PickImageFiles("Select Poster Images to Add");
        if (files != null && files.Count > 0)
        {
            foreach (var file in files)
            {
                LoadAndCreatePoster(file, true);
            }
        }
    }

    /// <summary>
    /// Loads an image file, creates a custom framed poster prefab, and adds it to the drawer.
    /// </summary>
    public GameObject LoadAndCreatePoster(string filePath, bool notifyDrawer = true)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!tex.LoadImage(fileBytes))
            {
                Debug.LogWarning("CustomPosterManager: Failed to load image data from " + filePath);
                return null;
            }

            string posterTitle = Path.GetFileNameWithoutExtension(filePath);
            customPosterCounter++;
            if (string.IsNullOrEmpty(posterTitle))
            {
                posterTitle = "Custom Poster " + customPosterCounter;
            }

            GameObject customPrefab = CreateCustomPosterPrefab(tex, posterTitle);
            if (customPrefab != null)
            {
                createdCustomPrefabs.Add(customPrefab);
                AddPosterButtonToDrawer(customPrefab, tex, posterTitle, notifyDrawer);
                return customPrefab;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("CustomPosterManager: Exception loading poster: " + ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Creates a 3D framed poster template GameObject from a texture, preserving its exact aspect ratio.
    /// </summary>
    public GameObject CreateCustomPosterPrefab(Texture2D texture, string title)
    {
        if (basePosterTemplate == null)
        {
            if (ObjectListController.Instance != null && ObjectListController.Instance.buttonsContent != null)
            {
                UISelectionButton btn = ObjectListController.Instance.buttonsContent.GetComponentInChildren<UISelectionButton>();
                if (btn != null) basePosterTemplate = btn.targetPrefab;
            }
        }

        if (basePosterTemplate == null)
        {
            Debug.LogError("CustomPosterManager: No base poster template available!");
            return null;
        }

        GameObject customObj = Instantiate(basePosterTemplate);
        customObj.name = "(Prb)WallArt_CustomPoster_" + title;
        customObj.transform.SetParent(transform, false);
        customObj.SetActive(false);

        // Aspect ratio: longer side matches ISO A3 baseline (0.420m).
        // Shorter side scales proportionally so the image is never stretched or distorted.
        float longerSide = 0.420f;
        float canvasWidth, canvasHeight;

        if (texture.width >= texture.height)
        {
            // Landscape or square: width is the longer side (0.420m)
            canvasWidth = longerSide;
            canvasHeight = longerSide * ((float)texture.height / (float)texture.width);
        }
        else
        {
            // Portrait: height is the longer side (0.420m)
            canvasHeight = longerSide;
            canvasWidth = longerSide * ((float)texture.width / (float)texture.height);
        }

        // Configure ArtworkCanvas
        Transform canvasTrans = customObj.transform.Find("ArtworkCanvas");
        if (canvasTrans != null)
        {
            canvasTrans.localScale = new Vector3(canvasWidth, canvasHeight, 0.014f);
            MeshRenderer mr = canvasTrans.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material customMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                customMat.name = "Mat_Poster_" + title;
                customMat.SetTexture("_BaseMap", texture);
                customMat.color = Color.white;
                customMat.SetFloat("_Smoothness", 0.25f);
                mr.material = customMat;
            }
        }

        // Configure OuterFrame
        Transform frameTrans = customObj.transform.Find("OuterFrame");
        if (frameTrans != null)
        {
            frameTrans.localScale = new Vector3(canvasWidth + 0.040f, canvasHeight + 0.040f, 0.016f);
        }

        // Configure BoxCollider
        BoxCollider boxCol = customObj.GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            boxCol.size = new Vector3(canvasWidth + 0.040f, canvasHeight + 0.040f, 0.018f);
            boxCol.center = new Vector3(0f, 0f, 0.009f);
        }

        // Configure ARInteractable
        ARInteractable interactable = customObj.GetComponent<ARInteractable>();
        if (interactable != null)
        {
            interactable.objectName = "CustomPoster_" + title;
            interactable.AnchorPointOfObject = Vector3.zero;
            interactable.NormalDirectionOfObject = new Vector3(0f, 0f, 1f);
        }

        // Configure ArtworkProperties
        ArtworkProperties props = customObj.GetComponent<ArtworkProperties>();
        if (props == null)
        {
            props = customObj.AddComponent<ArtworkProperties>();
        }
        props.currentPreset = ArtworkSizePreset.A3;
        props.currentGeneralPreset = GeneralSizePreset.Medium;
        props.baseDimensionsMeters = new Vector2(canvasWidth, canvasHeight);
        props.artworkTitle = title;
        props.artist = "Custom Artwork";
        props.currentOrientation = (canvasWidth > canvasHeight) ? ArtworkOrientation.Landscape : ArtworkOrientation.Portrait;

        return customObj;
    }

    /// <summary>
    /// Adds a thumbnail button for the custom poster right at the start of the artwork drawer.
    /// Sibling index 1 puts it right after the '+' Add button, before any default presets.
    /// </summary>
    private void AddPosterButtonToDrawer(GameObject customPrefab, Texture2D texture, string title, bool notifyDrawer = true)
    {
        if (ObjectListController.Instance == null || ObjectListController.Instance.buttonsContent == null) return;
        Transform content = ObjectListController.Instance.buttonsContent.transform;

        // Clone an existing artwork button from content to preserve UI styling and components
        GameObject templateButton = null;
        for (int i = 0; i < content.childCount; i++)
        {
            UISelectionButton btn = content.GetChild(i).GetComponent<UISelectionButton>();
            if (btn != null)
            {
                templateButton = content.GetChild(i).gameObject;
                break;
            }
        }

        GameObject newBtn;
        if (templateButton != null)
        {
            newBtn = Instantiate(templateButton, content);

            // Strip any text components or labels from cloned button so no custom button ever displays "Starry"
            foreach (var tmpTxt in newBtn.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Destroy(tmpTxt.gameObject);
            }
            foreach (var legTxt in newBtn.GetComponentsInChildren<Text>(true))
            {
                Destroy(legTxt.gameObject);
            }
            Transform lbl = newBtn.transform.Find("ArtworkLabel");
            if (lbl != null)
            {
                Destroy(lbl.gameObject);
            }
        }
        else
        {
            newBtn = new GameObject("Button_" + title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(UISelectionButton));
            newBtn.transform.SetParent(content, false);
        }

        newBtn.name = "Button_Custom_" + title;
        RectTransform btnRt = newBtn.GetComponent<RectTransform>();
        if (btnRt != null)
        {
            btnRt.sizeDelta = new Vector2(220f, 0f);
        }

        // Insert at start of artwork list (index 1 is right after '+' button; if '+' not present, index 0)
        int insertIndex = (plusDrawerButton != null && plusDrawerButton.transform.parent == content) ? 1 : 0;
        newBtn.transform.SetSiblingIndex(insertIndex);

        // Set thumbnail sprite
        Sprite thumbSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        Image img = newBtn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = thumbSprite;
            img.color = Color.white;
            img.preserveAspect = true;
        }

        // Configure UISelectionButton
        UISelectionButton sel = newBtn.GetComponent<UISelectionButton>();
        if (sel == null)
        {
            sel = newBtn.AddComponent<UISelectionButton>();
        }
        sel.targetPrefab = customPrefab;
        sel.objectListController = ObjectListController.Instance;

        if (notifyDrawer)
        {
            ObjectListController.Instance.RegisterAllButtons();
        }
    }

    /// <summary>
    /// Displays or creates the initial modal startup upload dialog.
    /// </summary>
    public void CreateOrShowStartupDialog()
    {
        if (startupDialogObject != null)
        {
            startupDialogObject.SetActive(true);
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Full screen darkened overlay
        startupDialogObject = new GameObject("StartupPosterUploadDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        startupDialogObject.transform.SetParent(canvas.transform, false);

        RectTransform overlayRt = startupDialogObject.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;

        Image overlayImg = startupDialogObject.GetComponent<Image>();
        overlayImg.color = new Color(0.05f, 0.07f, 0.10f, 0.94f);

        // Center Card Dialog Panel
        GameObject cardObj = new GameObject("DialogCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(startupDialogObject.transform, false);

        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(580f, 520f);
        cardRt.anchoredPosition = Vector2.zero;

        Image cardImg = cardObj.GetComponent<Image>();
        cardImg.color = new Color(0.12f, 0.15f, 0.20f, 0.98f);

        // Title
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -30f);
        titleRt.sizeDelta = new Vector2(520f, 60f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "XR Wall Art Studio";
        titleTmp.fontSize = 32;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;

        // Subtitle
        GameObject subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 1f);
        subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -95f);
        subRt.sizeDelta = new Vector2(500f, 60f);

        TextMeshProUGUI subTmp = subObj.GetComponent<TextMeshProUGUI>();
        subTmp.text = "Upload poster images from your device to test on your walls in realistic AR scale.";
        subTmp.fontSize = 18;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.75f, 0.80f, 0.88f, 1f);

        // Upload Button (prominent)
        GameObject uploadBtnObj = new GameObject("UploadButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        uploadBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform uploadRt = uploadBtnObj.GetComponent<RectTransform>();
        uploadRt.anchorMin = new Vector2(0.5f, 0.5f);
        uploadRt.anchorMax = new Vector2(0.5f, 0.5f);
        uploadRt.anchoredPosition = new Vector2(0f, 40f);
        uploadRt.sizeDelta = new Vector2(340f, 65f);

        Image uploadImg = uploadBtnObj.GetComponent<Image>();
        uploadImg.color = new Color(0.18f, 0.65f, 0.45f, 1f); // vibrant emerald green

        Button uploadBtn = uploadBtnObj.GetComponent<Button>();
        uploadBtn.onClick.AddListener(OnStartupUploadClicked);

        GameObject uploadLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        uploadLabelObj.transform.SetParent(uploadBtnObj.transform, false);
        RectTransform uploadLabelRt = uploadLabelObj.GetComponent<RectTransform>();
        uploadLabelRt.anchorMin = Vector2.zero;
        uploadLabelRt.anchorMax = Vector2.one;
        uploadLabelRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI uploadLabelTmp = uploadLabelObj.GetComponent<TextMeshProUGUI>();
        uploadLabelTmp.text = "+ Browse / Upload Images";
        uploadLabelTmp.fontSize = 20;
        uploadLabelTmp.alignment = TextAlignmentOptions.Center;
        uploadLabelTmp.fontStyle = FontStyles.Bold;
        uploadLabelTmp.color = Color.white;

        // Status / preview info text
        GameObject statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObj.transform.SetParent(cardObj.transform, false);
        RectTransform statusRt = statusObj.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0.5f, 0.5f);
        statusRt.anchorMax = new Vector2(0.5f, 0.5f);
        statusRt.anchoredPosition = new Vector2(0f, -35f);
        statusRt.sizeDelta = new Vector2(480f, 55f);

        startupStatusText = statusObj.GetComponent<TextMeshProUGUI>();
        startupStatusText.text = "Select one or more .png / .jpg images (Ctrl+Click to choose multiple).";
        startupStatusText.fontSize = 15;
        startupStatusText.alignment = TextAlignmentOptions.Center;
        startupStatusText.color = new Color(0.65f, 0.70f, 0.75f, 1f);

        // Bottom Action: Start in AR Button
        GameObject startBtnObj = new GameObject("StartButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        startBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform startRt = startBtnObj.GetComponent<RectTransform>();
        startRt.anchorMin = new Vector2(0.5f, 0f);
        startRt.anchorMax = new Vector2(0.5f, 0f);
        startRt.pivot = new Vector2(0.5f, 0f);
        startRt.anchoredPosition = new Vector2(0f, 85f);
        startRt.sizeDelta = new Vector2(340f, 55f);

        Image startImg = startBtnObj.GetComponent<Image>();
        startImg.color = new Color(0.25f, 0.45f, 0.85f, 1f); // modern blue

        startupContinueButton = startBtnObj.GetComponent<Button>();
        startupContinueButton.onClick.AddListener(OnStartupContinueClicked);

        GameObject startLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        startLabelObj.transform.SetParent(startBtnObj.transform, false);
        RectTransform startLabelRt = startLabelObj.GetComponent<RectTransform>();
        startLabelRt.anchorMin = Vector2.zero;
        startLabelRt.anchorMax = Vector2.one;
        startLabelRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI startLabelTmp = startLabelObj.GetComponent<TextMeshProUGUI>();
        startLabelTmp.text = "Enter AR / View on Wall";
        startLabelTmp.fontSize = 18;
        startLabelTmp.alignment = TextAlignmentOptions.Center;
        startLabelTmp.fontStyle = FontStyles.Bold;
        startLabelTmp.color = Color.white;

        // Bottom Action: Skip Button
        GameObject skipBtnObj = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        skipBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform skipRt = skipBtnObj.GetComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(0.5f, 0f);
        skipRt.anchorMax = new Vector2(0.5f, 0f);
        skipRt.pivot = new Vector2(0.5f, 0f);
        skipRt.anchoredPosition = new Vector2(0f, 25f);
        skipRt.sizeDelta = new Vector2(340f, 45f);

        Image skipImg = skipBtnObj.GetComponent<Image>();
        skipImg.color = new Color(0.18f, 0.20f, 0.25f, 0.7f);

        Button skipBtn = skipBtnObj.GetComponent<Button>();
        skipBtn.onClick.AddListener(OnStartupSkipClicked);

        GameObject skipLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        skipLabelObj.transform.SetParent(skipBtnObj.transform, false);
        RectTransform skipLabelRt = skipLabelObj.GetComponent<RectTransform>();
        skipLabelRt.anchorMin = Vector2.zero;
        skipLabelRt.anchorMax = Vector2.one;
        skipLabelRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI skipLabelTmp = skipLabelObj.GetComponent<TextMeshProUGUI>();
        skipLabelTmp.text = "Skip (Use Default Artworks)";
        skipLabelTmp.fontSize = 15;
        skipLabelTmp.alignment = TextAlignmentOptions.Center;
        skipLabelTmp.color = new Color(0.65f, 0.70f, 0.75f, 1f);
    }

    private void OnStartupUploadClicked()
    {
        List<string> files = NativeFilePickerHelper.PickImageFiles("Select Startup Poster Images (Multi-select supported)");
        if (files != null && files.Count > 0)
        {
            foreach (var f in files)
            {
                if (!pendingStartupFiles.Contains(f))
                {
                    pendingStartupFiles.Add(f);
                }
            }

            if (startupStatusText != null)
            {
                startupStatusText.text = $"✓ {pendingStartupFiles.Count} poster image(s) ready to load!";
                startupStatusText.color = new Color(0.35f, 0.90f, 0.60f, 1f);
            }
        }
    }

    private void OnStartupContinueClicked()
    {
        if (pendingStartupFiles.Count > 0)
        {
            foreach (var f in pendingStartupFiles)
            {
                LoadAndCreatePoster(f, false);
            }
            if (ObjectListController.Instance != null)
            {
                ObjectListController.Instance.RegisterAllButtons();
            }
        }

        CloseStartupDialog();
    }

    private void OnStartupSkipClicked()
    {
        CloseStartupDialog();
    }

    public void CloseStartupDialog()
    {
        if (startupDialogObject != null)
        {
            startupDialogObject.SetActive(false);
        }
    }
}
