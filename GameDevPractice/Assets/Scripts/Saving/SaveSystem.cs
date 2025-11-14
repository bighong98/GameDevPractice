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
using RPG.Saving;
using TH.Resource;
using TH.Utils;

namespace TH.SaveLoad
{
    public class SaveSystem : ISaveSystem
    {
        // CachedTypes: 캐싱된 런타임 저장 데이터 타입 정보
        // CachedMethodInfos: 캐싱된 타입 매서드 정보
        // LoadedStateCache: 캐싱된 런타임 인스턴스 세이브 데이터
        private static readonly Dictionary<string, Type> CachedTypes = new();
        private static readonly Dictionary<Type, MethodInfo> CachedMethodInfos = new();
        private static readonly Dictionary<string, Dictionary<string, object>> LoadedStateCache = new(); // Non-MB 클래스 데이터
        // SceneEntities: 씬별 세이브 객체, GlobalEntities: 씬 무관 글로벌 세이브 객체
        private static readonly Dictionary<SceneEntry, Dictionary<string, ISavableEntity>> SceneEntities = new();
        private static readonly Dictionary<string, ISavableEntity> GlobalEntities = new();
        
        // 외부 서비스 의존 (from ServiceLocator)
        private readonly IResourceLoader resourceLoader;
        private readonly ISceneLoader sceneLoader;
        
        // catalogPending: 씬 카탈로그 로드 완료 전 등록/해제 요청 대기 큐
        // catalogResolveTCS: 씬 카탈로드 로드 비동기 대기 TCS
        // catalogResolved: 다중 waiter 대응 객체
        private readonly ConcurrentQueue<Action<SceneEntry>> catalogPending = new();
        private readonly UniTaskCompletionSource<SceneCatalogSO> catalogResolveTCS = new();
        private readonly UniTask<SceneCatalogSO> catalogResolved;
        private SceneCatalogSO sceneCatalog;
        // 씬 카탈로그 로드 키
        private const string SceneCatalogKey = "SceneCatalogSO"; 
        private const int DefaultSceneIndexInCatalog = 0;
        
        // 중복 Save/Load 요청 플래그, 임시 캐시
        private bool isLoading;
        private bool saveRequested;
        private string requestedSaveFile;
        private SceneEntry requestedSceneEntry;
        
        // SaveFile(), LoadFile() - I/O 동기화 세마포어 (반드시 사용 전 유니티 메인 스레드 환경 보장 필요)
        private readonly SemaphoreSlim ioSemaphore = new (1, 1);
        
        public SaveSystem(ISceneLoader sceneLoader, IResourceLoader resourceLoader)
        {
            // 외부 서비스 의존 주입
            this.sceneLoader = sceneLoader;
            this.resourceLoader = resourceLoader;
            // 씬 카탈로그 대기용 UniTask TCS 초기화, 카탈로그 비동기 로드 시작
            catalogResolved = catalogResolveTCS.Task.Preserve();
            LoadSceneCatalogAsync().Forget();
        }

        #region Initialization

        private async UniTask LoadSceneCatalogAsync()
        {
            try
            {
                // IResourceLoader로부터 Addressables 기반 카탈로그 비동기 로드
                sceneCatalog = await resourceLoader.LoadAsync<SceneCatalogSO>(SceneCatalogKey);
                // 카탈로그 로딩 waiter들 대기 해제
                catalogResolveTCS.TrySetResult(sceneCatalog);
                // 로드 전 요청된 등록/해제 요청 처리
                await RunPendingJobsAsync();
            }
            catch (Exception e)
            {
                catalogResolveTCS.TrySetException(e);
                Logg.LogError($"[SaveSystem] Load Scene Catalog failed - {e}");
#if UNITY_EDITOR
                throw;
#endif
            }
        }
        
        // 씬 카탈로그 로드 전 ISavableEntity 자가 등록/해제 요청 일괄 처리
        // CancellationToken으로 유효성 검사하여 씬 이동 전 들어온 요청은 실행x
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

        // SceneCatalogSO 비동기 대기
        private async UniTask WaitForCatalog(CancellationToken token = default)
        {
            if (sceneCatalog != null) return; // sceneCatalog가 이미 세팅되어 있다면 await 없이 즉시 종료
            await catalogResolved.AttachExternalCancellation(token);
        }

        #endregion
        
        #region Load Last Scene

        // 저장 시점 씬 불러오기 (기본 세팅: 게임 시작 후 자동으로 호출)
        // 해당 씬 관련 세이브 데이터 + 글로벌 데이터 자동 적용
        public async UniTask LoadLastScene(string saveFile)
        {
            await RunExclusive(async () =>
            {
                if (isLoading) return;
                isLoading = true;
                try
                {
                    // 파일 I/O 접근은 풀 스레드에서 처리
                    await UniTask.SwitchToThreadPool();
                    if (LoadFile(saveFile) is not { } data) return;
                    // 씬 카탈로그(ScriptableObject) 접근은 메인 스레드에서 처리
                    await UniTask.SwitchToMainThread();
                    await WaitForCatalog(); // scene catalog 보장

                    // 저장된 씬이 없다면 디폴트 씬으로 이동
                    if (data.lastSceneEntry is not { sceneRef: { } key })
                        key = sceneCatalog.entries[DefaultSceneIndexInCatalog].sceneRef; 
                    // 씬 이동
                    await GameSceneManager.Instance.LoadSceneAsync(key);
                    await UniTask.Yield();
                    // 해당 씬 관련 + 글로벌 세이브 데이터 적용
                    RestoreState(data);
                }
                finally { isLoading = false; } // 로딩 종료 알림
            });
        }

        #endregion
        
        #region Save/Load/Delete (Async + public)

        // 비동기 세이브 
        public async UniTask SaveAsync(string saveFile, SceneEntry sceneEntry = null)
        {
            // 이미 로딩 중인 경우 실행x
            // isLoading, SemaphoreSlim.CurrentCount 둘다 thread-safe 하지 않음에 주의 (SaveAsync() 호출 전 메인 스레드 보장 필요)
            if (isLoading || ioSemaphore.CurrentCount == 0)
            {
                Logg.Log($"[SaveSystem] ioSemaphore.CurrentCount: {ioSemaphore.CurrentCount}", Logg.LoggingMode.InProgress);
                CoalesceSave(saveFile, sceneEntry);
                return;
            }

            await WaitForCatalog(); // scene catalog 보장
            await RunExclusive(async () => {
                // 병합된 요청이 있다면 우선 처리
                if (saveRequested)
                {
                    saveFile = requestedSaveFile ?? saveFile;
                    sceneEntry = requestedSceneEntry ?? sceneEntry;
                    ResetSaveRequest();
                }
                // 세이브 프로세스 실행
                try { await SaveCoreAsync(saveFile, sceneEntry); }
                catch (Exception e) { Logg.LogError($"[SaveSystem] SaveAsync() failed: {e.Message}"); }
                // 세이브 중 들어온 추가 세이브 요청들 처리
                try
                {
                    while (TryDequeueCoalescedSave(out var nextSaveFile, out var nextSaveEntry))
                    {
                        await SaveCoreAsync(nextSaveFile, nextSaveEntry);
                    }
                }
                catch (Exception e) { Logg.LogError($"[SaveSystem] SaveAsync() failed: + loop {e.Message}"); }
            });
        }
        
        // 비동기 세이브 삭제
        public async UniTask DeleteAsync(string saveFile)
        {
            await RunExclusive(async () =>
            {
                await UniTask.SwitchToThreadPool();
                try {Delete(saveFile);}
                catch (Exception e) { Logg.LogError($"[SaveSystem] Delete failed - {e}");}
            });
        }

        // 비동기 로드
        public async UniTask LoadAsync(string saveFile)
        {
            await RunExclusive(async () =>
            {
                if (isLoading) return;
                isLoading = true;

                try
                {
                    await UniTask.SwitchToMainThread(); // 메인 스레드 환경 보장
                    await WaitForCatalog(); // SceneCatalogSO 로드 보장
                    await LoadCoreAsync(saveFile); // 로드 프로세스 실행
                }
                catch (Exception e) { Logg.LogError($"[SaveSystem] LoadAsync() failed: {e.Message}"); }
                finally { isLoading = false; }
            });
        }
        
        private void ResetSaveRequest()
        {
            saveRequested = false;
            requestedSaveFile = null;
            requestedSceneEntry = null;
        }
        
        // Save/Load/Delete가 공유하는 동기화 객체
        private async UniTask RunExclusive(Func<UniTask> func)
        {
            await ioSemaphore.WaitAsync();
            try { await func(); }
            finally { ioSemaphore.Release(); }
        }
        
        // 복수의 세이브 요청 병합 (마지막 요청만 남김)
        private void CoalesceSave(string saveFile, SceneEntry sceneEntry)
        {
            saveRequested = true;
            requestedSaveFile = saveFile;
            requestedSceneEntry = sceneEntry;
            Logg.Log("[SaveSystem] Save queued (coalesced to latest)", Logg.LoggingMode.InProgress);
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

        #region Save/Load/Delete Core (private)

        private async UniTask SaveCoreAsync(string saveFile, SceneEntry sceneEntry = null)
        {
            await UniTask.SwitchToThreadPool();
            SaveFileData data = LoadFile(saveFile);
            
            List<SavableEntry> sceneEntries = new List<SavableEntry>();
            List<SavableEntry> globalEntries = new List<SavableEntry>();
            // 현재 씬 + 글로벌 런타임 데이터 수집
            await UniTask.SwitchToMainThread();
            CaptureState(sceneEntries, globalEntries);
            // 현재 씬 정보 갱신
            sceneEntry ??= sceneCatalog.GetCurrentSceneEntry();
            data.sceneData[sceneEntry.sceneId] = sceneEntries;
            data.globalData = globalEntries;
            data.lastSceneEntry = sceneEntry;
            // 세이브 파일 저장
            await UniTask.SwitchToThreadPool();
            SaveFile(saveFile, data);
        }
        
        private async UniTask LoadCoreAsync(string saveFile)
        {
            await UniTask.SwitchToThreadPool();
            var data = LoadFile(saveFile);
            if (data == null) return;

            await UniTask.SwitchToMainThread();
            RestoreState(data);
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
            AddEntries(globalEntries, GlobalEntities.Values);
            if (GetCurrentSceneSavables(out var sceneSavableCollection))
                AddEntries(sceneEntries, sceneSavableCollection);
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

        // 개별 ISavableEntity의 CaptureState() 호출
        // ISavableEntity는 MonoBehaviour일 수도 있으므로 메인 스레드 환경 보장 필요
        private void AddEntries(ICollection<SavableEntry> collection, ICollection<ISavableEntity> savables)
        {
            if (collection == null || savables == null) return;
            
            foreach (var savable in savables)
            {
                try
                {
                    if (!savable.IsAlive()) continue;
                    if (savable.CaptureState() is not { } captured) continue;
                    // 복수 ISavable 대응
                    if (captured is Dictionary<string, object> captures)
                    {
                        foreach (var (typeName, capture) in captures)
                        {
                            AddNewEntry(collection, typeName, capture, savable);
                        }
                    }
                    else // 1:1 ISavableEntity:ISavable
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
        // 런타임 데이터 -> json 직렬화 수행
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
            List<SavableEntry> entries = new(); // 세이브 데이터 리스트 생성
            var currentSceneEntry = sceneCatalog.GetCurrentSceneEntry(); // 현재 씬 정보 캡처
            
            // entries에 세이브 엔트리 목록 반영
            GetEntryFromSave(data, entries, currentSceneEntry);
            // <고유 식별자, 고유 객체의 <타입, 세이브 데이터>> 딕셔너리 생성 (grouped)
            var grouped = new Dictionary<string, Dictionary<string, object>>(entries.Count); 
            // json to runtime data 파싱 -> grouped에 등록
            ExtractSaveData(entries, grouped);
            
            // 파싱된 런타임 데이터 반영 (글로벌)
            RestoreState(GlobalEntities.Values, grouped);
            // 파싱된 런타임 데이터 반영 (현재 씬)
            if (SceneEntities.TryGetValue(currentSceneEntry, out var sceneSavables))
                RestoreState(sceneSavables.Values, grouped);
            
            foreach (var (id, stateDict) in grouped)
            {
                LoadedStateCache.TryAdd(id, stateDict);
            }
        }

        private static void GetEntryFromSave(SaveFileData data, List<SavableEntry> entries, SceneEntry currentSceneEntry)
        {
            var sceneEntries = data.sceneData;
            // 현재 씬 세이브 데이터 추가
            if (sceneEntries.TryGetValue(currentSceneEntry.sceneId, out var targetSceneEntries))
                entries.AddRange(targetSceneEntries); // 세이브 데이터 리스트에 추가
            else Logg.Log($"[SaveSystem] No saved data for scene '{currentSceneEntry.key}'", Logg.LoggingMode.InProgress);
            // 글로벌(특정 씬에 종속되지 않는) 세이브 데이터 추가
            if (data.globalData is { Count: > 0 } globEntries)
                entries.AddRange(globEntries); 
            else Logg.Log("[SaveSystem] No saved global data", Logg.LoggingMode.InProgress);
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
            string path = GetPathFromSaveFile(saveFile);
            if (!File.Exists(path)) return new SaveFileData();

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
            string path = GetPathFromSaveFile(saveFile);
            // 디렉토리 확보
            var dir  = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var tmp = Path.Combine(dir ?? "", $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            var bak = path + ".bak";

            string json;
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

            // 3) Replace 시도 (path = tmp)
            try
            {
                if (File.Exists(path)) // 성공 시: bak = path, path = tmp 으로 교체
                    File.Replace(tmp, path, bak);   
                else File.Move(tmp, path); // 실패 시 : path에 저장
            }
            catch (Exception e)
            {
                Logg.Log($"[SaveSystem.SaveFile] Replace fallback: {e.Message}", Logg.LoggingMode.InProgress);
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

            if (entity.IsGlobal)
            {
                GlobalEntities.TryAdd(id, entity);
                return;
            }

            if (sceneCatalog == null)
                catalogPending.Enqueue(entry => AddSceneSavableEntity(entry, entity, token));
            else AddSceneSavableEntity(sceneCatalog.GetCurrentSceneEntry(), entity, token);
        }

        public void UnRegisterEntity(ISavableEntity savable, CancellationToken token = default)
        {
            var id = savable.UniqueIdentifier;

            if (savable.IsGlobal)
            {
                GlobalEntities.Remove(id);
                return;
            }

            if (sceneCatalog == null) return;
            var currSceneEntry = sceneCatalog.GetCurrentSceneEntry();
            if (!SceneEntities.TryGetValue(currSceneEntry, out var dict))
                return;

            dict.Remove(id);
        }
        
        private static void AddSceneSavableEntity(SceneEntry sceneEntry, ISavableEntity entity, CancellationToken token)
        {
            if (token.IsCancellationRequested || !entity.IsAlive()) return;

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
        }

        #endregion
        
    }
}
