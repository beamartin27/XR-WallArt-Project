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

        // Ensure Viewport rect stretches properly to parent bounds
        if (itemList != null)
        {
            ScrollRect sr = itemList.GetComponent<ScrollRect>();
            if (sr != null && sr.viewport != null)
            {
                sr.viewport.anchorMin = Vector2.zero;
                sr.viewport.anchorMax = Vector2.one;
                sr.viewport.sizeDelta = Vector2.zero;
                sr.viewport.anchoredPosition = Vector2.zero;
            }
        }

        // Ensure HorizontalLayoutGroup has clean padding and alignment
        if (buttonsContent != null)
        {
            HorizontalLayoutGroup hlg = buttonsContent.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.padding = new RectOffset(15, 15, 15, 15);
                hlg.spacing = 20f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
            }
        }

        float h = (itemList != null && itemList.rect.height > 0) ? itemList.rect.height : 360f;
        closedPosition = -h / 2f;
        openPosition = h / 2f + 15f; // Lift slightly above bottom edge for clean margin
        itemListController.anchoredPosition = new Vector2(0, closedPosition);
        RegisterAllButtons();
    }

    public int GetArtworkButtonIndex(GameObject btnObject) {
        return buttons.IndexOf(btnObject);
    }

    public void RegisterAllButtons() {
        buttons.Clear();

        // Ensure no buttons have leftover "Starry" labels or distorted aspect ratios
        for (int i = 0; i < buttonsContent.transform.childCount; i++) {
            GameObject child = buttonsContent.transform.GetChild(i).gameObject;

            // Remove or disable any "ArtworkLabel" or "Starry" text
            Transform label = child.transform.Find("ArtworkLabel");
            if (label != null) {
                label.gameObject.SetActive(false);
            }
            foreach (var tmp in child.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) {
                if (child.name != "AddPosterButton" && tmp.text.IndexOf("starry", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                    tmp.gameObject.SetActive(false);
                }
            }
            foreach (var txt in child.GetComponentsInChildren<Text>(true)) {
                if (child.name != "AddPosterButton" && txt.text.IndexOf("starry", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                    txt.gameObject.SetActive(false);
                }
            }

            UISelectionButton selectBtn = child.GetComponent<UISelectionButton>();
            if (selectBtn == null) {
                // Non-artwork child (e.g. '+' AddPosterButton) - skip artwork registration
                continue;
            }

            // Ensure artwork thumbnail preserves aspect ratio without stretching
            Image img = child.GetComponent<Image>();
            if (img != null) {
                img.preserveAspect = true;
            }
            RectTransform childRt = child.GetComponent<RectTransform>();
            if (childRt != null) {
                childRt.sizeDelta = new Vector2(220f, 0f);
            }

            int artworkIndex = buttons.Count;
            selectBtn.objectListController = this;

            Button btn = child.GetComponent<Button>();
            if (btn != null) {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    selectBtn.SetTargetPrefab(artworkIndex);
                });
            }

            buttons.Add(child);
        }

        if (buttonsContent.transform.childCount > 0) {
            float totalWidth = 0f;
            for (int i = 0; i < buttonsContent.transform.childCount; i++) {
                RectTransform rt = buttonsContent.transform.GetChild(i).GetComponent<RectTransform>();
                if (rt != null) {
                    totalWidth += (rt.sizeDelta.x > 0 ? rt.sizeDelta.x : 220f) + 20f;
                }
            }
            buttonsContent.GetComponent<RectTransform>().sizeDelta = new Vector2(totalWidth + 120f, buttonsContent.GetComponent<RectTransform>().rect.height);
        }
    }

    public void ClearSelectionHighlight() {
        currentSelectedIndex = -1;
        if (buttons != null) {
            foreach (var button in buttons) {
                if (button != null) {
                    Image img = button.GetComponent<Image>();
                    if (img != null) {
                        img.color = Color.white;
                    }
                }
            }
        }
    }

    IEnumerator MoveItemList(float startPosition, float endPosition, System.Action onComplete = null) {
        float elapsedTime = 0;
        while (elapsedTime < animationDuration) {
            itemListController.anchoredPosition = new Vector2(0, Mathf.Lerp(startPosition, endPosition, elapsedTime / animationDuration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        itemListController.anchoredPosition = new Vector2(0, endPosition);
        onComplete?.Invoke();
    }

    // Update is called once per frame
    void Update()
    {
        if (currentSelectedIndex >= 0 && currentSelectedIndex < buttons.Count) {
            for (int i = 0; i < buttons.Count; i++) {
                if (buttons[i] != null) {
                    Image img = buttons[i].GetComponent<Image>();
                    if (img != null) {
                        img.color = (i == currentSelectedIndex) ? Color.green : Color.white;
                    }
                }
            }
        } else {
            foreach (var button in buttons) {
                if (button != null) {
                    Image img = button.GetComponent<Image>();
                    if (img != null) {
                        img.color = Color.white;
                    }
                }
            }
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
