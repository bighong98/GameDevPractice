using System;
using System.Collections.Generic;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace TH.SaveLoad
{
    public class SavableEntity : MonoBehaviour, ISavableEntity
    {
        [SerializeField] private string uniqueIdentifier = "";
        [SerializeField] private bool isGlobal = false;
        public bool IsGlobal => isGlobal;
        
        private static readonly Dictionary<string, SavableEntity> GlobalLookup = new Dictionary<string, SavableEntity>();
        private static readonly Dictionary<string, string> SavedTypeLookup = new Dictionary<string, string>(); // (ISavable 구현 클래스 이름, 세이브 데이터 저장 객체 이름) -> RestoreState()에서 사용 목적
        // private static ISaveSystem saveSystem;
        private static ISaveEntityRegistry saveEntityRegistry;
        
        private readonly List<ISavable> savables = new();
        
        private static readonly string UniqueIdentifierPropertyName = "uniqueIdentifier";

        public string UniqueIdentifier => uniqueIdentifier;
        public bool IsRegistered {get; set;} = false;
        public Scene TargetScene => gameObject.scene;

        private void Awake()
        {
            RebuildSavableList();
            saveEntityRegistry = ServiceLocator.Get<ISaveEntityRegistry>();
        }

        private void Start()
        {
            saveEntityRegistry.RegisterEntity(this, isGlobal, destroyCancellationToken);
        }

        private void OnDestroy()
        {
            savables.Clear();
        }
        
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
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.IsPlaying(gameObject)) return; // 에디터 모드가 아닌 경우 return
            if (string.IsNullOrEmpty(gameObject.scene.path)) return; // 프리팹 내부의 GO인 경우 return

            TryGetUniqueIdAndRegisterSelf();
        }

        private void TryGetUniqueIdAndRegisterSelf()
        {
            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty property = serializedObject.FindProperty(UniqueIdentifierPropertyName);
            
            if (string.IsNullOrEmpty(property.stringValue) || !IsUnique(property.stringValue)) // 고유식별자가 비어있거나, 유일한 고유식별자가 아닌 경우
            {
                property.stringValue = System.Guid.NewGuid().ToString(); // 고유식별자 생성
                serializedObject.ApplyModifiedProperties(); // 고유식별자 적용
            }

            GlobalLookup[property.stringValue] = this; // 글로벌 룩업 딕셔너리에 자기자신을 등록
        }
#endif
        
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

