using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


[ExecuteInEditMode]
public class DisableGizmos : MonoBehaviour
{
    // Start is called before the first frame update
    void Awake()
    {
#if UNITY_EDITOR
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.drawGizmos = false;
        }
#endif
    }

}
