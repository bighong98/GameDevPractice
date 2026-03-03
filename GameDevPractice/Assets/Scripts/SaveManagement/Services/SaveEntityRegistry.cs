using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
using TH.SceneManagement.Data;
using UnityEngine;
using TH.Utils;

namespace TH.SaveLoad
{
    /// <summary>
    /// ISavableEntity 등록/해제 및 상태 캐시 관리를 담당하는 레지스트리.
    /// SaveSystem의 outer service로 동작합니다.
    /// </summary>
    public class SaveEntityRegistry : ISaveEntityRegistry
    {
        // 씬 단위 엔티티 저장소: SceneEntry -> (UniqueIdentifier -> ISavableEntity)
        private readonly Dictionary<SceneEntry, Dictionary<string, ISavableEntity>> sceneEntities = new();
        
        // 글로벌 엔티티 저장소: UniqueIdentifier -> ISavableEntity
        private readonly Dictionary<string, ISavableEntity> globalEntities = new();
        
        // 로드된 상태 캐시: UniqueIdentifier -> (TypeName -> StateData)
        private readonly Dictionary<string, Dictionary<string, object>> loadedStateCache = new();
        
        // SceneCatalog 대기 큐
        private readonly ConcurrentQueue<Action<SceneEntry>> catalogPending = new();
        
        // SceneCatalog 접근용 (외부에서 주입)
        private Func<SceneCatalogSO> getCatalog;
        
        /// <summary>
        /// SceneCatalog 접근자 설정. SaveSystem 초기화 시 호출됩니다.
        /// </summary>
        public void SetCatalogAccessor(Func<SceneCatalogSO> catalogAccessor)
        {
            getCatalog = catalogAccessor;
        }
        
        /// <summary>
        /// SceneCatalog 준비 완료 후 대기 중인 등록 작업 처리.
        /// </summary>
        public void ProcessPendingRegistrations(SceneEntry currentSceneEntry)
        {
            while (catalogPending.TryDequeue(out var job))
            {
                job?.Invoke(currentSceneEntry);
            }
        }

        #region ISaveEntityRegistry Implementation

        public void RegisterEntity(ISavableEntity entity, bool saveImmediately = false, CancellationToken token = default)
        {
            // 등록 대상 고유 식별자 추출
            var id = entity.UniqueIdentifier;
            // 등록 요청 진단 로그 기록
            this.Log($"RegisterEntity({entity.GetType()}) - id: {id}, IsGlobal: {entity.IsGlobal}, IsRegistered: {entity.IsRegistered}"
                + $"{(entity.IsNotNull() && entity is Component c ? ", from scene:" + c.gameObject.scene.name : string.Empty)}"
                , Logg.LoggingMode.Completed);

            // 글로벌 엔티티 전용 등록 분기
            if (entity.IsGlobal)
            {
                RegisterGlobalEntity(entity, id);
                return;
            }

            // SceneCatalog 접근자 조회
            var sceneCatalog = getCatalog?.Invoke();
            if (sceneCatalog == null)
            {
                // SceneCatalog 준비 전 등록 요청 대기 큐 적재
                catalogPending.Enqueue(entry => RegisterSceneEntity(entry, entity, token));
                return;
            }

            // 대상 씬 엔트리 탐색 실패 시 현재 씬 엔트리 폴백
            if (!sceneCatalog.TryGetSceneEntry(entity.TargetScene, out var sceneEntry)
                && !sceneCatalog.TryGetCurrentSceneEntry(out sceneEntry))
            {
                Logg.LogWarning($"RegisterEntity({entity.GetType()}) - SceneEntry {sceneEntry} not found");
                return;
            }

            RegisterSceneEntity(sceneEntry, entity, token);
        }

        public void UnRegisterEntity(ISavableEntity entity, CancellationToken token = default)
        {
            if (!entity.IsNotNull())
            {
                this.LogWarning("UnRegisterEntity - invalid entity");
                return;
            }

            // 등록 해제 대상 고유 식별자 추출
            var id = entity.UniqueIdentifier;
            var removed = false;

            // 글로벌 엔티티 전용 해제 분기
            if (entity.IsGlobal)
            {
                removed = globalEntities.Remove(id);
            }
            else
            {
                // 현재 씬 엔트리 기준 제거 시도
                var sceneCatalog = getCatalog?.Invoke();
                if (sceneCatalog.IsNotNull()
                    && sceneCatalog.TryGetCurrentSceneEntry(out var currSceneEntry)
                    && sceneEntities.TryGetValue(currSceneEntry, out var dict))
                {
                    removed = dict.Remove(id);
                }

                // 씬 키 미일치 대비: 모든 씬 딕셔너리에서 fallback 제거
                if (!removed)
                {
                    removed = RemoveEntityByIdFromAllScenes(id);
                }

                // 잘못 분류되어 글로벌에 남아있는 경우까지 정리
                if (globalEntities.Remove(id))
                {
                    removed = true;
                }
            }

            entity.IsRegistered = false;
            this.Log($"UnRegisterEntity - entity: ({entity.GetType()}/{id}), IsGlobal: {entity.IsGlobal}, removed: {removed}", Logg.LoggingMode.Completed);
        }

        #endregion

        #region Entity Access (for SaveSystem)

        /// <summary>
        /// 특정 씬의 등록된 엔티티 컬렉션 조회.
        /// </summary>
        public bool TryGetSceneSavableEntries(SceneEntry targetSceneEntry, out ICollection<ISavableEntity> entityCollection)
        {
            PruneDestroyedEntities();

            this.Log($"TryGetSceneSavableEntries - targetSceneEntry: {targetSceneEntry?.key} (sceneId: {targetSceneEntry?.sceneId})", Logg.LoggingMode.Completed);
            this.Log($"TryGetSceneSavableEntries - sceneEntities.Keys: [{string.Join(", ", sceneEntities.Keys.Select(k => $"{k?.key}(hash:{k?.GetHashCode()})"))}]", Logg.LoggingMode.Completed);
            this.Log($"TryGetSceneSavableEntries - targetEntry hash: {targetSceneEntry?.GetHashCode()}", Logg.LoggingMode.Completed);
            
            if (targetSceneEntry != null
                && sceneEntities.TryGetValue(targetSceneEntry, out var sceneSavables))
            {
                this.Log($"TryGetSceneSavableEntries - SUCCESS, count: {sceneSavables.Count}", Logg.LoggingMode.Completed);
                entityCollection = sceneSavables.Values;
                return true;
            }
            
            this.Log($"TryGetSceneSavableEntries - FAILED, targetSceneEntry null? {targetSceneEntry == null}", Logg.LoggingMode.Completed);
            entityCollection = null;
            return false;
        }

        /// <summary>
        /// 글로벌 엔티티 컬렉션 조회.
        /// </summary>
        public ICollection<ISavableEntity> GetGlobalEntities()
        {
            PruneDestroyedEntities();
            return globalEntities.Values;
        }

        #endregion

        #region State Cache Access (for SaveSystem)

        /// <summary>
        /// 로드된 상태 캐시에 데이터 추가/갱신.
        /// </summary>
        public void UpdateStateCache(string id, Dictionary<string, object> stateDict)
        {
            loadedStateCache[id] = stateDict;
        }

        /// <summary>
        /// 로드된 상태 캐시에서 데이터 조회.
        /// </summary>
        public bool TryGetCachedState(string id, out Dictionary<string, object> stateDict)
        {
            return loadedStateCache.TryGetValue(id, out stateDict);
        }

        #endregion

        
        private void PruneDestroyedEntities()
        {
            PruneDestroyedFromDict(globalEntities);

            foreach (var dict in sceneEntities.Values)
            {
                PruneDestroyedFromDict(dict);
            }
        }

        private static void PruneDestroyedFromDict(IDictionary<string, ISavableEntity> dict)
        {
            if (dict == null || dict.Count == 0) return;

            List<string> staleIds = null;
            foreach (var (id, entity) in dict)
            {
                if (entity.IsNotNull()) continue;

                staleIds ??= new List<string>();
                staleIds.Add(id);
            }

            if (staleIds == null) return;
            foreach (var staleId in staleIds)
            {
                dict.Remove(staleId);
            }
        }

        private bool RemoveEntityByIdFromAllScenes(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            var removed = false;
            foreach (var dict in sceneEntities.Values)
            {
                removed |= dict.Remove(id);
            }

            return removed;
        }
        #region Private Helpers

        private void RegisterGlobalEntity(ISavableEntity entity, string id)
        {
            if (!entity.IsNotNull())
            {
                this.LogWarning($"RegisterGlobalEntity: invalid entity  id: {id}");
                return;
            }
            // 덮어쓰기 적용
            // 글로벌 ISavable 구현 객체는 자체적으로 IsRegistered 기준으로 중복 등록 방지 필요
            globalEntities[id] = entity;

            if (entity.IsNotNull() && loadedStateCache.TryGetValue(id, out var stateDict))
            {
                entity.RestoreState(stateDict);
            }

            entity.IsRegistered = true;
        }

        private void RegisterSceneEntity(SceneEntry sceneEntry, ISavableEntity entity, CancellationToken token)
        {
            if (token.IsCancellationRequested || sceneEntry == null || !entity.IsNotNull()) return;

            if (!sceneEntities.TryGetValue(sceneEntry, out var dict))
            {
                dict = new Dictionary<string, ISavableEntity>();
                sceneEntities[sceneEntry] = dict;
            }
            // 덮어쓰기 적용
            dict[entity.UniqueIdentifier] = entity;

            if (loadedStateCache.TryGetValue(entity.UniqueIdentifier, out var stateDict))
            {
                entity.RestoreState(stateDict);
            }

            entity.IsRegistered = true;
            Logg.Log($"[SaveEntityRegistry] SceneEntity({entity.GetType()}, {entity.UniqueIdentifier}) is added in sceneEntry: {sceneEntry}", Logg.LoggingMode.Completed);
        }

        #endregion
    }
}
