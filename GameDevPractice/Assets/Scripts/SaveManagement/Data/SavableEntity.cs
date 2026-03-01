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
    // 세이브 대상 게임오브젝트 단위 상태 수집 및 복원 책임 컴포넌트
    public class SavableEntity : MonoBehaviour, ISavableEntity
    {
        // 엔티티 영속 식별자 문자열
        [SerializeField] private string uniqueIdentifier = "";
        // 시작 시 레지스트리 자동 등록 활성화 플래그
        [SerializeField] private bool autoRegisterToRegistry = true;
        // 씬 종속성 없는 글로벌 엔티티 구분 플래그
        [SerializeField] private bool isGlobal = false;
        // 글로벌 저장 대상 여부 노출 프로퍼티
        public bool IsGlobal => isGlobal;
        
        // 식별자 기반 엔티티 인스턴스 조회 테이블
        private static readonly Dictionary<string, SavableEntity> GlobalLookup = new Dictionary<string, SavableEntity>();
        // 런타임 타입명과 저장 타입명 매핑 캐시 테이블
        private static readonly Dictionary<string, string> SavedTypeLookup = new Dictionary<string, string>(); // (ISavable 구현 클래스 이름, 세이브 데이터 저장 객체 이름) -> RestoreState()에서 사용 목적
        // 세이브 엔티티 등록 시스템 지연 초기화 참조
        private static ISaveEntityRegistry saveEntityRegistry;
        
        // 현재 오브젝트에 부착된 ISavable 컴포넌트 캐시 목록
        private readonly List<ISavable> savables = new();
        // 에디터 직렬화 프로퍼티 접근용 필드명 상수
        private static readonly string UniqueIdentifierPropertyName = "uniqueIdentifier";

        // 외부 조회용 엔티티 식별자 프로퍼티
        public string UniqueIdentifier => uniqueIdentifier;
        // 레지스트리 등록 상태 추적 플래그
        public bool IsRegistered {get; set;} = false;
        // 엔티티가 소속된 씬 정보 노출 프로퍼티
        public Scene TargetScene => gameObject.scene;

        // 서비스 레지스트리 확보 및 savable 목록 초기 캐싱
        private void Awake()
        {
            saveEntityRegistry ??= ServiceLocator.Get<ISaveEntityRegistry>();
            RebuildSavableList();
        }

        // 유효 식별자 확보 후 자동 등록 조건 충족 시 레지스트리 등록
        private void Start()
        {
            TryGetUniqueIdAndRegisterSelf();

            // 자동 등록 비활성 상태 조기 종료 가드
            if (!autoRegisterToRegistry) return;
            // 식별자 유효성 실패 상태 조기 종료 가드
            if (!IsValidIdentifier(uniqueIdentifier)) return;
            // 중복 등록 방지 조기 종료 가드
            if (IsRegistered) return;
            // 세이브 로드 대상으로 자가등록
            saveEntityRegistry?.RegisterEntity(this, isGlobal, destroyCancellationToken);
        }

        // 런타임 식별자 교체 및 글로벌 룩업 인덱스 동기화 처리
        public void SetRuntimeUniqueId(string id)
        {
            // 빈 식별자 입력 무시 가드
            if (string.IsNullOrEmpty(id)) return;

            // 기존 식별자 인덱스 제거 대상 확인 단계
            if (!string.IsNullOrEmpty(uniqueIdentifier)
                && GlobalLookup.TryGetValue(uniqueIdentifier, out var current)
                && current == this)
            {
                GlobalLookup.Remove(uniqueIdentifier);
            }

            // 신규 식별자 반영 및 룩업 재등록 단계
            uniqueIdentifier = id;
            GlobalLookup[uniqueIdentifier] = this;
        }

        // 자동 등록 플래그 변경 및 필요 시 즉시 등록 해제 처리
        public void SetAutoRegisterToRegistry(bool enabled, bool unregisterIfDisabled = true)
        {
            autoRegisterToRegistry = enabled;

            // 자동 등록 비활성 전환 시 레지스트리 정리 조건 분기
            if (!enabled && unregisterIfDisabled)
            {
                UnregisterFromRegistry();
            }
        }

        // 레지스트리 등록 해제 요청 진입 메서드
        public void UnregisterFromRegistry()
        {
            // 미등록 상태 조기 종료 가드
            if (!IsRegistered) return;

            saveEntityRegistry ??= ServiceLocator.Get<ISaveEntityRegistry>();
            saveEntityRegistry?.UnRegisterEntity(this, destroyCancellationToken);
        }


        // 파괴 시 캐시 목록 정리 단계
        private void OnDestroy()
        {
            savables.Clear();
        }
        
        #region ISavableEntity
        
        // 인터페이스 객체 반환 규약 대응용 명시적 구현 브리지
        object ISavable.CaptureState()
        {
            return CaptureState();
        }

        // object 입력 복원 경로 진입 및 딕셔너리 타입 검증 처리
        public bool RestoreState(object state)
        {
            this.Log($"{gameObject.name} - RestoreState", Logg.LoggingMode.Completed);
            // 복원 데이터 타입 미일치 조기 종료 가드
            if (state is not Dictionary<string, object> states) return false;
            RestoreState(states);
            return true;
        }

        // 부착된 모든 ISavable 상태 수집 및 타입명 키 기반 패킹 처리
        public Dictionary<string, object> CaptureState()
        {
            this.Log($"{gameObject.name} - CaptureState", Logg.LoggingMode.Completed);
            var state = new Dictionary<string, object>();
            
            foreach (var savable in savables)
            {
                if (savable == null) continue; // NRE 방어
                
                // 컴포넌트별 상태 오브젝트 추출 단계
                var objState = savable.CaptureState();
                if (objState == null) continue; // ISavable 구현 컴포넌트로부터 세이브 데이터 생성에 실패하면 취소
                
                // 저장 키로 사용할 직렬화 타입명 계산 단계
                var savedTypeName = objState.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(savedTypeName)) continue; // 저장 데이터 타입 이름 검출에 실패하면 취소
                
                Logg.Log($"[SavableEntity] ({savedTypeName}, {objState})", Logg.LoggingMode.Completed);
                
                state[savedTypeName] = objState; // 현재 상태 등록
                // 복원 가속용 타입명 캐시 축적 단계
                TryCacheSavedTypeName(savedTypeName, savable);
            }
            
            return state;
        }
        
        // 저장 딕셔너리 기반 컴포넌트 상태 역직렬화 처리
        public void RestoreState(Dictionary<string, object> state)
        {
            this.Log($"{gameObject.name} - RestoreState", Logg.LoggingMode.Completed);
            foreach (var savable in savables)
            {
                if (savable == null) continue;
                
                // 현재 컴포넌트 런타임 타입명 식별 단계
                var typeName = savable.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(typeName)) continue;
                
                // 기존 매핑 캐시 존재 경로 우선 복원 분기
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
                    // 저장 항목 전수 탐색 기반 최초 매핑 학습 분기
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

        // 부착된 모든 ISavable 기본 상태 리셋 일괄 실행 처리
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
        // 에디터 값 변경 시점 식별자 유효성 보정 진입점
        private void OnValidate()
        {
            if (Application.IsPlaying(gameObject)) return; // 에디터 플레이 모드인 경우 return
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject)) return; // 프리팹 에셋(프리팹 모드 포함)인 경우 return
            if (!gameObject.scene.IsValid() || string.IsNullOrEmpty(gameObject.scene.path)) return; // 씬에 배치되지 않은 GO인 경우 return

            TryGetUniqueIdAndRegisterSelf();
        }

        // 에디터 전용 식별자 생성 및 룩업 등록 처리
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

        // 에디터/런타임 분기 기반 식별자 등록 진입점
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

        // 플레이 모드 식별자 보정 및 룩업 반영 처리
        private void TryGetUniqueIdAndRegisterSelfAtRuntime()
        {
            if (!IsValidIdentifier(uniqueIdentifier))
            {
                uniqueIdentifier = System.Guid.NewGuid().ToString();
            }

            GlobalLookup[uniqueIdentifier] = this;
        }

        // 식별자 유효성 통합 검증 절차
        private bool IsValidIdentifier(string candidate)
        {
            if (string.IsNullOrEmpty(candidate)) return false;
            if (IsDefaultIdentifier(candidate)) return false;
            return IsUnique(candidate);
        }

        // 기본 식별자 접미사 규약 상수
        private const string DefaultIdentifierConvention = "_default";
        // 기본값 패턴 식별자 제외 검증 단계
        private bool IsDefaultIdentifier(string candidate)
        {
            return candidate.EndsWith(DefaultIdentifierConvention);
        }
        
        // 글로벌 룩업 내 충돌/유실 엔트리 정리 포함 고유성 검증 단계
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

        // 복원 성공 타입명 매핑 캐시 축적 처리
        private void TryCacheSavedTypeName(string savedTypeName, ISavable instance)
        {
            var typeName = instance.GetType().AssemblyQualifiedName;
            if (string.IsNullOrEmpty(typeName)) return; // 리플렉션 타입 이름 생성에 실패했다면 취소
            SavedTypeLookup.TryAdd(typeName, savedTypeName); // 중복 등록 방지
        }

        // 현재 게임오브젝트의 ISavable 목록 재구성 처리
        private void RebuildSavableList()
        {
            savables.Clear();
            GetComponents(savables);
            // 자기 자신 제외 처리
            savables.Remove(this);
        }
    }
}

