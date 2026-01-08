using System;
using System.Collections.Concurrent;
using System.IO;
using System.Collections.Generic;

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using TH.SceneManagement;
using TH.Resource;
using TH.Utils;

namespace TH.SaveLoad
{
    public class SaveSystem : ISaveSystem
    {

        
        // outer services
        private readonly IResourceLoader resourceLoader;
        private readonly ISceneLoader sceneLoader;
        private readonly ISaveFileHandler saveFileHandler;
        private readonly ISaveEntityRegistry entityRegistry;
        
        // sub classes
        private readonly SaveTypeResolver typeResolver;
        private readonly SaveStateSerializer stateSerializer;
        
        private readonly UniTaskCompletionSource<SceneCatalogSO> catalogResolveTCS = new();
        private readonly UniTask<SceneCatalogSO> catalogResolved;
        private SceneCatalogSO sceneCatalog;
        
        private const string SceneCatalogKey = "SceneCatalogSO";
        
        private readonly SemaphoreSlim ioSemaphore = new (1, 1);
        
        private bool isLoading;
        private bool saveRequested;
        private string requestedSaveFile;
        private SceneEntry requestedSceneEntry;

        public SaveSystem(ISceneLoader sceneLoader, IResourceLoader resourceLoader, 
            ISaveFileHandler saveFileHandler, ISaveEntityRegistry saveEntityRegistry)
        {
            // outer services 참조 캐싱
            this.sceneLoader = sceneLoader;
            this.resourceLoader = resourceLoader;
            this.saveFileHandler = saveFileHandler;
            this.entityRegistry = saveEntityRegistry;
            
            // entity registry 내부 의존성 주입
            entityRegistry.SetCatalogAccessor(() => sceneCatalog);
            
            // inner sub classes 초기화
            typeResolver = new SaveTypeResolver();
            stateSerializer = new SaveStateSerializer(typeResolver);

            catalogResolved = catalogResolveTCS.Task.Preserve();
            LoadSceneCatalogAsync().Forget();

            // LoadAsync(GetSaveFileName()).Forget(); //todo: 세이브파일 관리 기능 추가 후 제거
            
            sceneLoader.OnBeforeSceneChanged += OnBeforeSceneChanged;
            sceneLoader.OnAfterSceneChanged += OnAfterSceneChanged;
        }

        #region Initialization

        private async UniTask LoadSceneCatalogAsync()
        {
            this.Log($"[SaveSystem] LoadSceneCatalogAsync() 시작");
            try
            {
                this.Log($"resourceLoader.LoadAsync<SceneCatalogSO>('{SceneCatalogKey}') 호출 중...");
                sceneCatalog = await resourceLoader.LoadAsync<SceneCatalogSO>(SceneCatalogKey);
                
                this.Log($"sceneCatalog 로드 완료: {(sceneCatalog != null ? sceneCatalog.name : "NULL")}");
                catalogResolveTCS.TrySetResult(sceneCatalog);
                
                this.Log($"[SaveSystem] catalogResolveTCS.TrySetResult 완료, RunPendingJobsAsync 호출");
                await RunPendingJobsAsync();
                this.Log($"[SaveSystem] LoadSceneCatalogAsync() 완료");
            }
            catch (Exception e)
            {
                Logg.LogError($"[SaveSystem] LoadSceneCatalogAsync() 예외 발생: {e}");
                catalogResolveTCS.TrySetException(e);
#if UNITY_EDITOR
                throw;
#endif
            }
        }
        
        private async UniTask RunPendingJobsAsync()
        {
            if (sceneCatalog == null)
            {
                Logg.LogError($"[SaveSystem] {nameof(RunPendingJobsAsync)} invoked before sceneCatalog is ready");
                return;
            }

            await UniTask.SwitchToMainThread();
            // SceneCatalog 준비 완료 후 EntityRegistry의 대기 등록 작업 처리
            var currentEntry = sceneCatalog.GetCurrentSceneEntry();
            entityRegistry.ProcessPendingRegistrations(currentEntry);
        }

        private async UniTask WaitForCatalog(CancellationToken token = default)
        {
            if (sceneCatalog != null) return; // sceneCatalog가 이미 세팅되어 있다면 await 없이 즉시 종료
            await catalogResolved.AttachExternalCancellation(token);
        }

        private bool IsMainMenuSceneEntry(SceneEntry sceneEntry)
        {
            return sceneCatalog != null && sceneCatalog.IsMainMenuSceneEntry(sceneEntry);
        }

        private static bool IsSaveTargetScene(SceneEntry sceneEntry)
        {
            return sceneEntry != null && sceneEntry.SaveTargetScene;
        }


        #endregion
        
        #region Load Last Scene
        
        public async UniTask LoadLastScene(string saveFile = null)
        {
            try
            {
                // 별도의 세이브 파일명을 지정하지 않은 경우 saveFileHandler로부터 받아옴
                // 현재 세이브 파일 우선 적용 (없을 시 생성, 세부 정책은 saveFileHandler 내부 구현 참고)
                if (string.IsNullOrEmpty(saveFile))
                    saveFile = GetSaveFileName();

                if (LoadFile(saveFile) is not { } data)
                    throw new IOException($"failed to load saveFile:({saveFile})");

                // 메인 쓰레드 환경, scene catalog 보장
                await UniTask.SwitchToMainThread();
                await WaitForCatalog();

                // 저장된 씬이 없다면 디폴트 씬으로 이동
                var lastSceneEntry = data.lastSceneEntry;
                if (!IsSaveTargetScene(lastSceneEntry) || IsMainMenuSceneEntry(lastSceneEntry))
                    lastSceneEntry = null;

                object key;
                if (lastSceneEntry is { sceneRef: { } sceneRef })
                    key = sceneRef;
                else
                    key = sceneCatalog.GetDefaultSceneEntry().sceneRef;
                    
                await sceneLoader.LoadSceneAsync(key);
            }
            catch (Exception e) { Logg.LogError($"[{GetType().Name}] exception while LoadLastScene - {e}"); }
            finally
            {
                this.Log($"LoadLastScene() 종료", Logg.LoggingMode.Completed);
            }
        }

        #endregion
        
        #region Save/Load/Delete (public API)

        public async UniTask SaveAsync(string saveFile = null, SceneEntry sceneEntry = null)
        {
            this.Log($"===== SaveAsync() 시작 ===== saveFile: {saveFile}, sceneEntry: {(sceneEntry != null ? sceneEntry.key : "NULL")}", Logg.LoggingMode.Completed);
            this.Log($"SaveAsync 조건체크 - isLoading: {isLoading}, ioSemaphore.CurrentCount: {ioSemaphore.CurrentCount}", Logg.LoggingMode.Completed);
            
            if (isLoading || ioSemaphore.CurrentCount == 0)
            {
                Logg.Log($"[SaveSystem] ioSemaphore.CurrentCount: {ioSemaphore.CurrentCount}", Logg.LoggingMode.Completed);
                CoalesceSave(saveFile, sceneEntry);
                return;
            }

            this.Log($"WaitForCatalog 호출");
            await WaitForCatalog();
            this.Log($"WaitForCatalog 완료, RunExclusive 진입");
            
            await RunExclusive(async () => {
                this.Log($"RunExclusive 내부 시작");
                
                if (saveRequested)
                {
                    this.Log($"saveRequested=true, 기존 요청으로 대체: {requestedSaveFile}");
                    saveFile = requestedSaveFile ?? saveFile;
                    sceneEntry = requestedSceneEntry ?? sceneEntry;
                    ResetSaveRequest();
                }
                // 별도의 세이브 파일명을 지정하지 않은 경우 saveFileHandler로부터 받아옴
                // 현재 세이브 파일 우선 적용 (없을 시 생성, 세부 정책은 saveFileHandler 내부 구현 참고)
                else if (string.IsNullOrEmpty(saveFile))
                    saveFile = GetSaveFileName(); 
                
                try 
                { 
                    this.Log($"SaveCoreAsync 호출 - saveFile: {saveFile}");
                    await SaveCoreAsync(saveFile, sceneEntry); 
                    this.Log($"SaveCoreAsync 완료", Logg.LoggingMode.Completed);
                }
                catch (Exception e) 
                { 
                    Logg.LogError($"[SaveSystem] SaveCoreAsync 예외: {e}");
                }

                try
                {
                    while (TryDequeueCoalescedSave(out var nextSaveFile, out var nextSaveEntry))
                    {
                        this.Log($"대기 저장 처리: {nextSaveFile}");
                        await SaveCoreAsync(nextSaveFile, nextSaveEntry);
                    }
                }
                catch (Exception e) 
                { 
                    Logg.LogError($"[SaveSystem] 대기 저장 예외: {e}");
                }
                
                this.Log($"===== SaveAsync() 완료 =====");
            });
        }
        
        public async UniTask DeleteAsync(string saveFile)
        {
            await RunExclusive(async () =>
            {
                await UniTask.SwitchToMainThread();
                try {Delete(saveFile);}
                catch (Exception e) { Logg.LogError($"[SaveSystem] Delete failed - {e}");}
            });
        }

        public async UniTask LoadAsync(string saveFile = null)
        {
            // 별도의 세이브 파일명을 지정하지 않은 경우 saveFileHandler로부터 받아옴
            // 현재 세이브 파일 우선 적용 (없을 시 생성, 세부 정책은 saveFileHandler 내부 구현 참고)
            if (string.IsNullOrEmpty(saveFile))
                saveFile = GetSaveFileName();
            
            this.Log($"===== LoadAsync() 시작 ===== saveFile: {saveFile}");
            await RunExclusive(async () =>
            {
                this.Log($"LoadAsync RunExclusive 내부 - isLoading: {isLoading}");
                if (isLoading)
                {
                    Debug.LogWarning("[SaveSystem] LoadAsync 중단 - 이미 로딩 중");
                    return;
                }
                isLoading = true;

                try
                {
                    await UniTask.SwitchToMainThread();
                    this.Log($"[SaveSystem] WaitForCatalog 호출");
                    await WaitForCatalog();
                    this.Log($"[SaveSystem] LoadCoreAsync 호출");
                    await LoadCoreAsync(saveFile);
                    this.Log($"[SaveSystem] LoadCoreAsync 완료");
                }
                catch (Exception e) 
                { 
                    Logg.LogError($"[SaveSystem] LoadAsync() failed: {e.Message}"); 
                }
                finally 
                { 
                    isLoading = false; 
                    this.Log($"[SaveSystem] ===== LoadAsync() 완료 =====", Logg.LoggingMode.Completed);
                }
            });
        }
        
        private void ResetSaveRequest()
        {
            saveRequested = false;
            requestedSaveFile = null;
            requestedSceneEntry = null;
        }
        
        private async UniTask RunExclusive(Func<UniTask> func)
        {
            await ioSemaphore.WaitAsync();
            try { await func(); }
            finally { ioSemaphore.Release(); }
        }
        
        private void CoalesceSave(string saveFile, SceneEntry sceneEntry)
        {
            saveRequested = true;
            requestedSaveFile = saveFile;
            requestedSceneEntry = sceneEntry;
            Logg.Log("[SaveSystem] Save queued (coalesced to latest)", Logg.LoggingMode.Completed);
        }

        private bool TryDequeueCoalescedSave(out string file, out SceneEntry entry)
        {
            if (!saveRequested || string.IsNullOrEmpty(requestedSaveFile))
            {
                file = null; 
                entry = null; 
                return false;
            }
            
            file = requestedSaveFile; 
            entry = requestedSceneEntry;
            ResetSaveRequest();
            
            return true;
        }

        #endregion

        #region Save/Load/Delete (private Core)
        
        private async UniTask SaveCoreAsync(string saveFile, SceneEntry sceneEntry = null)
        {
            this.Log($"SaveCoreAsync() 시작 - saveFile: {saveFile}", Logg.LoggingMode.Completed);
            // 세이브 데이터를 저장할 세이브 파일 데이터 컨테이너 생성
            SaveFileData data = LoadFile(saveFile);
            this.Log($"LoadFile 완료 - data: (globalData.Count: {data.globalData.Count}, lastSceneEntry: {data.lastSceneEntry?.key}, sceneData.Count: {data.sceneData.Count})", Logg.LoggingMode.Completed);
            // 세이브 데이터(SavableEntry) 목록 생성
            List<SavableEntry> sceneSaveEntries = new List<SavableEntry>();
            List<SavableEntry> globalSaveEntries = new List<SavableEntry>();
            
            await UniTask.SwitchToMainThread();
            sceneEntry ??= sceneCatalog.GetCurrentSceneEntry();
            if (sceneEntry != null && !IsSaveTargetScene(sceneEntry))
            {
                this.Log($"SaveCoreAsync skipped - sceneEntry is not save target: {sceneEntry?.key}", Logg.LoggingMode.Completed);
                return;
            }
            var sceneEntryForSave = IsMainMenuSceneEntry(sceneEntry) ? null : sceneEntry;
            
            // 등록된 세이브 대상(ISavable)들의 데이터 직렬화 수행
            // 직렬화된 데이터(SavableEntry)를 씬 데이터(씬에 종속된 오브젝트), 글로벌(씬과 무관한 오브젝트, 서비스) 데이터로 구분하여 캐싱
            if (entityRegistry.TryGetSceneSavableEntries(sceneEntryForSave, out var sceneEntities))
                AddSaveEntries(sceneSaveEntries, sceneEntities);
            AddSaveEntries(globalSaveEntries, entityRegistry.GetGlobalEntities());
            this.Log($"CaptureState 완료 - sceneEntries: {sceneSaveEntries.Count}, globalEntries: {globalSaveEntries.Count}", Logg.LoggingMode.Completed);
            
            // 직렬화된 데이터를 컨테이너(SaveFileData)에 저장 
            if (data.sceneData != null && sceneEntryForSave != null)
                data.sceneData[sceneEntryForSave.sceneId] = sceneSaveEntries;
            data.globalData = globalSaveEntries;
            if (sceneEntryForSave != null)
                data.lastSceneEntry = sceneEntryForSave;
            
            // 세이브 데이터 파일로 저장
            SaveFile(saveFile, data);
            this.Log($"SaveCoreAsync() 완료", Logg.LoggingMode.Completed);
        }
        
        
        private async UniTask LoadCoreAsync(string saveFile, SceneEntry currentSceneEntry = null)
        {
            this.Log($"LoadCoreAsync() 시작 - saveFile: {saveFile}");
            var data = LoadFile(saveFile);
            this.Log($"LoadFile 완료 - data null? {data == null}");
            if (data == null)
            {
                Debug.LogWarning("[SaveSystem] LoadCoreAsync 중단 - data is null");
                return;
            }

            await UniTask.SwitchToMainThread();
            currentSceneEntry ??= sceneCatalog.GetCurrentSceneEntry();
            if (currentSceneEntry != null && !IsSaveTargetScene(currentSceneEntry))
            {
                this.Log($"LoadCoreAsync skipped - sceneEntry is not save target: {currentSceneEntry?.key}", Logg.LoggingMode.Completed);
                return;
            }
            RestoreState(data, currentSceneEntry);
            this.Log($"LoadCoreAsync({saveFile}, {currentSceneEntry?.key}) 완료", Logg.LoggingMode.Completed);
        }
        
        private void Delete(string saveFile)
        {
            File.Delete(GetPathFromSaveFile(saveFile));
        }

        #endregion

        #region ISceneLoader Event handler

        private async UniTask OnBeforeSceneChanged(CancellationToken externalToken)
        {
            if (string.IsNullOrEmpty(GetSaveFileName()))
                return;
            // 씬 전환 전 자동 저장
            externalToken.ThrowIfCancellationRequested();
            await WaitForCatalog(externalToken);
            
            var currentSceneEntry = sceneCatalog.GetCurrentSceneEntry();
            if (currentSceneEntry == null || !IsSaveTargetScene(currentSceneEntry))
            {
                this.Log($"OnBeforeSceneChanged skipped - sceneEntry is not save target: {currentSceneEntry?.key}", Logg.LoggingMode.Completed);
                return;
            }
            await SaveAsync(GetSaveFileName());
        }
        private async UniTask OnAfterSceneChanged(CancellationToken externalToken)
        {
            if (string.IsNullOrEmpty(GetSaveFileName()))
                return;
            // 씬 전환 후 자동 로드 + 저장
            externalToken.ThrowIfCancellationRequested();
            await WaitForCatalog(externalToken);
           
            var currentSceneEntry = sceneCatalog.GetCurrentSceneEntry();
            if (currentSceneEntry == null || !IsSaveTargetScene(currentSceneEntry))
            {
                this.Log($"OnAfterSceneChanged skipped - sceneEntry is not save target: {currentSceneEntry?.key}", Logg.LoggingMode.Completed);
                return;
            }
            // 씬 전환 후 자동 로드 및 저장
            await LoadAsync(GetSaveFileName());
            await SaveAsync(GetSaveFileName());
        }

        #endregion
        
        #region State (CaptureState, RestoreState)

        // 씬에 존재하는 모든 SavableEntity의 상태 수집, 저장데이터에 반영
        private void AddSaveEntries(ICollection<SavableEntry> entryCollection, ICollection<ISavableEntity> entities)
        {
            if (entryCollection == null || entities == null)
            {
                Logg.LogWarning($"[{GetType().Name}] AddEntries - empty entryCollection or savableEntities");
                return;
            }
            
            foreach (var entity in entities)
            {
                stateSerializer.SerializeEntity(entryCollection, entity);
            }
        }

        // SavableEntity 에 상태 복원
        private void RestoreState(SaveFileData data, SceneEntry currentSceneEntry = null)
        {
            this.Log($"RestoreState(SaveFileData) 시작");
            List<SavableEntry> entries = new(); // 세이브 데이터 리스트 생성
            currentSceneEntry ??= sceneCatalog.GetCurrentSceneEntry(); // 현재 씬 정보 캡처
            this.Log($"scene Entry: {currentSceneEntry} - {currentSceneEntry?.key}", Logg.LoggingMode.Completed);
            // entries에 세이브 엔트리 목록 반영
            GetEntryFromSave(data, entries, currentSceneEntry);
            this.Log($"GetEntryFromSave 완료 - currentSceneEntry: {currentSceneEntry}, entries: {entries.Count}", Logg.LoggingMode.Completed);
            string entryFromSave = "";
            foreach (var e in entries)
            {
                entryFromSave += $"\n({e.id} - {e.typeName})";
            }
            this.Log($"GetEntryFromSave (entries.id - entries.typeName): {entryFromSave}", Logg.LoggingMode.Completed);
            
            // <고유 식별자, 고유 객체의 <타입, 세이브 데이터>> 딕셔너리 생성 (grouped)
            var grouped = new Dictionary<string, Dictionary<string, object>>(entries.Count); 
            // json to runtime data 파싱 -> grouped에 등록
            stateSerializer.DeserializeEntries(entries, grouped);
            this.Log($"ExtractSaveData 완료 - currentSceneEntry: {currentSceneEntry}, grouped keys: {grouped.Count}", Logg.LoggingMode.Completed);
            
            // 파싱된 런타임 데이터 반영 (글로벌)
            this.Log($"글로벌 엔티티 복원 시작 - GlobalEntities: {entityRegistry.GetGlobalEntities().Count}", Logg.LoggingMode.Completed);
            ApplyState(entityRegistry.GetGlobalEntities(), grouped);
            
            // 파싱된 런타임 데이터 반영 (현재 씬)
            if (currentSceneEntry != null &&
                entityRegistry.TryGetSceneSavableEntries(currentSceneEntry, out var sceneSavables))
            {
                ApplyState(sceneSavables, grouped);
                this.Log($"씬 엔티티 복원 완료 - count: {sceneSavables.Count}",  Logg.LoggingMode.Completed);
            }
            else
            {
                this.Log($"[{GetType().Name}] RestoreState() - 현재 씬의 엔티티 없음 (currentSceneEntry: {currentSceneEntry})", Logg.LoggingMode.Completed);
            }
            
            this.Log($"LoadedStateCache 업데이트 시작", Logg.LoggingMode.Completed);
            foreach (var (id, stateDict) in grouped)
            {
                // 기존 데이터가 있으면 덜어쓰기, 없으면 추가
                entityRegistry.UpdateStateCache(id, stateDict);
            }
            this.Log($"RestoreState(SaveFileData) 완료", Logg.LoggingMode.Completed);
        }


        private static void GetEntryFromSave(SaveFileData data, List<SavableEntry> entries, SceneEntry currentSceneEntry)
        {
            var sceneEntries = data.sceneData;
            // 현재 씬 세이브 데이터 추가
            if (currentSceneEntry != null
                && sceneEntries.TryGetValue(currentSceneEntry.sceneId, 
                    out var targetSceneEntries))
            {
                // 세이브 데이터 리스트에 추가
                entries.AddRange(targetSceneEntries);
            }
            else if (currentSceneEntry != null)
            {
                Logg.Log($"[SaveSystem] No saved data for scene '{currentSceneEntry?.key}'", Logg.LoggingMode.Completed);
            }
            // 글로벌(특정 씬에 종속되지 않는) 세이브 데이터 추가
            if (data.globalData is { Count: > 0 } globEntries)
            {
                Logg.Log($"[SaveSystem] GetEntryFromSave - globEntries.Count: {globEntries.Count}", Logg.LoggingMode.Completed);
                entries.AddRange(globEntries);
            } 
            else Logg.Log("[SaveSystem] No saved global data", Logg.LoggingMode.Completed);
        }
        

        private void ApplyState(IEnumerable<ISavableEntity> entities,
            IReadOnlyDictionary<string, Dictionary<string, object>> stateGroup)
        {
            foreach (var entity in entities)
            {
                if (!entity.IsNotNull())
                {
                    Logg.LogWarning($"[{GetType().Name}] ApplyState - entity is destroyed");
                    continue;
                }

                if (!stateGroup.TryGetValue(entity.UniqueIdentifier,
                        out var states))
                {
                    Logg.Log($"[{GetType().Name}] ApplyState - there is no state group for {entity.UniqueIdentifier}", Logg.LoggingMode.Completed);
                    continue;
                }
                entity.RestoreState(states);
            }
        }

        #endregion

        #region Save File I/O (ISaveFileHandler)

        SaveFileData LoadFile(string saveFile) => saveFileHandler.LoadFile(saveFile);
        void SaveFile(string saveFile, SaveFileData data) => saveFileHandler.SaveFile(saveFile, data);

        string GetSaveFileName() => saveFileHandler.GetSaveFileName();
        string GetPathFromSaveFile(string saveFile) => saveFileHandler.GetPathFromSaveFile(saveFile);

        #endregion
    }
}
