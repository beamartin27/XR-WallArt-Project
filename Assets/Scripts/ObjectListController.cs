using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectListController : MonoBehaviour
{
    public static ObjectListController Instance { get; private set; }

    public RectTransform itemList;

    public RectTransform itemListController;

    public float animationDuration = 0.5f;
    private int screenHeight;
    private int screenWidth;

    private float closedPosition;
    private float openPosition;

    public GameObject buttonsContent;
    private List<GameObject> buttons = new List<GameObject>();

    public int currentSelectedIndex = -1;
    private bool isDrawerOpen = false;
    public bool IsDrawerOpen => isDrawerOpen;
    private float lastToggleTime = 0f;

    // Tiles that pick an artwork (parallel to "buttons"; null for other tiles such as "Add photo")
    private List<Image> selectableImages = new List<Image>();

    // Extra travel so the open drawer clears the phone's home indicator, and so the closed
    // drawer (including any backdrop that sticks out above the list) is fully off screen.
    private float bottomInset = 0f;
    private float hiddenExtra = 0f;
    private bool positionsReady = false;

    /// <summary>Tiles not chosen are dimmed to this colour while one is armed for placement.</summary>
    public static readonly Color DimmedTile = new Color(0.45f, 0.45f, 0.45f, 1f);

    /// <summary>Canvas-space y of the top edge of the artwork list (0 = bottom of the screen).</summary>
    public float ListTopY {
        get {
            float h = (itemList != null && itemList.rect.height > 0) ? itemList.rect.height : 360f;
            return itemListController.anchoredPosition.y + h / 2f;
        }
    }

    public void SetInsets(float bottomInset, float hiddenExtra) {
        this.bottomInset = Mathf.Max(0f, bottomInset);
        this.hiddenExtra = Mathf.Max(0f, hiddenExtra);
        if (positionsReady) {
            ComputePositions();
            itemListController.anchoredPosition = new Vector2(0, isDrawerOpen ? openPosition : closedPosition);
        }
    }

    private void ComputePositions() {
        float h = (itemList != null && itemList.rect.height > 0) ? itemList.rect.height : 360f;
        closedPosition = -h / 2f - hiddenExtra;
        openPosition = h / 2f + bottomInset;
        positionsReady = true;
    }

    void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        ScreenRayInteractor.Instance.isSpawnerActivated = false;
        screenHeight = Screen.height;
        screenWidth = Screen.width;
        ComputePositions();
        itemListController.anchoredPosition = new Vector2(0, closedPosition);
        RegisterAllButtons();
    }

    public void RegisterAllButtons() {
        buttons.Clear();
        selectableImages.Clear();
        float contentWidth = 0f;
        int visibleCount = 0;
        for (int i = 0; i < buttonsContent.transform.childCount; i++) {
            GameObject button = buttonsContent.transform.GetChild(i).gameObject;
            UISelectionButton selectBtn = button.GetComponent<UISelectionButton>();

            // Only artwork tiles are wired here; other tiles (e.g. "Add photo") manage themselves
            if (selectBtn != null) {
                selectBtn.objectListController = this;
                Button btn = button.GetComponent<Button>();
                if (btn != null) {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(selectBtn.SelectArtwork);
                }
            }

            buttons.Add(button);
            selectableImages.Add(selectBtn != null ? button.GetComponent<Image>() : null);
            if (button.activeSelf) {
                contentWidth += button.GetComponent<RectTransform>().rect.width;
                visibleCount++;
            }
        }
        if (buttons.Count > 0) {
            HorizontalLayoutGroup layout = buttonsContent.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) {
                contentWidth += layout.padding.left + layout.padding.right + layout.spacing * Mathf.Max(0, visibleCount - 1);
            }
            RectTransform contentRect = buttonsContent.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(contentWidth, contentRect.sizeDelta.y);
        }
    }

    public void ClearSelectionHighlight() {
        currentSelectedIndex = -1;
        foreach (Image img in selectableImages) {
            if (img != null) {
                img.color = Color.white;
            }
        }
    }

    IEnumerator MoveItemList(float startPosition, float endPosition, System.Action onComplete = null) {
        float elapsedTime = 0;
        while (elapsedTime < animationDuration) {
            // Ease out: fast at first, settling gently
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsedTime / animationDuration), 3f);
            itemListController.anchoredPosition = new Vector2(0, Mathf.Lerp(startPosition, endPosition, t));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        itemListController.anchoredPosition = new Vector2(0, endPosition);
        onComplete?.Invoke();
    }

    // Update is called once per frame
    void Update()
    {
        // The tile armed for placement stays bright; the others dim so the choice is obvious
        // (tinting the tile itself would discolour the user's photos).
        bool anySelected = currentSelectedIndex >= 0 && currentSelectedIndex < selectableImages.Count
            && selectableImages[currentSelectedIndex] != null;
        for (int i = 0; i < selectableImages.Count; i++) {
            Image img = selectableImages[i];
            if (img == null) continue;
            Color target = (!anySelected || i == currentSelectedIndex) ? Color.white : DimmedTile;
            if (img.color != target) img.color = target;
        }
    }
    public void EnableItemList() {
        isDrawerOpen = true;
        StopAllCoroutines();
        StartCoroutine(MoveItemList(itemListController.anchoredPosition.y, openPosition));
        // Keep current selection intact so artwork can be replaced if selected
        if (ScreenRayInteractor.Instance != null && ScreenRayInteractor.Instance.currentSelectedObject == null) {
            ScreenRayInteractor.Instance.isSpawnerActivated = true;
        }
    }
    public void DisableItemList(System.Action onComplete = null) {
        isDrawerOpen = false;
        StopAllCoroutines();
        StartCoroutine(MoveItemList(itemListController.anchoredPosition.y, closedPosition, onComplete));
        ClearSelectionHighlight();
        if (ScreenRayInteractor.Instance != null) {
            ScreenRayInteractor.Instance.CancelPlacementPreview();
            ScreenRayInteractor.Instance.currentChosenPrefab = null;
            ScreenRayInteractor.Instance.isSpawnerActivated = false;
        }
    }

    public void ToggleItemList() {
        if (Time.unscaledTime - lastToggleTime < 0.25f) return;
        lastToggleTime = Time.unscaledTime;

        if (!isDrawerOpen) {
            EnableItemList();
        } else {
            DisableItemList();
        }
    }
}
