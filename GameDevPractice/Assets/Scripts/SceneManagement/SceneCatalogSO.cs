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

    [CreateAssetMenu(fileName = "SceneCatalogSO", menuName = "Scriptable Objects/Catalog/SceneCatalogSO")]
    // 씬 엔트리 카탈로그 조회 및 에디터 메타 동기화 ScriptableObject
    public class SceneCatalogSO : ScriptableObject
    {
        // 프로젝트 내 이동 대상 씬 엔트리 목록
        [SerializeField] private List<SceneEntry> entries = new();
        // 기본 진입 씬 엔트리
        [SerializeField] private SceneEntry defaultSceneEntry;
        // 메인 메뉴 씬 엔트리
        [SerializeField] private SceneEntry mainMenuSceneEntry;

#if UNITY_EDITOR
        // 인스펙터 값 변경 시 sceneId/key 자동 동기화 훅
        private void OnValidate()
        {
            // 변경사항 존재 여부 추적 플래그
            bool dirty = false;
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    // 비어있는 엔트리 스킵 가드
                    if (e == null) continue;
                    // Addressables 참조 기반 GUID 추출 단계
                    var sceneRef = e.sceneRef;
                    var sceneGuid = sceneRef.AssetGUID;

                    // GUID와 주소 키 동기화 반영
                    e.sceneId = sceneGuid;
                    e.key = Util.GetAddressKeyInEditor(sceneGuid);

                    // 필수 식별값 누락 검증 가드
                    if (string.IsNullOrEmpty(e.sceneId) || string.IsNullOrEmpty(e.key))
                    {
                        throw new Exception($"[{GetType()}.OnValidate] AssetReferenceScene is invalid");
                    }

                    dirty = true;
                }
            }

            // 기본 씬 엔트리 메타 동기화 분기
            if (defaultSceneEntry.sceneRef is { } defaultSceneRef)
            {
                defaultSceneEntry.sceneId = defaultSceneRef.AssetGUID;
                defaultSceneEntry.key = Util.GetAddressKeyInEditor(defaultSceneRef);
                dirty = true;
            }
             
            // 메인 메뉴 씬 엔트리 메타 동기화 분기
            if (mainMenuSceneEntry.sceneRef is { } mainMenuSceneRef)
            {
                mainMenuSceneEntry.sceneId = mainMenuSceneRef.AssetGUID;
                mainMenuSceneEntry.key = Util.GetAddressKeyInEditor(mainMenuSceneRef);
                dirty = true;
            }

            // 에셋 변경 플래그 반영
            if (dirty)
                UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        // 실행 환경 기준 기본 진입 씬 엔트리 반환
        public SceneEntry GetDefaultSceneEntry()
        {
#if UNITY_EDITOR
            // 에디터 환경: 플레이 모드 진입 시점 씬 엔트리 반환
            var guid = PlayModeSceneCache.GetCachedGuid();
            this.Log($"GetDefaultSceneEntry - guid: {guid}", Logg.LoggingMode.Completed);

            if (!string.IsNullOrEmpty(guid))
            {
                // 캐시 GUID 기반 씬 엔트리 조회 분기
                var e = FindByGuid(guid);
                if (e != null && !IsMainMenuSceneEntry(e)) {
                    this.Log($"GetDefaultSceneEntry return {e.key}", Logg.LoggingMode.Completed);
                    return e;}
            }
#endif
            // 빌드 환경: 기본 씬 엔트리 반환
            return defaultSceneEntry;
        }

        // 메인 메뉴 씬 엔트리 반환
        public SceneEntry GetMainMenuSceneEntry()
        {
            return mainMenuSceneEntry;
        }

        // 메인 메뉴 씬 엔트리 동일성 판별
        public bool IsMainMenuSceneEntry(SceneEntry sceneEntry)
        {
            // 비교 대상 누락 조기 종료 가드
            if (sceneEntry == null || mainMenuSceneEntry == null)
                return false;

            // sceneId 우선 비교 분기
            if (!string.IsNullOrEmpty(sceneEntry.sceneId) && sceneEntry.sceneId == mainMenuSceneEntry.sceneId)
                return true;

            // key 비교 보조 분기
            return !string.IsNullOrEmpty(sceneEntry.key) && sceneEntry.key == mainMenuSceneEntry.key;
        }
        
        // Scene 구조체 입력 기반 엔트리 조회 래퍼
        public bool TryGetSceneEntry(Scene scene, out SceneEntry sceneEntry)
        {
            return TryGetSceneEntry(scene.name, out sceneEntry);
        }

        // 씬 이름 기반 엔트리 조회 루틴
        private bool TryGetSceneEntry(string sceneName, out SceneEntry sceneEntry)
        {
            sceneEntry = null;
            // 유효하지 않은 씬 이름 조기 종료 가드
            if (sceneName == null || string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            foreach (var e in entries)
            {
                // null 엔트리 스킵 가드
                if (e == null) continue;
                // 키 불일치 엔트리 스킵 가드
                if (e.key != sceneName) continue;

                sceneEntry = e;
                return true;
            }

            return false;
        }
        
        // 현재 활성 씬 이름 조회 프로퍼티
        private string CurrSceneName => SceneManager.GetActiveScene().name;

        // 현재 활성 씬 엔트리 TryGet 래퍼
        public bool TryGetCurrentSceneEntry(out SceneEntry sceneEntry)
        {
            return TryGetSceneEntry(CurrSceneName, out sceneEntry);
        }

        // 현재 활성 씬 엔트리 반환
        public SceneEntry GetCurrentSceneEntry()
        {
            return FindBySceneName(CurrSceneName);
        }

        // GUID 기반 씬 엔트리 탐색 루틴
        public SceneEntry FindByGuid(string guid)
        {
            this.Log($"FindByGuid({guid})", Logg.LoggingMode.Completed);
            if (string.IsNullOrEmpty(guid) || entries == null || entries.Count == 0) return null;

            foreach (var entry in entries)
            {
                // null 엔트리 스킵 가드
                if (entry == null) continue;
                // GUID 불일치 엔트리 스킵 가드
                if (entry.sceneId != guid) continue;
                return entry;
            }

            return null;
        }

        // 씬 이름 기반 씬 엔트리 탐색 루틴
        public SceneEntry FindBySceneName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || entries == null || entries.Count == 0) return null;

            foreach (var entry in entries)
            {
                // null 엔트리 스킵 가드
                if (entry == null) continue;
                // 이름 불일치 엔트리 스킵 가드
                if (entry.key != sceneName) continue;
                return entry;
            }

            return null;
        }

#if UNITY_EDITOR
        [InitializeOnLoad]
        // 에디트 모드 마지막 활성 씬 GUID 캐시 유틸리티
        public static class PlayModeSceneCache
        {
            // EditorPrefs 저장 키 상수
            private const string PrefKey = "TH.LastEditModeActiveSceneGuid";

            // 플레이 모드 상태 변경 이벤트 구독 정적 생성자
            static PlayModeSceneCache()
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }

            // 에디트 모드 종료 시 활성 씬 GUID 캐시 갱신 훅
            private static void OnPlayModeStateChanged(PlayModeStateChange state)
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;

                // 활성 씬 메타 조회 구간
                // ReSharper disable once AccessToStaticMemberViaDerivedType
                var scene = EditorSceneManager.GetActiveScene();
                var guid = AssetDatabase.AssetPathToGUID(scene.path);

                EditorPrefs.SetString(PrefKey, guid);
            }

            // 캐시된 씬 GUID 조회
            public static string GetCachedGuid() => EditorPrefs.GetString(PrefKey, string.Empty);
        }
#endif
    }
}
