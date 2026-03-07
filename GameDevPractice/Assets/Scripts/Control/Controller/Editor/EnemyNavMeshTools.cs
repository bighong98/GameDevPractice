#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace TH.Control.Editor
{
    public static class EnemyNavMeshTools
    {
        private const string SnapAllEnemiesMenuPath = "Tools/Control/NavMesh/Snap All Scene Agents To NavMesh";
        private const float SampleDistance = 4f;

        [MenuItem(SnapAllEnemiesMenuPath)]
        private static void SnapAllSceneAgentsToNavMesh()
        {
            NavMeshAgent[] agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int total = 0;
            int snapped = 0;
            int skippedAlreadyOnNavMesh = 0;
            int failed = 0;
            var dirtyScenes = new HashSet<Scene>();

            foreach (var agent in agents)
            {
                if (!IsSceneInstance(agent))
                    continue;

                total++;

                if (agent.isOnNavMesh)
                {
                    skippedAlreadyOnNavMesh++;
                    continue;
                }

                Vector3 origin = agent.transform.position;
                if (!NavMesh.SamplePosition(origin, out var hit, SampleDistance, NavMesh.AllAreas))
                {
                    failed++;
                    Debug.LogWarning($"[{nameof(EnemyNavMeshTools)}] Failed to sample NavMesh near '{agent.name}' (distance={SampleDistance}).", agent);
                    continue;
                }

                Undo.RecordObject(agent.transform, "Snap Agent To NavMesh");
                agent.transform.position = hit.position;
                EditorUtility.SetDirty(agent.transform);

                if (agent.gameObject.scene.IsValid())
                    dirtyScenes.Add(agent.gameObject.scene);

                snapped++;
            }

            foreach (Scene scene in dirtyScenes)
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[{nameof(EnemyNavMeshTools)}] Completed. total={total}, snapped={snapped}, skippedAlreadyOnNavMesh={skippedAlreadyOnNavMesh}, failed={failed}");
        }

        [MenuItem(SnapAllEnemiesMenuPath, true)]
        private static bool ValidateSnapAllSceneAgentsToNavMesh()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static bool IsSceneInstance(NavMeshAgent agent)
        {
            if (agent == null)
                return false;

            if (EditorUtility.IsPersistent(agent))
                return false;

            if (PrefabStageUtility.GetPrefabStage(agent.gameObject) != null)
                return false;

            Scene scene = agent.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }
    }
}
#endif
