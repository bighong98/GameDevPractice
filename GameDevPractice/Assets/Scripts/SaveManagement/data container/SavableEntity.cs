using System;
using System.Collections.Generic;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.SceneManagement;

namespace TH.SaveLoad
{
    public class SavableEntity : MonoBehaviour, ISavableEntity
    {
        [SerializeField] private string uniqueIdentifier = "";
        [SerializeField] private bool autoRegisterToRegistry = true;
        [SerializeField] private bool isGlobal = false;
        public bool IsGlobal => isGlobal;
        
        private static readonly Dictionary<string, SavableEntity> GlobalLookup = new Dictionary<string, SavableEntity>();
        private static readonly Dictionary<string, string> SavedTypeLookup = new Dictionary<string, string>(); // (ISavable 구현 클래스 이름, 세이브 데이터 저장 객체 이름) -> RestoreState()에서 사용 목적
        private static ISaveEntityRegistry saveEntityRegistry;
        
        private readonly List<ISavable> savables = new();
        private static readonly string UniqueIdentifierPropertyName = "uniqueIdentifier";

        public string UniqueIdentifier => uniqueIdentifier;
        public bool IsRegistered {get; set;} = false;
        public Scene TargetScene => gameObject.scene;

        private void Awake()
        {
            saveEntityRegistry ??= ServiceLocator.Get<ISaveEntityRegistry>();
            RebuildSavableList();
        }

        private void Start()
        {
            TryGetUniqueIdAndRegisterSelf();

            if (!autoRegisterToRegistry) return;
            if (!IsValidIdentifier(uniqueIdentifier)) return;
            if (IsRegistered) return;

            saveEntityRegistry?.RegisterEntity(this, isGlobal, destroyCancellationToken);
        }

        public void SetRuntimeUniqueId(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (!string.IsNullOrEmpty(uniqueIdentifier)
                && GlobalLookup.TryGetValue(uniqueIdentifier, out var current)
                && current == this)
            {
                GlobalLookup.Remove(uniqueIdentifier);
            }

            uniqueIdentifier = id;
            GlobalLookup[uniqueIdentifier] = this;
        }

        public void SetAutoRegisterToRegistry(bool enabled, bool unregisterIfDisabled = true)
        {
            autoRegisterToRegistry = enabled;

            if (!enabled && unregisterIfDisabled)
            {
                UnregisterFromRegistry();
            }
        }

        public void UnregisterFromRegistry()
        {
            if (!IsRegistered) return;

            saveEntityRegistry ??= ServiceLocator.Get<ISaveEntityRegistry>();
            saveEntityRegistry?.UnRegisterEntity(this, destroyCancellationToken);
        }


        private void OnDestroy()
        {
            savables.Clear();
        }
        
        #region ISavableEntity
        
        object ISavable.CaptureState()
        {
            return CaptureState();
        }

        public bool RestoreState(object state)
        {
            this.Log($"{gameObject.name} - RestoreState", Logg.LoggingMode.Completed);
            if (state is not Dictionary<string, object> states) return false;
            RestoreState(states);
            return true;
        }

        public Dictionary<string, object> CaptureState()
        {
            this.Log($"{gameObject.name} - CaptureState", Logg.LoggingMode.Completed);
            var state = new Dictionary<string, object>();
            
            foreach (var savable in savables)
            {
                if (savable == null) continue; // NRE 방어
                
                var objState = savable.CaptureState();
                if (objState == null) continue; // ISavable 구현 컴포넌트로부터 세이브 데이터 생성에 실패하면 취소
                
                var savedTypeName = objState.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(savedTypeName)) continue; // 저장 데이터 타입 이름 검출에 실패하면 취소
                
                Logg.Log($"[SavableEntity] ({savedTypeName}, {objState})", Logg.LoggingMode.Completed);
                
                state[savedTypeName] = objState; // 현재 상태 등록
                TryCacheSavedTypeName(savedTypeName, savable);
            }
            
            return state;
        }
        
        public void RestoreState(Dictionary<string, object> state)
        {
            this.Log($"{gameObject.name} - RestoreState", Logg.LoggingMode.Completed);
            foreach (var savable in savables)
            {
                if (savable == null) continue;
                
                var typeName = savable.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(typeName)) continue;
                
                if (SavedTypeLookup.TryGetValue(typeName, out var savedTypeName))
                {
                    if (!state.TryGetValue(savedTypeName, out var saved)) continue;
                    
                    try { savable.RestoreState(saved); }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SavableEntity] RestoreState() failed for {typeName} " +
                                       $"with known mapping {e}");
                    }
                }
                else // 캐싱된 savedType이 없는 경우
                {
                    foreach (var kvp in state)
                    {
                        var value = kvp.Value;
                        var key = kvp.Key;
                        if (value == null) continue;

                        try
                        {
                            if (savable.RestoreState(value)) // RestoreState 성공 여부 확인
                            {
                                SavedTypeLookup[typeName] = key; // 적합한 타입인 경우 타입 저장
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SavableEntity] Failed to restore state. " +
                                           $"Type: {kvp.Key}, Exception: {e}");
                        }
                    }
                }
            }
        }

        public void ResetToDefaultState()
        {
            this.Log($"{gameObject.name} - ResetToDefaultState", Logg.LoggingMode.Completed);

            foreach (var savable in savables)
            {
                if (savable == null) continue;

                try
                {
                    savable.ResetToDefaultState();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SavableEntity] ResetToDefaultState() failed for {savable.GetType().AssemblyQualifiedName}: {e}");
                }
            }
        }
        #endregion
        
        #region Unique Identifier
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.IsPlaying(gameObject)) return; // 에디터 플레이 모드인 경우 return
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject)) return; // 프리팹 에셋(프리팹 모드 포함)인 경우 return
            if (!gameObject.scene.IsValid() || string.IsNullOrEmpty(gameObject.scene.path)) return; // 씬에 배치되지 않은 GO인 경우 return

            TryGetUniqueIdAndRegisterSelf();
        }

        private void TryGetUniqueIdAndRegisterSelfInEditor()
        {
            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty property = serializedObject.FindProperty(UniqueIdentifierPropertyName);

            if (!IsValidIdentifier(property.stringValue))
            {
                property.stringValue = System.Guid.NewGuid().ToString(); // 고유 식별자 생성
                serializedObject.ApplyModifiedProperties(); // 고유 식별자 적용
            }

            GlobalLookup[property.stringValue] = this; // 글로벌 룩업 딕셔너리에 자기 자신을 등록
        }
#endif

        private void TryGetUniqueIdAndRegisterSelf()
        {
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                TryGetUniqueIdAndRegisterSelfInEditor();
                return;
            }
#endif
            TryGetUniqueIdAndRegisterSelfAtRuntime();
        }

        private void TryGetUniqueIdAndRegisterSelfAtRuntime()
        {
            if (!IsValidIdentifier(uniqueIdentifier))
            {
                uniqueIdentifier = System.Guid.NewGuid().ToString();
            }

            GlobalLookup[uniqueIdentifier] = this;
        }

        private bool IsValidIdentifier(string candidate)
        {
            if (string.IsNullOrEmpty(candidate)) return false;
            if (IsDefaultIdentifier(candidate)) return false;
            return IsUnique(candidate);
        }

        private const string DefaultIdentifierConvention = "_default";
        private bool IsDefaultIdentifier(string candidate)
        {
            return candidate.EndsWith(DefaultIdentifierConvention);
        }
        
        private bool IsUnique(string candidate)
        {
            if (!GlobalLookup.ContainsKey(candidate)) return true;

            if (GlobalLookup[candidate] == this) return true;

            if (GlobalLookup[candidate] == null)
            {
                GlobalLookup.Remove(candidate);
                return true;
            }

            if (GlobalLookup[candidate].UniqueIdentifier != candidate)
            {
                GlobalLookup.Remove(candidate);
                return true;
            }

            return false;
        }

        #endregion

        private void TryCacheSavedTypeName(string savedTypeName, ISavable instance)
        {
            var typeName = instance.GetType().AssemblyQualifiedName;
            if (string.IsNullOrEmpty(typeName)) return; // 리플렉션 타입 이름 생성에 실패했다면 취소
            SavedTypeLookup.TryAdd(typeName, savedTypeName); // 중복 등록x
        }

        private void RebuildSavableList()
        {
            savables.Clear();
            GetComponents(savables);
            savables.Remove(this);
        }
    }
}

