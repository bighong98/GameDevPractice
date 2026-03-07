#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TH.Control.Editor
{
    public static class EnemyPatrolPathTools
    {
        private const string ForceMenuPath = "Tools/Control/Patrol/Regenerate All Enemy Paths (Force)";
        private const string MissingOnlyMenuPath = "Tools/Control/Patrol/Regenerate Missing Enemy Paths";

        [MenuItem(ForceMenuPath)]
        private static void RegenerateAllEnemyPatrolPathsForce()
        {
            RegenerateAllEnemyPatrolPaths(onlyWhenMissing: false);
        }

        [MenuItem(MissingOnlyMenuPath)]
        private static void RegenerateAllEnemyPatrolPathsOnlyMissing()
        {
            RegenerateAllEnemyPatrolPaths(onlyWhenMissing: true);
        }

        [MenuItem(ForceMenuPath, true)]
        [MenuItem(MissingOnlyMenuPath, true)]
        private static bool ValidateRegenerateAllEnemyPatrolPaths()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void RegenerateAllEnemyPatrolPaths(bool onlyWhenMissing)
        {
            EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int total = 0;
            int regenerated = 0;
            int skipped = 0;
            int failed = 0;

            foreach (EnemyController enemy in enemies)
            {
                if (!IsSceneEnemyInstance(enemy))
                    continue;

                total++;

                if (onlyWhenMissing && enemy.HasPatrolPathInEditor)
                {
                    skipped++;
                    continue;
                }

                bool generated;
                try
                {
                    generated = enemy.TryRegeneratePatrolWaypointsInEditor();
                }
                catch (System.Exception ex)
                {
                    generated = false;
                    Debug.LogError($"[{nameof(EnemyPatrolPathTools)}] Exception while regenerating patrol path on '{enemy.name}'.\n{ex}", enemy);
                }

                if (generated)
                    regenerated++;
                else
                    failed++;
            }

            string mode = onlyWhenMissing ? "only-missing" : "force";
            Debug.Log($"[{nameof(EnemyPatrolPathTools)}] Completed mode={mode}. total={total}, regenerated={regenerated}, skipped={skipped}, failed={failed}");
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
