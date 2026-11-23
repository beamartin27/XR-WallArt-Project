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
        if (ScreenRayInteractor.Instance == null || ScreenRayInteractor.Instance.outlineMaterial == null) return;
        string outlineMatName = ScreenRayInteractor.Instance.outlineMaterial.name;

        foreach (var renderer in GetComponentsInChildren<Renderer>()) {
            Material[] currentMaterials = renderer.materials;
            List<Material> cleanedMaterials = new List<Material>();

            // Completely strip any existing outline material instances
            for (int i = 0; i < currentMaterials.Length; i++) {
                if (currentMaterials[i] != null && !currentMaterials[i].name.StartsWith(outlineMatName)) {
                    cleanedMaterials.Add(currentMaterials[i]);
                }
            }

            if (isEmission) {
                cleanedMaterials.Add(ScreenRayInteractor.Instance.outlineMaterial);
            }

            renderer.materials = cleanedMaterials.ToArray();
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
