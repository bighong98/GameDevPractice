using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        
        private SceneCatalogSO sceneCatalog;
        private const int DefaultSceneIndexInCatalog = 0;

        private static readonly string SceneCatalogKey = "SceneCatalogSO";
        
        public SaveSystem()
        {
            ResourceManager.Instance.ReserveOperation(() =>
            {
                sceneCatalog = ResourceManager.Instance.Load<SceneCatalogSO>(SceneCatalogKey);
            });
        }
        
        #region Scene

        public async UniTask LoadLastScene(string saveFile)
        {
            if (LoadFile(saveFile) is not { } data) return;
            
            await UniTask.SwitchToMainThread();
            await UniTask.Yield(); // 1프레임 지연
            
            if (sceneCatalog == null)
            {
                sceneCatalog = ResourceManager.Instance.Load<SceneCatalogSO>(SceneCatalogKey);
            }
            if (data.lastSceneEntry is not { key: { } key } || string.IsNullOrEmpty(key))
            {
                key = sceneCatalog.entries[DefaultSceneIndexInCatalog].key; // 저장된 씬이 없다면 디폴트 씬으로 이동
            }
            await GameSceneManager.Instance.LoadSceneAsync(key);
            await UniTask.Yield(); // 1프레임 지연
            
            RestoreState(data);
        }

        #endregion
        
        #region Save/Load/Delete Async (public)

        public async UniTask SaveAsync(string saveFile, SceneEntry sceneEntry = null)
        {
            await UniTask.SwitchToMainThread();
            try { Save(saveFile, sceneEntry); }
            catch (Exception e) { Logg.LogError($"[SaveSystem] SaveAsync() failed: {e.Message}"); }
            await UniTask.Yield();
        }

        public async UniTask DeleteAsync(string saveFile)
        {
            Delete(saveFile);
            await UniTask.Yield();
        }

        public async UniTask LoadAsync(string saveFile)
        {
            await UniTask.SwitchToMainThread();
            try { Load(saveFile); }
            catch (Exception e) { Logg.LogError($"[SaveSystem] LoadAsync() failed: {e.Message}"); }
            await UniTask.Yield();
        }

        #endregion

        #region Save/Load/Delete (private)

        private void Save(string saveFile, SceneEntry sceneEntry = null)
        {
            // var buildIndex = SceneManager.GetActiveScene().buildIndex;
            var sceneName = SceneManager.GetActiveScene().name;

            SaveFileData data = LoadFile(saveFile);

            data.lastSceneEntry = sceneEntry;
            // data.lastSceneBuildIndex = buildIndex;
            
            List<SavableEntry> sceneEntries = new List<SavableEntry>();
            List<SavableEntry> globalEntries = new List<SavableEntry>();
            CaptureState(sceneEntries, globalEntries);
            
            // data.sceneData[buildIndex] = sceneEntries;
            data.sceneData[sceneName] = sceneEntries;
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
            // int buildIndex = SceneManager.GetActiveScene().buildIndex;
            var sceneName = SceneManager.GetActiveScene().name;
            var sceneEntries = data.sceneData;
            List<SavableEntry> entries = new();
            
            // if (sceneEntries.TryGetValue(buildIndex, out var targetSceneEntries))
            if (sceneEntries.TryGetValue(sceneName, out var targetSceneEntries))
            {
                entries.AddRange(targetSceneEntries);
            }
            else
            {
                // Util.Log($"[SaveSystem] No saved data for scene '{buildIndex}'");
                Logg.Log($"[SaveSystem] No saved data for scene '{sceneName}'", Logg.LoggingMode.InProgress);
            }
            
            if (data.globalData is { Count: > 0 } globEntries)
            {
                entries.AddRange(globEntries);
            }
            else
            {
                Logg.Log("[SaveSystem] No saved global data");
            }

            var grouped = new Dictionary<string, Dictionary<string, object>>(entries.Count);

            foreach (var entry in entries)
            {
                var type = GetTypeByName(entry.typeName);
                if (type == null)
                {
                    Logg.LogError($"[{nameof(SaveSystem)}.{nameof(RestoreState)}()] Type not found: {entry.typeName}");
                    continue;
                }

                object state;
                try
                {
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

                if (!grouped.TryGetValue(entry.id, out var dict))
                {
                    dict = new Dictionary<string, object>();
                    grouped[entry.id] = dict;
                }

                dict[entry.typeName] = state;
            }

            foreach (var entity in UnityEngine.Object.FindObjectsByType<SavableEntity>(UnityEngine.FindObjectsSortMode.None))
            {
                string id = entity.UniqueIdentifier;
                if (grouped.TryGetValue(id, out var stateDict))
                {
                    entity.RestoreState(stateDict);
                }
            }

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
        
        #region File (LoadFile, SaveFile)

        private SaveFileData LoadFile(string saveFile)
        {
            string path = GetPathFromSaveFile(saveFile);
            if (!File.Exists(path)) return new SaveFileData();

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerialization.FromJson<SaveFileData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to load file {path}: {e.Message}");
                return new SaveFileData();
            }
        }
        
        private void SaveFile(string saveFile, SaveFileData data)
        {
            string path = GetPathFromSaveFile(saveFile);
            var tmp = path + ".tmp"; // 임시 파일명
            var bak = path + ".bak"; // 백업 파일명
            
            string json = JsonSerialization.ToJson(data, new JsonSerializationParameters
            {
                DisableSerializedReferences = true
            });

            try
            {
                File.WriteAllText(tmp, json);
            }
            catch (Exception e)
            {
                Logg.LogError($"[{nameof(SaveSystem)}.{nameof(SaveFile)}()] Failed to write new save file. {tmp}: {e.Message}");
                return; // 세이브 파일 생성 실패 시 중지
            }

            
            if (File.Exists(path)) // 기존 세이브가 존재하는 경우
                File.Replace(tmp, path, bak);
            else // 신규 세이브
                File.Move(tmp, path);
            
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
