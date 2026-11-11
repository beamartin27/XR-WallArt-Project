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
        for (int i = 0; i < buttonsContent.transform.childCount; i++) {
            GameObject button = buttonsContent.transform.GetChild(i).gameObject;
            button.GetComponent<UISelectionButton>().objectListController = this;
            buttons.Add(button);
        }
        float buttonWidth = buttons[0].GetComponent<RectTransform>().rect.width;
        float ContentWidth = buttonWidth * buttons.Count;
        buttonsContent.GetComponent<RectTransform>().sizeDelta = new Vector2(ContentWidth, buttonsContent.GetComponent<RectTransform>().rect.height);
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
    void EnableItemList() {
        StartCoroutine(MoveItemList(closedPosition, openPosition));
        ScreenRayInteractor.Instance.ClearSelection();
        ScreenRayInteractor.Instance.isSpawnerActivated = true;
    }
    void DisableItemList() {
        StartCoroutine(MoveItemList(openPosition, closedPosition));
        currentSelectedIndex = -1;
        ScreenRayInteractor.Instance.currentChosenPrefab = null;
        ScreenRayInteractor.Instance.isSpawnerActivated = false;
    }

    public void ToggleItemList() {
        if (ScreenRayInteractor.Instance.isSpawnerActivated == false) {
            EnableItemList();
        } else {
            DisableItemList();
        }
    }
}
