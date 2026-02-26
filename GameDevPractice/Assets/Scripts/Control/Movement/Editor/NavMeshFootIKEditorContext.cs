#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TH.Control.Movement.Editor
{
    public static class NavMeshFootIKEditorContext
    {
        private const string EnvironmentLayerName = "Environment";
        private const string ToolsMenuPath = "Tools/Control/Foot IK/Configure Environment MeshColliders";

        [MenuItem(ToolsMenuPath)]
        private static void ConfigureEnvironmentMeshColliders()
        {
            int environmentLayer = LayerMask.NameToLayer(EnvironmentLayerName);
            if (environmentLayer < 0)
            {
                Debug.LogWarning($"[{nameof(NavMeshFootIKEditorContext)}] Layer '{EnvironmentLayerName}' was not found.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError($"[{nameof(NavMeshFootIKEditorContext)}] Active scene is invalid.");
                return;
            }

            int addedCount = 0;
            int updatedCount = 0;
            int skippedNoMeshCount = 0;

            foreach (GameObject root in activeScene.GetRootGameObjects())
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform current in transforms)
                {
                    if (current == null || current.gameObject.layer != environmentLayer)
                    {
                        continue;
                    }

                    var meshFilter = current.GetComponent<MeshFilter>();
                    Mesh sharedMesh = meshFilter != null ? meshFilter.sharedMesh : null;
                    if (sharedMesh == null)
                    {
                        skippedNoMeshCount++;
                        continue;
                    }

                    var meshCollider = current.GetComponent<MeshCollider>();
                    if (meshCollider == null)
                    {
                        meshCollider = Undo.AddComponent<MeshCollider>(current.gameObject);
                        addedCount++;
                    }
                    else
                    {
                        Undo.RecordObject(meshCollider, "Configure Environment MeshCollider");
                        updatedCount++;
                    }

                    meshCollider.sharedMesh = sharedMesh;
                    meshCollider.convex = false;
                    meshCollider.isTrigger = false;
                    EditorUtility.SetDirty(meshCollider);
                }
            }

            if (addedCount > 0 || updatedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log($"[{nameof(NavMeshFootIKEditorContext)}] Scene '{activeScene.name}': added={addedCount}, updated={updatedCount}, skippedNoMesh={skippedNoMeshCount}");
        }
    }
}
#endif
