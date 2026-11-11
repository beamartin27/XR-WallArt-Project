using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class RotationButton : MonoBehaviour, IPointerDownHandler, IPointerExitHandler, IPointerUpHandler {

    public bool isDown = false;


    // Use this for initialization
    void Start() {

    }

    // Update is called once per frame
    void Update() {
        if (isDown) {
            TouchScreenInputManager.Instance.isDoingRotate = true;
        } else {
            TouchScreenInputManager.Instance.isDoingRotate = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData) {
        isDown = true;
        //Debug.Log("Button Down");
    }

    public void OnPointerExit(PointerEventData eventData) {
        isDown = false;
        //Debug.Log("Button Exit");
    }

    public void OnPointerUp(PointerEventData eventData) {
        isDown = false;
        //Debug.Log("Button Up");
    }
}