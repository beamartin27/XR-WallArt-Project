using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UISelectionButton : MonoBehaviour
{
    public GameObject targetPrefab;
    public ObjectListController objectListController;


    public void SetTargetPrefab(int  i) {
        ScreenRayInteractor.Instance.SetTargetPrefab(targetPrefab);
        objectListController.currentSelectedIndex = i;
    }
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
