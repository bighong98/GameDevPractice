using System;
using System.Collections.Generic;
using TH.SaveLoad;
using TH.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using EditorSceneManager = UnityEditor.SceneManagement.EditorSceneManager;
#endif

namespace TH.Resource
{

    [CreateAssetMenu(fileName = "SceneCatalogSO", menuName = "Scriptable Objects/SceneCatalogSO")]
    public class SceneCatalogSO : ScriptableObject
    {
        [SerializeField] private List<SceneEntry> entries = new();
        [SerializeField] private SceneEntry defaultSceneEntry;

#if UNITY_EDITOR
        private void OnValidate()
        {
            bool dirty = false;
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    var sceneRef = e.sceneRef;
                    var sceneGuid = sceneRef.AssetGUID;

                    e.sceneId = sceneGuid;
                    e.key = Util.GetAddressKeyInEditor(sceneGuid);

                    if (string.IsNullOrEmpty(e.sceneId) || string.IsNullOrEmpty(e.key))
                    {
                        throw new Exception($"[{GetType()}.OnValidate] AssetReferenceScene is invalid");
                    }

                    dirty = true;
                }
            }

            if (defaultSceneEntry.sceneRef is { } defaultSceneRef)
            {
                defaultSceneEntry.sceneId = defaultSceneRef.AssetGUID;
                defaultSceneEntry.key = Util.GetAddressKeyInEditor(defaultSceneRef);
                dirty = true;
            }

            if (dirty)
                UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public SceneEntry GetDefaultSceneEntry()
        {
#if UNITY_EDITOR
            // 에디터 환경: 플레이 모드 진입 시점 씬 엔트리 반환
            var guid = PlayModeSceneCache.GetCachedGuid();
            if (!string.IsNullOrEmpty(guid))
            {
                var e = FindByGuid(guid);
                if (e != null) return e;
            }
#endif
            // 빌드 환경: 기본 씬 엔트리 반환
            return defaultSceneEntry;
        }
        
        public bool TryGetSceneEntry(Scene scene, out SceneEntry sceneEntry)
        {
            return TryGetSceneEntry(scene.name, out sceneEntry);
        }

        private bool TryGetSceneEntry(string sceneName, out SceneEntry sceneEntry)
        {
            sceneEntry = null;
            if (sceneName == null || string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            foreach (var e in entries)
            {
                if (e == null) continue;
                if (e.key != sceneName) continue;

                sceneEntry = e;
                return true;
            }

            return false;
        }
        
        private string CurrSceneName => SceneManager.GetActiveScene().name;
        public bool TryGetCurrentSceneEntry(out SceneEntry sceneEntry)
        {
            return TryGetSceneEntry(CurrSceneName, out sceneEntry);
        }

        public SceneEntry GetCurrentSceneEntry()
        {
            return FindBySceneName(CurrSceneName);
        }

        public SceneEntry FindByGuid(string guid)
        {
            this.Log($"FindByGuid({guid})", Logg.LoggingMode.Completed);
            if (string.IsNullOrEmpty(guid) || entries == null || entries.Count == 0) return null;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (entry.sceneId != guid) continue;
                return entry;
            }

            return null;
        }

        public SceneEntry FindBySceneName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || entries == null || entries.Count == 0) return null;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (entry.key != sceneName) continue;
                return entry;
            }

            return null;
        }

#if UNITY_EDITOR
        [InitializeOnLoad]
        public static class PlayModeSceneCache
        {
            private const string PrefKey = "TH.LastEditModeActiveSceneGuid";

            static PlayModeSceneCache()
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }

            private static void OnPlayModeStateChanged(PlayModeStateChange state)
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;

                // ReSharper disable once AccessToStaticMemberViaDerivedType
                var scene = EditorSceneManager.GetActiveScene();
                var guid = AssetDatabase.AssetPathToGUID(scene.path);

                EditorPrefs.SetString(PrefKey, guid);
            }

            public static string GetCachedGuid() => EditorPrefs.GetString(PrefKey, string.Empty);
        }
#endif
    }
}