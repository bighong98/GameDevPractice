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
        private const string SnapAllEnemiesMenuPath = "Tools/Control/NavMesh/Snap All Scene Enemies To NavMesh";
        private const float SampleDistance = 4f;

        [MenuItem(SnapAllEnemiesMenuPath)]
        private static void SnapAllSceneEnemiesToNavMesh()
        {
            EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int total = 0;
            int snapped = 0;
            int skippedAlreadyOnNavMesh = 0;
            int skippedNoAgent = 0;
            int failed = 0;
            var dirtyScenes = new HashSet<Scene>();

            foreach (EnemyController enemy in enemies)
            {
                if (!IsSceneEnemyInstance(enemy))
                    continue;

                total++;

                if (!enemy.TryGetComponent<NavMeshAgent>(out var agent) || agent == null)
                {
                    skippedNoAgent++;
                    continue;
                }

                if (agent.isOnNavMesh)
                {
                    skippedAlreadyOnNavMesh++;
                    continue;
                }

                Vector3 origin = enemy.transform.position;
                if (!NavMesh.SamplePosition(origin, out var hit, SampleDistance, NavMesh.AllAreas))
                {
                    failed++;
                    Debug.LogWarning($"[{nameof(EnemyNavMeshTools)}] Failed to sample NavMesh near '{enemy.name}' (distance={SampleDistance}).", enemy);
                    continue;
                }

                Undo.RecordObject(enemy.transform, "Snap Enemy To NavMesh");
                enemy.transform.position = hit.position;
                EditorUtility.SetDirty(enemy.transform);

                if (enemy.gameObject.scene.IsValid())
                    dirtyScenes.Add(enemy.gameObject.scene);

                snapped++;
            }

            foreach (Scene scene in dirtyScenes)
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[{nameof(EnemyNavMeshTools)}] Completed. total={total}, snapped={snapped}, skippedAlreadyOnNavMesh={skippedAlreadyOnNavMesh}, skippedNoAgent={skippedNoAgent}, failed={failed}");
        }

        [MenuItem(SnapAllEnemiesMenuPath, true)]
        private static bool ValidateSnapAllSceneEnemiesToNavMesh()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static bool IsSceneEnemyInstance(EnemyController enemy)
        {
            if (enemy == null)
                return false;

            if (EditorUtility.IsPersistent(enemy))
                return false;

            if (PrefabStageUtility.GetPrefabStage(enemy.gameObject) != null)
                return false;

            Scene scene = enemy.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }
    }
}
#endif
