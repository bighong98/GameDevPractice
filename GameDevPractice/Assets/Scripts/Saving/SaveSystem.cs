using System;
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
        private static readonly Dictionary<Type, MethodInfo> CachedMethodInfos = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<string, Type> CachedTypes = new Dictionary<string, Type>();

        private readonly IResourceLoader resourceLoader;
        private SceneCatalogSO sceneCatalog;
        private static readonly string SceneCatalogKey = "SceneCatalogSO";
        private const int DefaultSceneIndexInCatalog = 0;
        
        private readonly SemaphoreSlim ioSemaphore = new SemaphoreSlim(1, 1);
        
        private bool isLoading;
        private bool saveRequested;
        private string requestedSaveFile;
        private SceneEntry requestedSceneEntry;

        public SaveSystem(ISceneLoader sceneLoader, IResourceLoader resourceLoader)
        {
            this.resourceLoader = resourceLoader;
            resourceLoader.NotifyResourceLoad += LoadSceneCatalog;
        }
        
        private void LoadSceneCatalog(string label)
        {
            if (!string.Equals(label, resourceLoader.PreLoadLabel)) return;
            if (!resourceLoader.TryLoad(SceneCatalogKey, out sceneCatalog))
                Logg.LogError($"[SaveSystem] failed to load scene catalog");
        }

        private async UniTask WaitForCatalog()
        {
            await UniTask.WaitUntil(resourceLoader.IsPreLoadDone);
            resourceLoader.TryLoad(SceneCatalogKey, out sceneCatalog);
        }
        
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

                    await UniTask.SwitchToMainThread();
                    await UniTask.Yield(); // 1프레임 지연

                    if (sceneCatalog == null)
                        await WaitForCatalog();

                    if (data.lastSceneEntry is not { sceneRef: { } key })
                        key = sceneCatalog.entries[DefaultSceneIndexInCatalog].sceneRef; // 저장된 씬이 없다면 디폴트 씬으로 이동

                    await GameSceneManager.Instance.LoadSceneAsync(key);
                    await UniTask.Yield(); // 1프레임 지연

                    RestoreState(data);
                }
                finally { isLoading = false; }
            });
        }

        #endregion
        
        #region Save/Load/Delete (Async + public)

        public async UniTask SaveAsync(string saveFile, SceneEntry sceneEntry = null)
        {
            if (isLoading)
            {
                CoalesceSave(saveFile, sceneEntry);
                return;
            }

            if (sceneCatalog == null)
                await WaitForCatalog();
            
            await RunExclusive(async () => {
                
                if (saveRequested)
                {
                    saveFile = requestedSaveFile ?? saveFile;
                    sceneEntry = requestedSceneEntry ?? sceneEntry;
                    ResetSaveRequest();
                }
                
                try
                {
                    await UniTask.SwitchToMainThread();
                    Save(saveFile, sceneEntry);
                    await UniTask.Yield();

                    while (TryDequeueCoalescedSave(out var nextSaveFile, out var nextSaveEntry))
                    {
                        await UniTask.SwitchToMainThread();
                        Save(nextSaveFile, nextSaveEntry);
                        await UniTask.Yield();
                    }
                }
                catch (Exception e) { Logg.LogError($"[SaveSystem] SaveAsync() failed: {e.Message}"); }
            });
        }

        private void ResetSaveRequest()
        {
            saveRequested = false;
            requestedSaveFile = null;
            requestedSceneEntry = null;
        }

        public async UniTask DeleteAsync(string saveFile)
        {
            await RunExclusive(async () =>
            {
                Delete(saveFile);
                await UniTask.Yield();
            });
        }

        public async UniTask LoadAsync(string saveFile)
        {
            await RunExclusive(async () =>
            {
                if (isLoading) return;
                isLoading = true;
                try
                {
                    await UniTask.SwitchToMainThread();
                    Load(saveFile);
                    await UniTask.Yield();
                }
                catch (Exception e) { Logg.LogError($"[SaveSystem] LoadAsync() failed: {e.Message}"); }
                finally { isLoading = false; }
            });
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
            Logg.Log("[SaveSystem] Save queued (coalesced to latest)", Logg.LoggingMode.InProgress);
        }

        private bool TryDequeueCoalescedSave(out string file, out SceneEntry entry)
        {
            if (!saveRequested)
            {
                file = null; 
                entry = null; 
                return false;
            }
            
            file = requestedSaveFile; 
            entry = requestedSceneEntry;
            
            saveRequested = false; 
            requestedSaveFile = null; 
            requestedSceneEntry = null;
            
            return true;
        }

        #endregion

        #region Save/Load/Delete (private)

        private void Save(string saveFile, SceneEntry sceneEntry = null)
        {
            SaveFileData data = LoadFile(saveFile);

            data.lastSceneEntry = sceneEntry;
            
            List<SavableEntry> sceneEntries = new List<SavableEntry>();
            List<SavableEntry> globalEntries = new List<SavableEntry>();
            CaptureState(sceneEntries, globalEntries);
            
            data.sceneData[sceneCatalog.GetCurrentSceneEntry().sceneId] = sceneEntries;
            data.globalData = globalEntries;
            data.lastSceneEntry = sceneCatalog.GetCurrentSceneEntry();

            SaveFile(saveFile, data);
        }
        
        private void Load(string saveFile)
        {
            var data = LoadFile(saveFile);
            if (data == null) return;

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
            foreach (var entity in UnityEngine.Object.FindObjectsByType<SavableEntity>(UnityEngine.FindObjectsSortMode.None))
            {
                var targetEntryList = entity.IsGlobal ? globalEntries : sceneEntries;
                var stateDict = entity.CaptureState();

                foreach (var (typeName, stateObj) in stateDict)
                {
                    var type = GetTypeByName(typeName);
                    if (type == null) continue;

                    RegisterEntries(stateObj, type, targetEntryList, entity.UniqueIdentifier, typeName);
                }
            }

            foreach (var savable in Registers.Values)
            {
                var stateObj = savable.CaptureState();
                if (stateObj == null) continue;
                
                var typeName = stateObj.GetType().AssemblyQualifiedName;
                var type = GetTypeByName(typeName);
                if (string.IsNullOrEmpty(typeName) || type == null) continue;
                
                RegisterEntries(stateObj, type, globalEntries, savable.UniqueIdentifier, typeName);
            }
        }

        private static void RegisterEntries(object stateObj, Type type, List<SavableEntry> targetEntryList, 
            string identifier, string typeName)
        {
            try
            {
                string json = JsonSerialization.ToJson(stateObj, new JsonSerializationParameters
                {
                    SerializedType = type
                });

                targetEntryList.Add(new SavableEntry
                {
                    id = identifier,
                    typeName = typeName,
                    jsonPayload = json
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to serialize {typeName}: {e.Message}");
            }
        }

        // SavableEntity 에 상태 복원
        private void RestoreState(SaveFileData data)
        {
            // var sceneName = SceneManager.GetActiveScene().name;
            var sceneEntries = data.sceneData;
            List<SavableEntry> entries = new(); // 세이브 데이터 리스트 생성
            
            // 현재 씬 세이브 데이터 검색
            var currentSceneEntry = sceneCatalog.GetCurrentSceneEntry();
            if (sceneEntries.TryGetValue(currentSceneEntry.sceneId, out var targetSceneEntries))
                entries.AddRange(targetSceneEntries); // 세이브 데이터 리스트에 추가
            else Logg.Log($"[SaveSystem] No saved data for scene '{currentSceneEntry.key}'", Logg.LoggingMode.InProgress);
            
            if (data.globalData is { Count: > 0 } globEntries)
                entries.AddRange(globEntries); // 글로벌(특정 씬에 종속되지 않는) 세이브 데이터 리스트에 추가
            else Logg.Log("[SaveSystem] No saved global data");

            var grouped = new Dictionary<string, Dictionary<string, object>>(entries.Count); // <고유 식별자, 고유 객체의 <타입, 타입 데이터>> 딕셔너리 생성

            foreach (var entry in entries)
            {
                if (GetTypeByName(entry.typeName) is not { } type) // 타입명으로 데이터 타입 조회(or 리플렉션 생성)
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
            // MonoBehaviour 기반 ISavable 클래스 세이브 데이터 적용
            foreach (var entity in UnityEngine.Object.FindObjectsByType<SavableEntity>(UnityEngine.FindObjectsSortMode.None))
            {
                string id = entity.UniqueIdentifier;
                if (grouped.TryGetValue(id, out var stateDict))
                {
                    entity.RestoreState(stateDict);
                }
            }
            // Non-Mono(일반 C#) ISavable 클래스 세이브 데이터 적용
            foreach (var savable in Registers.Values)
            {
                if (!grouped.TryGetValue(savable.UniqueIdentifier, out var stateDict)) continue;

                foreach (var entry in stateDict)
                {
                    var savedTypeName = entry.Key;
                    var savedData = entry.Value;
                    if (savedData == null) continue;

                    try { savable.RestoreState(savedData); }
                    catch (Exception e) {Logg.LogError($"[SaveSystem] Restore failed. ({savedTypeName}, {savedData}): {e}");}
                }
            }
            
            foreach (var (id, stateDict) in grouped)
            {
                if (Registers.ContainsKey(id)) continue;
                Registry[id] = stateDict;
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
            
            // var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; // 임시 파일명
            // var bak = path + ".bak"; // 백업 파일명
            
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
        
        private static readonly Dictionary<string, Dictionary<string, object>> Registry = new(); // Non-MB 클래스 데이터
        private static readonly Dictionary<string, ISavableWithId> Registers = new(); // Non-MB 클래스
        public void Register(ISavableWithId savable)
        {
            Registers[savable.UniqueIdentifier] = savable;

            if (!Registry.TryGetValue(savable.UniqueIdentifier, out var saved)) return;
            try
            {
                foreach (var s in saved.Values)
                {
                    if (s == null) continue;
                    savable.RestoreState(s);
                }
            }
            catch (Exception e) {Logg.LogError($"[SaveSystem] Register.Restore failed {e}");}
        }

        public void UnRegister(ISavableWithId savable)
        {
            if (savable == null || string.IsNullOrEmpty(savable.UniqueIdentifier)) return;
            Registers.Remove(savable.UniqueIdentifier);
        }
    }
}
