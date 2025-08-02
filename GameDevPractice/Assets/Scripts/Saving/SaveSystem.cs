using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Serialization.Json;

namespace RPG.Saving
{
    public class SaveSystem : MonoBehaviour
    {
        private static readonly Dictionary<Type, MethodInfo> CachedMethodInfos = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<string, Type> CachedTypes = new Dictionary<string, Type>();

        public async UniTask LoadLastScene(string saveFile)
        {
            SaveFileData data = LoadFile(saveFile);
            if (data == null) return;
            
            await UniTask.SwitchToMainThread();
            await UniTask.Yield(); // 1프레임 지연
            
            int buildIndex = data.lastSceneBuildIndex;
            // await SceneManager.LoadSceneAsync(buildIndex);
            await GameSceneManager.Instance.LoadSceneAsync(buildIndex);
            await UniTask.Yield(); // 1프레임 지연
            
            RestoreState(data);
        }

        public async UniTask SaveAsync(string saveFile)
        {
            await UniTask.SwitchToMainThread();
            try
            {
                Save(saveFile);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] SaveAsync() failed: {e.Message}");
            }
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
            try
            {
                Load(saveFile);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] LoadAsync() failed: {e.Message}");
            }
            await UniTask.Yield();
        }
        
        private void Save(string saveFile)
        {
            var buildIndex = SceneManager.GetActiveScene().buildIndex;

            SaveFileData data = LoadFile(saveFile);
            data.lastSceneBuildIndex = buildIndex;
            
            List<SavableEntry> sceneEntries = new List<SavableEntry>();
            List<SavableEntry> globalEntries = new List<SavableEntry>();
            CaptureState(sceneEntries, globalEntries);
            
            data.sceneEntries[buildIndex] = sceneEntries;
            data.globalEntries = globalEntries;

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

        #region State

        // 씬에 존재하는 모든 SavableEntity의 상태 수집, 저장데이터에 반영
        private void CaptureState(List<SavableEntry> sceneEntries, List<SavableEntry> globalEntries)
        {
            foreach (var entity in FindObjectsByType<SavableEntity>(UnityEngine.FindObjectsSortMode.None))
            {
                var targetEntryList = entity.IsGlobal ? globalEntries : sceneEntries;
                var stateDict = entity.CaptureState();

                foreach (var (typeName, stateObj) in stateDict)
                {
                    var type = GetTypeByName(typeName);
                    if (type == null) continue;

                    try
                    {
                        string json = JsonSerialization.ToJson(stateObj, new JsonSerializationParameters
                        {
                            SerializedType = type
                        });

                        targetEntryList.Add(new SavableEntry
                        {
                            id = entity.GetUniqueIdentifier(),
                            typeName = typeName,
                            jsonPayload = json
                        });
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SaveSystem] Failed to serialize {typeName}: {e.Message}");
                    }
                }
            }
        }
        
        // SavableEntity 에 상태 복원
        private void RestoreState(SaveFileData data)
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            var sceneEntries = data.sceneEntries;
            List<SavableEntry> entries = new();
            
            if (sceneEntries.TryGetValue(buildIndex, out var targetSceneEntries))
            {
                entries.AddRange(targetSceneEntries);
            }
            else
            {
                Debug.Log($"[SaveSystem] No saved data for scene {buildIndex}");
            }
            
            if (data.globalEntries is { Count: > 0 } globEntries)
            {
                entries.AddRange(globEntries);
            }
            else
            {
                Debug.Log("[SaveSystem] No saved global data");
            }

            var grouped = new Dictionary<string, Dictionary<string, object>>();

            foreach (var entry in entries)
            {
                var type = GetTypeByName(entry.typeName);
                if (type == null) continue;

                object state;
                try
                {
                    state = GetMethodByType(type)?.Invoke(null, new object[]
                    {
                        entry.jsonPayload,
                        new JsonSerializationParameters { SerializedType = type }
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] Restore failed for {entry.typeName}: {e}");
                    continue;
                }

                if (!grouped.TryGetValue(entry.id, out var dict))
                {
                    dict = new Dictionary<string, object>();
                    grouped[entry.id] = dict;
                }

                dict[entry.typeName] = state;
            }

            foreach (var entity in FindObjectsByType<SavableEntity>(UnityEngine.FindObjectsSortMode.None))
            {
                string id = entity.GetUniqueIdentifier();
                if (grouped.TryGetValue(id, out var stateDict))
                {
                    entity.RestoreState(stateDict);
                }
            }
        }

        #endregion
        
        #region File

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
            string json = JsonSerialization.ToJson(data, new JsonSerializationParameters
            {
                DisableSerializedReferences = true
            });

            File.WriteAllText(path, json);
        }
        
        // 경로 생성 (임시)
        private string GetPathFromSaveFile(string saveFile)
        {
            return Path.Combine(Application.persistentDataPath, saveFile + ".sav");
        }
        
        #endregion
        
        #region Helper Function
        
        // 리플렉션 기반 Type to MethodInfo
        private MethodInfo GetMethodByType(Type type)
        {
            if (CachedMethodInfos.TryGetValue(type, out var result))
            {
                return result;
            }

            var methods = typeof(JsonSerialization).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var m in methods)
            {
                if (m.Name != "FromJson") continue;
                if (!m.IsGenericMethodDefinition) continue;

                var parameters = m.GetParameters();
                if (parameters.Length != 2 ||
                    parameters[0].ParameterType != typeof(string) ||
                    parameters[1].ParameterType != typeof(JsonSerializationParameters))
                {
                    continue;
                }

                var method = m.MakeGenericMethod(type);
                CachedMethodInfos[type] = method;
                return method;
            }

            return null;
        }

        // 리플렉션 기반 Name to Type
        private Type GetTypeByName(string typeName)
        {
            if (CachedTypes.TryGetValue(typeName, out var t)) return t;

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
        
        #region Deprecated
        
        // public void Save(string saveFile)
        // {
        //     SaveFileData data = new SaveFileData
        //     {
        //         lastSceneBuildIndex = SceneManager.GetActiveScene().buildIndex
        //     };
        //     
        //     CaptureState(data);
        //
        //     SaveFile(saveFile, data);
        // }
        
        // public void Load(string saveFile)
        // {
        //     var data = LoadFile(saveFile);
        //     if (data != null)
        //     {
        //         RestoreState(data.entries);
        //     }
        // }
        
                // private void CaptureState(SaveFileData data)
        // {
        //     foreach (var entity in FindObjectsOfType<SavableEntity>())
        //     {
        //         var stateDict = entity.CaptureState(); // Dictionary<string, object>
        //         
        //         foreach (var (typeName, stateObj) in stateDict)
        //         {
        //             var type = GetTypeByName(typeName);
        //             if (type == null) continue;
        //
        //             try
        //             {
        //                 string json = JsonSerialization.ToJson(stateObj, new JsonSerializationParameters
        //                 {
        //                     SerializedType = type
        //                 });
        //
        //                 data.entries.Add(new SavableEntry
        //                 {
        //                     id = entity.GetUniqueIdentifier(),
        //                     typeName = typeName,
        //                     jsonPayload = json
        //                 });
        //             }
        //             catch (Exception e)
        //             {
        //                 Debug.LogError($"[SaveSystem] Failed to serialize {typeName}: {e.Message}");
        //             }
        //         }
        //     }
        // }
        
        // private void RestoreState(List<SavableEntry> entries)
        // {
        //     var grouped = new Dictionary<string, Dictionary<string, object>>();
        //
        //     foreach (var entry in entries)
        //     {
        //         Debug.Log($"[Savable Entry info]\n" + 
        //                   $"id: {entry.id}, \n" +
        //                   $"type: {entry.typeName}, \n" +
        //                   $"jsonPayload: {entry.jsonPayload}");
        //         
        //         var type = GetTypeByName(entry.typeName);
        //         if (type == null)
        //         {
        //             Debug.Log($"[SaveSystem] 타입을 찾을 수 없음: {entry.typeName}");
        //             continue;
        //         }
        //         
        //         object state;
        //         try
        //         {
        //             state = GetMethodByType(type)?.Invoke(null, new object[]
        //             {
        //                 entry.jsonPayload,
        //                 new JsonSerializationParameters
        //                 {
        //                     SerializedType = type
        //                 }
        //             });
        //             if (state == null)
        //             {
        //                 Debug.Log(
        //                     $"[SaveSystem]: failed to get method. type: {type.FullName}, jsonPayload: {entry.jsonPayload}");
        //                 continue;
        //             }
        //         }
        //         catch (TargetInvocationException tie)
        //         {
        //             var inner = tie.InnerException;
        //             Debug.LogError($"type: {type.Name}, message: {inner?.Message}, resultType: {inner?.GetType().Name}\n" + 
        //                            $"StackTrace: {inner?.StackTrace}");
        //             continue;
        //         }
        //         catch (Exception e)
        //         {
        //             Debug.LogError($"[SaveSystem]: Exception Occured while RestoreState(): {type.Name}, {e.Message}");
        //             continue;
        //         }
        //
        //         if (!grouped.TryGetValue(entry.id, out var dict))
        //         {
        //             dict = new Dictionary<string, object>();
        //             grouped[entry.id] = dict;
        //         }
        //
        //         dict[entry.typeName] = state;
        //     }
        //
        //     foreach (var entity in FindObjectsOfType<SavableEntity>())
        //     {
        //         string id = entity.GetUniqueIdentifier();
        //         if (grouped.TryGetValue(id, out var stateDict))
        //         {
        //             entity.RestoreState(stateDict);
        //         }
        //     }
        // }
        
        // private MethodInfo GetMethodByType(Type type)
        // {
        //     if (CachedMethodInfos.TryGetValue(type, out var result))
        //     {
        //         return result; // 캐싱된 MethodInfo가 있으면 리턴
        //     }
        //
        //     var methods = typeof(JsonSerialization).GetMethods(BindingFlags.Public | BindingFlags.Static);
        //     foreach (var m in methods)
        //     {
        //         if (m.Name != "FromJson") continue;
        //         if (!m.IsGenericMethodDefinition) continue;
        //
        //         var method = m.MakeGenericMethod(type);
        //         CachedMethodInfos[type] = method;
        //         return method;
        //     }
        //     return null;
        // }
        
        #endregion
        
    }
}

