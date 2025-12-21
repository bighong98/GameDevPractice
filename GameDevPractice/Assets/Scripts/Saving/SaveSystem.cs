using System;
using System.Collections.Concurrent;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Serialization.Json;
using TH.SceneManagement;
using TH.Resource;
using TH.Utils;

namespace TH.SaveLoad
{
    public class SaveSystem : ISaveSystem
    {
        private static readonly Dictionary<Type, MethodInfo> CachedMethodInfos = new();
        private static readonly Dictionary<string, Type> CachedTypes = new();
        private static readonly Dictionary<string, Dictionary<string, object>> LoadedStateCache = new(); // Non-MB 클래스 데이터

        private static readonly Dictionary<SceneEntry, Dictionary<string, ISavableEntity>> SceneEntities = new();
        private static readonly Dictionary<string, ISavableEntity> GlobalEntities = new();
        
        private readonly IResourceLoader resourceLoader;
        private readonly ISceneLoader sceneLoader;
        
        private readonly ConcurrentQueue<Action<SceneEntry>> catalogPending = new();
        private readonly UniTaskCompletionSource<SceneCatalogSO> catalogResolveTCS = new();
        private readonly UniTask<SceneCatalogSO> catalogResolved;
        private SceneCatalogSO sceneCatalog;
        
        private const string SceneCatalogKey = "SceneCatalogSO";
        private const int DefaultSceneIndexInCatalog = 0;
        
        private readonly SemaphoreSlim ioSemaphore = new (1, 1);
        
        private bool isLoading;
        private bool saveRequested;
        private string requestedSaveFile;
        private SceneEntry requestedSceneEntry;

        public SaveSystem(ISceneLoader sceneLoader, IResourceLoader resourceLoader)
        {
            this.sceneLoader = sceneLoader;
            this.resourceLoader = resourceLoader;

            catalogResolved = catalogResolveTCS.Task.Preserve();
            LoadSceneCatalogAsync().Forget();
        }

        #region Initialization

        private async UniTask LoadSceneCatalogAsync()
        {
            this.Log("[SaveSystem] LoadSceneCatalogAsync() 시작");
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
            var entry = sceneCatalog.GetCurrentSceneEntry();
            while (catalogPending.TryDequeue(out var job))
            {
                job?.Invoke(entry);
            }
        }

        private async UniTask WaitForCatalog(CancellationToken token = default)
        {
            if (sceneCatalog != null) return; // sceneCatalog가 이미 세팅되어 있다면 await 없이 즉시 종료
            await catalogResolved.AttachExternalCancellation(token);
        }

        #endregion
        
        #region Load Last Scene

        public async UniTask LoadLastScene(string saveFile)
        {
            await RunExclusive(async () =>
            {
                if (isLoading) return;
                isLoading = true;
                
                try
                {
                    if (LoadFile(saveFile) is not { } data) return;

                    // 메인 쓰레드 환경, scene catalog 보장
                    await UniTask.SwitchToMainThread();
                    await WaitForCatalog();

                    // 저장된 씬이 없다면 디폴트 씬으로 이동
                    if (data.lastSceneEntry is not { sceneRef: { } key })
                        key = sceneCatalog.GetDefaultSceneEntry().sceneRef;

                    await sceneLoader.LoadSceneAsync(key);
                    await UniTask.Yield();
                    RestoreState(data);
                }
                catch (Exception e) { Logg.LogWarning($"[{GetType().Name}] exception while LoadLastScene - {e}"); }
                finally
                {
                    isLoading = false;
                    this.Log($"LoadLastScene() 종료", Logg.LoggingMode.Completed);
                }
            });
        }

        #endregion
        
        #region Save/Load/Delete (Async + public)

        public async UniTask SaveAsync(string saveFile, SceneEntry sceneEntry = null)
        {
            this.Log($"===== SaveAsync() 시작 ===== saveFile: {saveFile}, sceneEntry: {(sceneEntry != null ? sceneEntry.key : "NULL")}");
            this.Log($"SaveAsync 조건체크 - isLoading: {isLoading}, ioSemaphore.CurrentCount: {ioSemaphore.CurrentCount}");
            
            if (isLoading || ioSemaphore.CurrentCount == 0)
            {
                Logg.Log($"[SaveSystem] ioSemaphore.CurrentCount: {ioSemaphore.CurrentCount}", Logg.LoggingMode.Completed);
                CoalesceSave(saveFile, sceneEntry);
                return;
            }

            this.Log("WaitForCatalog 호출");
            await WaitForCatalog();
            this.Log("WaitForCatalog 완료, RunExclusive 진입");
            
            await RunExclusive(async () => {
                this.Log("RunExclusive 내부 시작");
                
                if (saveRequested)
                {
                    this.Log("saveRequested=true, 기존 요청으로 대체: {requestedSaveFile}");
                    saveFile = requestedSaveFile ?? saveFile;
                    sceneEntry = requestedSceneEntry ?? sceneEntry;
                    ResetSaveRequest();
                }
                
                try 
                { 
                    this.Log("SaveCoreAsync 호출 - saveFile: {saveFile}");
                    await SaveCoreAsync(saveFile, sceneEntry); 
                    this.Log("SaveCoreAsync 완료");
                }
                catch (Exception e) 
                { 
                    Logg.LogError($"[SaveSystem] SaveCoreAsync 예외: {e}");
                }

                try
                {
                    while (TryDequeueCoalescedSave(out var nextSaveFile, out var nextSaveEntry))
                    {
                        this.Log("대기 저장 처리: {nextSaveFile}");
                        await SaveCoreAsync(nextSaveFile, nextSaveEntry);
                    }
                }
                catch (Exception e) 
                { 
                    Logg.LogError($"[SaveSystem] 대기 저장 예외: {e}");
                }
                
                this.Log("===== SaveAsync() 완료 =====");
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

        public async UniTask LoadAsync(string saveFile)
        {
            this.Log("===== LoadAsync() 시작 ===== saveFile: {saveFile}");
            await RunExclusive(async () =>
            {
                this.Log("LoadAsync RunExclusive 내부 - isLoading: {isLoading}");
                if (isLoading)
                {
                    Debug.LogWarning("[SaveSystem] LoadAsync 중단 - 이미 로딩 중");
                    return;
                }
                isLoading = true;

                try
                {
                    await UniTask.SwitchToMainThread();
                    this.Log("[SaveSystem] WaitForCatalog 호출");
                    await WaitForCatalog();
                    this.Log("[SaveSystem] LoadCoreAsync 호출");
                    await LoadCoreAsync(saveFile);
                    this.Log("[SaveSystem] LoadCoreAsync 완료");
                }
                catch (Exception e) 
                { 
                    Logg.LogError($"[SaveSystem] LoadAsync() failed: {e.Message}"); 
                }
                finally 
                { 
                    isLoading = false; 
                    this.Log("[SaveSystem] ===== LoadAsync() 완료 =====");
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

        #region Save/Load/Delete (private)

        private async UniTask SaveCoreAsync(string saveFile, SceneEntry sceneEntry = null)
        {
            this.Log("SaveCoreAsync() 시작 - saveFile: {saveFile}");
            
            SaveFileData data = LoadFile(saveFile);
            this.Log("LoadFile 완료 - data null? {data == null}");
            
            List<SavableEntry> sceneEntries = new List<SavableEntry>();
            List<SavableEntry> globalEntries = new List<SavableEntry>();
            
            await UniTask.SwitchToMainThread();
            CaptureState(sceneEntries, globalEntries);
            this.Log("CaptureState 완료 - sceneEntries: {sceneEntries.Count}, globalEntries: {globalEntries.Count}");
            
            sceneEntry ??= sceneCatalog.GetCurrentSceneEntry();
            data.sceneData[sceneEntry.sceneId] = sceneEntries;
            data.globalData = globalEntries;
            data.lastSceneEntry = sceneEntry;

            SaveFile(saveFile, data);
            this.Log("[SaveSystem] SaveCoreAsync() 완료", Logg.LoggingMode.Completed);
        }
        
        private async UniTask LoadCoreAsync(string saveFile)
        {
            this.Log("LoadCoreAsync() 시작 - saveFile: {saveFile}");
            var data = LoadFile(saveFile);
            this.Log("LoadFile 완료 - data null? {data == null}");
            if (data == null)
            {
                Debug.LogWarning("[SaveSystem] LoadCoreAsync 중단 - data is null");
                return;
            }

            await UniTask.SwitchToMainThread();
            RestoreState(data);
            this.Log("[SaveSystem] LoadCoreAsync() 완료");
        }
        
        private void Delete(string saveFile)
        {
            File.Delete(GetPathFromSaveFile(saveFile));
        }

        #endregion
        
        #region State (CaptureState, RestoreState)

        // 씬에 존재하는 모든 SavableEntity의 상태 수집, 저장데이터에 반영
        private void CaptureState(List<SavableEntry> sceneEntries, List<SavableEntry> globalEntries)
        {
            this.Log("CaptureState() 시작 - GlobalEntities: {GlobalEntities.Count}, SceneEntities keys: {SceneEntities.Count}");
            AddEntries(globalEntries, GlobalEntities.Values);
            this.Log("글로벌 엔티티 처리 완료 - globalEntries: {globalEntries.Count}");
            if (GetCurrentSceneSavables(out var sceneSavableCollection))
            {
                this.Log("씬 엔티티 - count: {sceneSavableCollection.Count}");
                AddEntries(sceneEntries, sceneSavableCollection);
            }
            else
            {
                Debug.LogWarning("[SaveSystem] 현재 씬에 저장 가능한 엔티티 없음");
            }
            this.Log("CaptureState() 완료 - sceneEntries: {sceneEntries.Count}, globalEntries: {globalEntries.Count}");
        }

        private bool GetCurrentSceneSavables(out ICollection<ISavableEntity> savables)
        {
            if (sceneCatalog.GetCurrentSceneEntry() is { } currentSceneEntry
                && SceneEntities.TryGetValue(currentSceneEntry,
                    out var sceneSavables))
            {
                savables = sceneSavables.Values;
                return true;
            }
            savables = null;
            return false;
        } 

        private void AddEntries(ICollection<SavableEntry> collection, ICollection<ISavableEntity> savables)
        {
            if (collection == null || savables == null) return;
            
            foreach (var savable in savables)
            {
                try
                {
                    if (!savable.IsAlive()) continue;
                    if (savable.CaptureState() is not { } captured) continue;

                    if (captured is Dictionary<string, object> captures)
                    {
                        foreach (var (typeName, capture) in captures)
                        {
                            AddNewEntry(collection, typeName, capture, savable);
                        }
                    }
                    else
                    {
                        var typeName = captured.GetType().AssemblyQualifiedName;
                        AddNewEntry(collection, typeName, captured, savable);
                    }
                }
                catch (Exception e) { Debug.LogError($"[SaveSystem] error occured while AddEntries() - {e}");}
            }
        }

        private void AddNewEntry(ICollection<SavableEntry> collection, string typeName, object stateObj, ISavableEntity entity)
        {
            if (GetTypeByName(typeName) is not { } type) return;
            RegisterEntries(stateObj, type, collection, entity.UniqueIdentifier, typeName);
        }

        private static void RegisterEntries(object stateObj, Type type, ICollection<SavableEntry> targetEntryCollection, 
            string identifier, string typeName)
        {
            try
            {
                string json = JsonSerialization.ToJson(stateObj, new JsonSerializationParameters
                {
                    SerializedType = type,
                });

                targetEntryCollection.Add(new SavableEntry
                {
                    id = identifier,
                    typeName = typeName,
                    jsonPayload = json
                });
            }
            catch (Exception e) { Debug.LogError($"[SaveSystem] Failed to serialize {typeName}: {e.Message}"); }
        }

        // SavableEntity 에 상태 복원
        private void RestoreState(SaveFileData data)
        {
            this.Log("RestoreState(SaveFileData) 시작");
            List<SavableEntry> entries = new(); // 세이브 데이터 리스트 생성
            var currentSceneEntry = sceneCatalog.GetCurrentSceneEntry(); // 현재 씬 정보 캡처
            this.Log($"scene Entry: {currentSceneEntry} - {currentSceneEntry?.key}", Logg.LoggingMode.Completed);
            // entries에 세이브 엔트리 목록 반영
            GetEntryFromSave(data, entries, currentSceneEntry);
            this.Log($"GetEntryFromSave 완료 - currentSceneEntry: {currentSceneEntry}, entries: {entries.Count}", Logg.LoggingMode.Completed);
            
            // <고유 식별자, 고유 객체의 <타입, 세이브 데이터>> 딕셔너리 생성 (grouped)
            var grouped = new Dictionary<string, Dictionary<string, object>>(entries.Count); 
            // json to runtime data 파싱 -> grouped에 등록
            ExtractSaveData(entries, grouped);
            this.Log($"ExtractSaveData 완료 - currentSceneEntry: {currentSceneEntry}, grouped keys: {grouped.Count}", Logg.LoggingMode.Completed);
            
            // 파싱된 런타임 데이터 반영 (글로벌)
            this.Log($"글로벌 엔티티 복원 시작 - GlobalEntities: {GlobalEntities.Count}", Logg.LoggingMode.Completed);
            RestoreState(GlobalEntities.Values, grouped);
            // 파싱된 런타임 데이터 반영 (현재 씬)
            if (currentSceneEntry != null &&
                SceneEntities.TryGetValue(currentSceneEntry, out var sceneSavables))
            {
                RestoreState(sceneSavables.Values, grouped);
                this.Log($"씬 엔티티 복원 완료 - count: {sceneSavables.Count}");
            }
            else
            {
                Logg.LogWarning($"[{GetType().Name}] RestoreState() - 현재 씬의 엔티티 없음 (currentSceneEntry: {currentSceneEntry})");
            }
            
            this.Log("LoadedStateCache 업데이트 시작", Logg.LoggingMode.Completed);
            foreach (var (id, stateDict) in grouped)
            {
                LoadedStateCache.TryAdd(id, stateDict);
            }
            this.Log("RestoreState(SaveFileData) 완료", Logg.LoggingMode.Completed);
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
                entries.AddRange(globEntries); 
            else Logg.Log("[SaveSystem] No saved global data", Logg.LoggingMode.Completed);
        }

        private void ExtractSaveData(List<SavableEntry> entries, Dictionary<string, Dictionary<string, object>> grouped)
        {
            foreach (var entry in entries)
            {
                // 타입명으로 데이터 타입 조회(or 리플렉션 생성)
                if (GetTypeByName(entry.typeName) is not { } type) 
                {
                    Logg.LogError($"[{nameof(SaveSystem)}.{nameof(RestoreState)}()] Type not found: {entry.typeName}");
                    continue;
                }

                object state; // 런타임 타입 세이브 데이터
                try
                {
                    // json-> 런타임 세이브 데이터 생성
                    state = GetMethodByType(type)?.Invoke(null, new object[]
                    {
                        entry.jsonPayload,
                        new JsonSerializationParameters { SerializedType = type }
                    });

                    if (state == null)
                    {
                        Logg.LogError($"[{nameof(SaveSystem)}.{nameof(RestoreState)}()] FromJson Method missing for: {type.FullName}");
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Logg.LogError($"[SaveSystem] Restore failed for {entry.typeName}: {e}");
                    continue;
                }
                // 고유 객체별 세이브 데이터 딕셔너리 조회
                if (!grouped.TryGetValue(entry.id, out var dict))
                {
                    dict = new Dictionary<string, object>(); 
                    grouped[entry.id] = dict; // 딕셔너리에 없으면 신규 등록
                }

                dict[entry.typeName] = state; // 해당 객체의 (데이터 타입명-데이터) 저장
            }
        }

        private void RestoreState(IEnumerable<ISavableEntity> entities,
            IReadOnlyDictionary<string, Dictionary<string, object>> stateGroup)
        {
            foreach (var entity in entities)
            {
                if (!entity.IsAlive()) continue;
                if (!stateGroup.TryGetValue(entity.UniqueIdentifier,
                        out var states)) continue;
                entity.RestoreState(states);
            }
        }

        #endregion
        
        #region File I/O (LoadFile, SaveFile)

        private SaveFileData LoadFile(string saveFile)
        {
            this.Log("LoadFile() 시작 - saveFile: {saveFile}");
            string path = GetPathFromSaveFile(saveFile);
            this.Log("파일 경로: {path}");
            if (!File.Exists(path))
            {
                this.Log("파일 없음 - 새 SaveFileData 반환");
                return new SaveFileData();
            }
            
            this.Log("[SaveSystem] 파일 존재 - 읽기 시도");
            try
            {
                // json -> 런타임 데이터로 파싱 시도
                string json = File.ReadAllText(path);
                return JsonSerialization.FromJson<SaveFileData>(json); 
            }
            catch (Exception e)
            {
                // 세이브파일 파싱 실패 시 빈 세이브 파일 생성 및 반환
                Debug.LogError($"[SaveSystem] Failed to load file {path}: {e.Message}");
                return new SaveFileData();
            }
        }
        
        private void SaveFile(string saveFile, SaveFileData data)
        {
            this.Log("SaveFile() 시작 - saveFile: {saveFile}");
            string path = GetPathFromSaveFile(saveFile);
            this.Log("저장 경로: {path}");
            // 디렉토리 확보
            var dir  = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            // var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; // 임시 파일명
            // var bak = path + ".bak"; // 백업 파일명
            
            var tmp = Path.Combine(dir ?? "", $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            var bak = path + ".bak";

            string json;
            this.Log("JSON 직렬화 시작");
            try
            {
                // json 직렬화 시도
                json = JsonSerialization.ToJson(
                    data,
                    new JsonSerializationParameters
                    {
                        DisableSerializedReferences = true
                    });
            }
            catch (Exception e)
            {
                Logg.LogError($"[{nameof(SaveSystem)}.{nameof(SaveFile)}()] JsonSerialization failed {e}");
                return; // 데이터 직렬화 실패 시 중지
            }
            
            this.Log("JSON 직렬화 완료 - 길이: {json.Length} chars");
            this.Log("임시 파일 쓰기 시작");
            try
            {
                using var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None);
                using var sw = new StreamWriter(fs);
                sw.Write(json);
                sw.Flush();
                fs.Flush(true);
            }
            catch (Exception e)
            {
                Logg.LogError($"[{nameof(SaveSystem)}.{nameof(SaveFile)}()] Writing tmp failed: {tmp}, {e}");
                return; // tmp 파일 생성 실패 시 중지
            }
            
            this.Log("[SaveSystem] 임시 파일 쓰기 완료");
            // 3) Replace 시도
            this.Log("[SaveSystem] 파일 교체 시도");
            // (path = tmp)
            try
            {
                if (File.Exists(path)) // 성공 시: bak = path, path = tmp 으로 교체
                    File.Replace(tmp, path, bak);   
                else File.Move(tmp, path); // 실패 시 : path에 저장
            }
            catch (Exception e)
            {
                Logg.Log($"[SaveSystem.SaveFile] Replace fallback: {e.Message}", Logg.LoggingMode.Completed);
                try
                {
                    // 백업 시도
                    if (File.Exists(path))
                    {
                        // 백업 실패 시 throw 하지 않고 그대로 overwrite 시도
                        try { File.Copy(path, bak, overwrite: true); } catch { }
                        try { File.Delete(path); } catch { }
                    }

                    // Move가 막히면 Copy(overwrite)
                    try { File.Move(tmp, path); }
                    catch { File.Copy(tmp, path, overwrite: true); File.Delete(tmp); }
                }
                catch (Exception fbEx) { Logg.LogError($"[SaveSystem.SaveFile] Fallback failed: {fbEx}"); }
            }
            this.Log("[SaveSystem] SaveFile() 완료");
        }

        // 경로 생성 (임시)
        private string GetPathFromSaveFile(string saveFile)
        {
            return Path.Combine(Application.persistentDataPath, saveFile + ".sav");
        }
        
        #endregion
        
        #region Method Info

        private static readonly Dictionary<string, Type> BuiltinAliasTypes = new(StringComparer.Ordinal)
        {
            ["bool"] = typeof(bool),
            ["byte"] = typeof(byte),
            ["sbyte"] = typeof(sbyte),
            ["char"] = typeof(char),
            ["decimal"] = typeof(decimal),
            ["double"] = typeof(double),
            ["float"] = typeof(float),
            ["int"] = typeof(int),
            ["uint"] = typeof(uint),
            ["long"] = typeof(long),
            ["ulong"] = typeof(ulong),
            ["short"] = typeof(short),
            ["ushort"] = typeof(ushort),
            ["string"] = typeof(string),
        };

        private static readonly MethodInfo FromJsonOpenGeneric = 
            typeof(JsonSerialization).GetMethod(
                "FromJson",
                BindingFlags.Public | BindingFlags.Static, 
                null,
                new[] { typeof(string), typeof(JsonSerializationParameters) }, 
                null
                );
        
        // 리플렉션 기반 Type to MethodInfo
        private MethodInfo GetMethodByType(Type type)
        {
            if (type == null) return null;
            
            if (CachedMethodInfos.TryGetValue(type, out var result))
            {
                return result;
            }

            try
            {
                var method = FromJsonOpenGeneric.MakeGenericMethod(type);
                CachedMethodInfos[type] = method;
                return method;
            }
            catch (Exception e)
            {
                Logg.LogError($"[{nameof(SaveSystem)}] MakeGenericMethod failed: {type.FullName}, {e.Message}");
                return null;
            }
        }

        // 리플렉션 기반 Name to Type
        private Type GetTypeByName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            
            if (CachedTypes.TryGetValue(typeName, out var t)) return t;

            if (BuiltinAliasTypes.TryGetValue(typeName, out var aliasType))
            {
                CachedTypes[typeName] = aliasType;
                return aliasType;
            }

            var type = Type.GetType(typeName);
            if (type == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) // 추후 불필요해지면 삭제 고려
                {
                    type = assembly.GetType(typeName);
                    if (type != null) break;
                }
            }
            
            CachedTypes[typeName] = type;
            return type;
        }
        
        #endregion

        #region Register/UnRegister Entity

        public void RegisterEntity(ISavableEntity entity, CancellationToken token = default)
        {
            var id = entity.UniqueIdentifier;
            this.Log($"RegisterEntity({entity.GetType()}) - id: {id}, IsGlobal: {entity.IsGlobal}", Logg.LoggingMode.Completed);

            if (entity.IsGlobal)
            {
                // 덮어쓰기 적용
                // 글로벌 ISavable 구현 객체는 자체적으로 IsRegistered 기준으로 중복 등록 방지 필요
                GlobalEntities[id] = entity;

                if (entity.IsAlive() && LoadedStateCache.TryGetValue(id, out var stateDict))
                {
                    entity.RestoreState(stateDict);
                }

                entity.IsRegistered = true;
                return;
            }

            if (sceneCatalog == null)
            {
                catalogPending.Enqueue(entry => AddSceneSavableEntity(entry, entity, token));
                return;
            }

            if (!sceneCatalog.TryGetSceneEntry(entity.TargetScene, out var sceneEntry)
                && !sceneCatalog.TryGetCurrentSceneEntry(out sceneEntry))
            {
                Logg.LogWarning($"RegisterEntity({entity.GetType()}) - SceneEntry {sceneEntry} not found");
                return;
            }
            
            AddSceneSavableEntity(sceneEntry, entity, token);
        }

        public void UnRegisterEntity(ISavableEntity entity, CancellationToken token = default)
        {
            var id = entity.UniqueIdentifier;

            if (entity.IsGlobal)
            {
                GlobalEntities.Remove(id);
                entity.IsRegistered = false;
                return;
            }

            if (!sceneCatalog.IsAlive() || !sceneCatalog.TryGetCurrentSceneEntry(out var currSceneEntry))
                return;
            
            if (!SceneEntities.TryGetValue(currSceneEntry, out var dict))
                return;

            entity.IsRegistered = false;
            dict.Remove(id);
        }
        
        private static void AddSceneSavableEntity(SceneEntry sceneEntry, ISavableEntity entity, CancellationToken token)
        {
            if (token.IsCancellationRequested || sceneEntry == null || !entity.IsAlive()) return;

            if (!SceneEntities.TryGetValue(sceneEntry, out var dict))
            {
                dict = new Dictionary<string, ISavableEntity>();
                SceneEntities[sceneEntry] = dict;
            }

            dict.TryAdd(entity.UniqueIdentifier, entity);

            if (LoadedStateCache.TryGetValue(entity.UniqueIdentifier, out var stateDict))
            {
                entity.RestoreState(stateDict);
            }

            entity.IsRegistered = true;
            Logg.Log($"[SaveSystem] SceneEntity({entity.GetType()}, {entity.UniqueIdentifier}) is added in sceneEntry: {sceneEntry}", Logg.LoggingMode.Completed);
        }

        #endregion
        
    }
}
