using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectListController : MonoBehaviour
{
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


    // Start is called before the first frame update
    void Start()
    {
        ScreenRayInteractor.Instance.isSpawnerActivated = false;
        screenHeight = Screen.height;
        screenWidth = Screen.width;
        closedPosition = -itemList.rect.height/2;
        openPosition = itemList.rect.height/2;
        itemListController.anchoredPosition = new Vector2(0, closedPosition);
        RegisterAllButtons();
    }


    void RegisterAllButtons() {
        buttons.Clear();
        for (int i = 0; i < buttonsContent.transform.childCount; i++) {
            GameObject button = buttonsContent.transform.GetChild(i).gameObject;
            UISelectionButton selectBtn = button.GetComponent<UISelectionButton>();
            if (selectBtn != null) {
                selectBtn.objectListController = this;
            }
            buttons.Add(button);
        }
        if (buttons.Count > 0) {
            float buttonWidth = buttons[0].GetComponent<RectTransform>().rect.width;
            float contentWidth = buttonWidth * buttons.Count;
            buttonsContent.GetComponent<RectTransform>().sizeDelta = new Vector2(contentWidth, buttonsContent.GetComponent<RectTransform>().rect.height);
        }
    }

    IEnumerator MoveItemList(float startPosition, float endPosition) {
        float elapsedTime = 0;
        while (elapsedTime < animationDuration) {
            itemListController.anchoredPosition = new Vector2(0, Mathf.Lerp(startPosition, endPosition, elapsedTime / animationDuration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        itemListController.anchoredPosition = new Vector2(0, endPosition);
    }

    // Update is called once per frame
    void Update()
    {
        if (currentSelectedIndex != -1) {
            foreach (var button in buttons) {
                button.GetComponent<Image>().color = Color.white;
            }
            buttons[currentSelectedIndex].GetComponent<Image>().color = Color.green;
        } else {
            foreach (var button in buttons) {
                button.GetComponent<Image>().color = Color.white;
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
    public void DisableItemList() {
        isDrawerOpen = false;
        StopAllCoroutines();
        StartCoroutine(MoveItemList(itemListController.anchoredPosition.y, closedPosition));
        currentSelectedIndex = -1;
        if (ScreenRayInteractor.Instance != null) {
            ScreenRayInteractor.Instance.currentChosenPrefab = null;
            ScreenRayInteractor.Instance.isSpawnerActivated = false;
        }
    }

    public void ToggleItemList() {
        if (!isDrawerOpen) {
            EnableItemList();
        } else {
            DisableItemList();
        }
    }
}
