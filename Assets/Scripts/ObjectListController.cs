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
        float h = (itemList != null && itemList.rect.height > 0) ? itemList.rect.height : 360f;
        closedPosition = -h / 2f;
        openPosition = h / 2f;
        itemListController.anchoredPosition = new Vector2(0, closedPosition);
        RegisterAllButtons();
    }

    public void RegisterAllButtons() {
        buttons.Clear();
        for (int i = 0; i < buttonsContent.transform.childCount; i++) {
            int buttonIndex = i;
            GameObject button = buttonsContent.transform.GetChild(i).gameObject;
            UISelectionButton selectBtn = button.GetComponent<UISelectionButton>();
            if (selectBtn != null) {
                selectBtn.objectListController = this;
            }

            Button btn = button.GetComponent<Button>();
            if (btn != null) {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    if (selectBtn != null) {
                        selectBtn.SetTargetPrefab(buttonIndex);
                    }
                });
            }

            buttons.Add(button);
        }
        if (buttons.Count > 0) {
            float buttonWidth = buttons[0].GetComponent<RectTransform>().rect.width;
            float contentWidth = buttonWidth * buttons.Count;
            buttonsContent.GetComponent<RectTransform>().sizeDelta = new Vector2(contentWidth, buttonsContent.GetComponent<RectTransform>().rect.height);
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
