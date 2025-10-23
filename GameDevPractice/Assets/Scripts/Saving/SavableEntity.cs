using System;
using System.Collections;
using System.Collections.Generic;
using TH.SaveLoad;
using UnityEngine;
using UnityEditor;

namespace RPG.Saving
{
    [ExecuteAlways]
    public class SavableEntity : MonoBehaviour, ISavableEntity
    {
        [SerializeField] private string uniqueIdentifier = "";
        [SerializeField] private bool isGlobal = false;
        public bool IsGlobal => isGlobal;
        
        static Dictionary<string, SavableEntity> globalLookup = new Dictionary<string, SavableEntity>();
        static Dictionary<string, string> savedTypeLookup = new Dictionary<string, string>(); // (ISavable 구현 클래스 이름, 세이브 데이터 저장 객체 이름) -> RestoreState()에서 사용 목적
        private bool hasCaptured = false;

        private static readonly string UniqueIdentifierPropertyName = "uniqueIdentifier";
        
        public string UniqueIdentifier => uniqueIdentifier;
        
        
        public Dictionary<string, object> CaptureState()
        {
            var state = new Dictionary<string, object>();

            foreach (var savable in GetComponents<ISavable>())
            {
                if (savable == null) continue; // ISavable 구현 컴포넌트가 null이면 취소
                
                var objState = savable.CaptureState();
                if (objState == null) continue; // ISavable 구현 컴포넌트로부터 세이브 데이터 생성에 실패하면 취소
                
                var savedTypeName = objState.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(savedTypeName)) continue; // 저장 데이터 타입 이름 검출에 실패하면 취소
                
                state[savedTypeName] = objState; // 현재 상태 등록
                TryCacheSavedTypeName(savedTypeName, savable);
            }

            if (!hasCaptured) hasCaptured = true; // flag 갱신
            
            return state;
        }
        
        public void RestoreState(Dictionary<string, object> state)
        {
            foreach (var savable in GetComponents<ISavable>())
            {
                if (savable == null) continue;
                
                var typeName = savable.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(typeName)) continue;
                if (savedTypeLookup.TryGetValue(typeName, out var savedTypeName))
                {
                    if (state.TryGetValue(savedTypeName, out var saved))
                    {
                        try
                        {
                            savable.RestoreState(saved);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SavableEntity] RestoreState() failed for {typeName} with known mapping {e}");
                        }
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
                                savedTypeLookup[typeName] = key; // 적합한 타입인 경우 타입 저장
                                break;
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"[SavableEntity] Failed to restore state. Type: {kvp.Key}, Exception: {e}");
                        }
                    }
                }
            }
        }
        
#if UNITY_EDITOR
        private void Update() {
            if (Application.IsPlaying(gameObject)) return; // 에디터 모드가 아닌 경우 return
            if (string.IsNullOrEmpty(gameObject.scene.path)) return; // 프리팹 내부의 GO인 경우 return

            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty property = serializedObject.FindProperty(UniqueIdentifierPropertyName);
            
            if (string.IsNullOrEmpty(property.stringValue) || !IsUnique(property.stringValue)) // 고유식별자가 비어있거나, 유일한 고유식별자가 아닌 경우
            {
                property.stringValue = System.Guid.NewGuid().ToString(); // 고유식별자 생성
                serializedObject.ApplyModifiedProperties(); // 고유식별자 적용
            }

            globalLookup[property.stringValue] = this; // 글로벌 룩업 딕셔너리에 자기자신을 등록
        }
#endif
        
        private bool IsUnique(string candidate)
        {
            if (!globalLookup.ContainsKey(candidate)) return true;

            if (globalLookup[candidate] == this) return true;

            if (globalLookup[candidate] == null)
            {
                globalLookup.Remove(candidate);
                return true;
            }

            if (globalLookup[candidate].UniqueIdentifier != candidate)
            {
                globalLookup.Remove(candidate);
                return true;
            }

            return false;
        }

        private void TryCacheSavedTypeName(string savedTypeName, ISavable instance)
        {
            // if (hasCaptured) return; // 한번 TryCacheSavedTypeName()이 호출된 적이 있다면 취소

            var typeName = instance.GetType().AssemblyQualifiedName;
            if (string.IsNullOrEmpty(typeName)) return; // 리플렉션 타입 이름 생성에 실패했다면 취소
            savedTypeLookup.TryAdd(typeName, savedTypeName); // 중복 등록x
        }
        
    }
}

