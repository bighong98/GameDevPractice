using System;
// using GameDevTV.Utils;
using TH.SaveLoad;
using TH.Stats;
using TH.Core.Pool;
using UnityEngine;
using Cysharp.Threading.Tasks;
using TH.Resource;
using TH.Attribute.Stat;
using TH.Utils;
using TH.Core.Service;
using UnityEngine.Scripting;

namespace TH.Attribute
{
    // 플레이어 경험치/레벨 상태 관리 및 저장 복원 담당 컴포넌트
    public class PlayerExperience : MonoBehaviour, IExperience, ILevel, ISavable, ITypeDependent
    {
        #region Events (IExperience, ILevel)
        // 레벨 변경 알림 이벤트
        public event Action<int> OnLevelChanged;

        // 경험치 증가량 알림 이벤트
        public event Action<float> OnXpGained;
        // 현재 경험치 변경 알림 이벤트
        public event Action<float> OnXpChanged;
        // 현재 레벨 기준 시작 경험치 변경 알림 이벤트
        public event Action<float> OnXpBaselineChanged;
        // 다음 레벨 도달 목표 경험치 변경 알림 이벤트
        public event Action<float> OnXpToLevelUpChanged;
        #endregion

        #region Properties (IExperience, ILevel)
        // 현재 레벨 조회 프로퍼티
        public int GetCurrLevel => currentLevel;
        // 현재 경험치 조회 프로퍼티
        public float GetCurrXp => currentXp;
        // 현재 레벨업 목표 경험치 조회 프로퍼티
        public float GetCurrXpToLevelUp => currXpToLevelUp;
        // 현재 레벨 기준선 경험치 조회 프로퍼티
        public float GetCurrBaselineXp => currBaselineXp;
        #endregion
        
        #region Fields
        // 현재 레벨 상태값
        private int currentLevel = 1;
        // 현재 누적 경험치 상태값
        private float currentXp = 0;
        // 다음 레벨 목표 경험치 상태값
        private float currXpToLevelUp = 1;
        // 현재 레벨 기준선 경험치 상태값
        private float currBaselineXp = 0;
        
        // 진행 데이터 부재 시 시작 레벨 기본값
        private int startingLevel = 1;
        #endregion

        // 레벨업 이펙트 실행 델리게이트
        private Action LevelUpEffectAction;
        // 레벨/경험치 테이블 SO 참조
        private ProgressionSO progression;

        // 경험치 획득 플로팅 텍스트 스포너 참조
        private IFloatingTextSpawner textSpawner;
        
        // 선초기화와 리소스 로드 후 초기화 분리 진입점
        private void Awake()
        {
            InitBeforeLoad();
            ResourceManager.Instance.WaitForPreLoadOnlyOnce(InitAfterLoad);
        }

        // 등록된 플로팅 텍스트 핸들러 정리 단계
        void OnDestroy()
        {
            textSpawner?.UnRegister(this, FloatingTextEventType.GetXp);
        }

        #region Initialization
        // 로드 전 서비스 참조 확보 단계
        private void InitBeforeLoad()
        {
            textSpawner = ServiceLocator.Get<IFloatingTextSpawner>();
        }
        
        // 로드 후 진행 테이블 및 이벤트 등록 단계
        private void InitAfterLoad()
        {
            progression = ResourceManager.Instance.Load<ProgressionSO>("ProgressionSO.asset");
            textSpawner.Register(this, FloatingTextEventType.GetXp);

            LevelUpTestMethod().Forget();
        }

        #endregion
        
        #region ITypeDependent
        // 타입 정보 수신 기반 시작 레벨 및 이펙트 핸들러 설정 단계
        public void ReceiveType(ScriptableObject typeInfo)
        {
            // 플레이어 타입 정보 미일치 조기 종료 가드
            if (typeInfo is not PlayerTypeSO playerInfo) return;
            
            LevelUpEffectAction = () =>
            {
                // 파괴된 트랜스폼 참조 방어 가드
                if (transform == null) return;
                PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(playerInfo.levelUpEffect, transform, transform.position);
            };

            // 타입 기본 시작 레벨과 현재 레벨 불일치 보정 분기
            if (playerInfo.startingLevel is {} defaultStartLv and > 0 && this.currentLevel > defaultStartLv)
            {
                SetLevel(defaultStartLv, byForce: true);
            }
        }
        #endregion

        #region IExpereince
        // 현재 경험치 값 갱신 및 필요 시 레벨 재계산 처리
        public void SetXp(float xp, bool updateLevel = true)
        {
            if (xp.IsEqualFloat(currentXp)) return;
             
            // 증가분 알림용 델타 계산 단계
            var delta = Mathf.Clamp(xp - currentXp, min: 0f, max: currentXp);
            currentXp = xp;

            OnXpChanged?.Invoke(currentXp);
            if (delta > 0) OnXpGained?.Invoke(delta);

            // 경험치 기반 레벨 동기화 분기
            if (updateLevel)
                SetLevel(CalculateLevel(xp));
        }

        // 경험치 누적 증가 처리
        public void GainXp(float xp)
        {
            if (xp < 0) return; // 음수 입력 무시 가드
            Logg.Log($"Experience Gained ({xp})", Logg.LoggingMode.Completed);
            SetXp(currentXp + xp, true);
        }

        #endregion
        
        #region ILevel
        // 레벨 값 설정 및 연관 경험치 기준값 갱신 처리
        // byForce: 강제 지정 모드 활성화 플래그
        // notifyCallbacks: 콜백 제어 확장 예약 파라미터
        public void SetLevel(int level, bool byForce = false, bool notifyCallbacks = true) 
        {
            if (currentLevel == level) return; // 동일 레벨 설정 무시 가드
            // 강제 레벨 지정 시 해당 레벨 기준 경험치로 동기화 분기
            if (byForce && CalculateXpFromLevel(level, out float xp))
            {
                SetXp(xp);
                return;
            }

            var prevLevel = currentLevel;
            currentLevel = level;

            currentLevel = level;
            if (prevLevel < level)
            {
                // 레벨 상승 시 이펙트 및 로그 처리 분기
                LevelUpEffectAction?.Invoke();
                Logg.Log($"Level up: ({level})", Logg.LoggingMode.Completed);
            }

            OnLevelChanged?.Invoke(currentLevel);
            if (CalculateXpFromLevel(currentLevel - 1, out var newBaselineXp))
            {
                currBaselineXp = newBaselineXp;
                OnXpBaselineChanged?.Invoke(newBaselineXp);
            }   
            if (CalculateXpFromLevel(currentLevel, out var newXptoLevelUp))
            {
                currXpToLevelUp = newXptoLevelUp;
                OnXpToLevelUpChanged?.Invoke(newXptoLevelUp);
            }
                
        }

        #endregion

        // 경험치 구간 테이블 기반 레벨 계산 처리
        private int CalculateLevel(float xp)
        {
            if (progression == null) // ProgressionSO 참조가 없는 경우 startingLevel 반환
                return startingLevel;
            //todo: ExperienceToLevelUp 스탯 관리 정책 확정 후 재검토
            int penultimateLevel = progression.GetMaxLevel(GameStats.ExperienceToLevelUp, CharacterType.Player);
            
            // 첫 임계값 초과 레벨 탐색 루프
            for (int level = 1; level <= penultimateLevel; level++)
            {
                if (progression.GetProgressionStat(GameStats.ExperienceToLevelUp, CharacterType.Player, level) is
                        { } xpToLevelUp && xpToLevelUp > xp)
                {
                    return level;
                }
            }

            return penultimateLevel + 1;
        }

        // 레벨 기준 목표 경험치 조회 처리
        private bool CalculateXpFromLevel(int level, out float xp)
        {
            if (progression != null &&
                progression.GetProgressionStat(GameStats.ExperienceToLevelUp, CharacterType.Player, level) is
                    float result)
            {
                xp = result;
                return true;
            }

            xp = 0;
            return false;
        }

        #region ISavable (Save/Load)

        [Preserve]
        // 플레이어 레벨/경험치 저장 데이터 구조체
        public struct PlayerLevelXpData
        {
            // 저장 대상 레벨 값
            public int level;
            // 저장 대상 경험치 값
            public float xp;

            // 저장 데이터 생성자
            public PlayerLevelXpData(int level, float xp)
            {
                this.level = level;
                this.xp = xp;
            }
        }

        // 레벨/경험치 상태 직렬화 데이터 생성 처리
        public object CaptureState()
        {
            this.Log($"- ({gameObject.name}) CaptureState invoked", Logg.LoggingMode.Completed);

            if (GetCurrLevel is { } level && GetCurrXp is { } xp)
            {
                this.Log($"- ({gameObject.name}) CaptureState invoked - level: {level}), xp: {xp}", Logg.LoggingMode.Completed);
                return new PlayerLevelXpData(level, xp);
            }

            return null;
        }

        // 레벨/경험치 상태 복원 처리
        public bool RestoreState(object state)
        {
            this.Log($"- ({gameObject.name}) RestoreState invoked", Logg.LoggingMode.Completed);
            if (state is PlayerLevelXpData { } data)
            {
                this.Log($"- ({gameObject.name}) RestoreState invoked - SetLevel({data.level}), SetXp({data.xp})", Logg.LoggingMode.Completed);
                // 경험치 설정 기반 레벨 자동 동기화 경로 사용
                SetXp(data.xp, updateLevel: true);
                return true;
            }

            return false;
        }

        // 저장 상태 기본값 리셋 처리
        public void ResetToDefaultState()
        {
            SetXp(0f, updateLevel: true);
        }

        #endregion
        
        #region Test (Editor Only)

#if UNITY_EDITOR
        // 에디터 전용 레벨업 테스트 지연 상수
        private readonly TimeSpan oneSecond = TimeSpan.FromSeconds(1);
        // 에디터 환경 레벨업 반복 테스트 루틴
        private async UniTaskVoid LevelUpTestMethod()
        {
            Logg.Log($"[{nameof(PlayerExperience)}] '{nameof(LevelUpTestMethod)}' started", Logg.LoggingMode.Completed);
            int count = 0;
            while (count < 10)
            {
                await UniTask.Delay(oneSecond, DelayType.DeltaTime);
                if (this == null || gameObject == null) break;
                
                count++;
                GainXp(50);
            }
        } 
#endif
        #endregion

    }
}

