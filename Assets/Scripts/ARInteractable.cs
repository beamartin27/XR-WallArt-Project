using System.Collections;
using System.Collections.Generic;

using UnityEngine;


public class ARInteractable : MonoBehaviour
{
    public Vector3 AnchorPointOfObject;  // anchor point of a virtual object for plane registration
    public Vector3 NormalDirectionOfObject;  // normal vector of a virtual object for plane registration

    [HideInInspector]
    public Vector3 currentAnchorPointInWorld; // anchor point of a plane in the real world for plane registration
    [HideInInspector]
    public Vector3 currentNormalDirectionInWorld;  // normal vector of a plane in the real world for plane registration

    public GameObject currentARPlane;  // a plane in the real world

    public string objectName;

    public void SetSelfEmission(bool isEmission) {
        foreach (var renderer in GetComponentsInChildren<Renderer>()) {
            if (isEmission) {
                Material[] originalMaterials = renderer.materials;
                Material[] materialsWithOutline = new Material[originalMaterials.Length + 1];
                for (int i = 0; i < originalMaterials.Length; i++) {
                    materialsWithOutline[i] = originalMaterials[i];
                }
                materialsWithOutline[originalMaterials.Length] = ScreenRayInteractor.Instance.outlineMaterial;
                renderer.materials = materialsWithOutline;
            } else {
                Material[] originalMaterials = renderer.materials;
                Material[] materialsWithoutOutline = new Material[originalMaterials.Length - 1];
                for (int i = 0; i < originalMaterials.Length - 1; i++) {
                    materialsWithoutOutline[i] = originalMaterials[i];
                }
                renderer.materials = materialsWithoutOutline;
            }
        }
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
