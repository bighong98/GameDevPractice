using System;
using UnityEngine;
using TH.SaveLoad;
using TH.Utils;
using TH.UI;
using TH.Attribute.Stat;
using TH.Combat;
using TH.Core.Service;
using TH.Resource;
using TH.Combat.Service;

namespace TH.Attribute
{
    [Serializable]
    public struct HealthSaveData
    {
        public bool isDead;
        public float ratio;
    }
    public class Health : MonoBehaviour, IDamageable, IHealable, ISavable, ITypeDependent //todo: ITypeDependant 구현 후 HPBar 처리
    {
        [SerializeField] private GameStatSO hpStatSO;

        // fields serailized for debug
        [SerializeField] private float maxHp = 1;
        [SerializeField] private float hp = 1;

        private IStatHolder statHolder;
        private IGameStat maxHpStat;
        private GameObject characterHpBarPrefab;
        
        // 외부 서비스
        private IFloatingTextSpawner textSpawner;
        private IKillEventHandler killEventHandler;

        private Collider collider;

        public event Action OnDead;
        public event Action OnRevived;
        public event HitEvent OnDamaged; // 피해를 입은 경우
        public event Action<float> OnHealed; // 회복 받은 경우
        
        public Action<float> OnHealthRatioChanged; // 현재 체력에 변동이 생긴 경우 (피격, 회복 등)
        public Action<float> OnMaxHealthChanged; // 최대 체력에 변동이 생긴 경우 (레벨 업, 장비 변경 등)
        public Action<float> OnCurrHealthChanged; // 현재 체력에 변동이 생긴 경우 (피격, 회복 등)
        
        public float Hp => hp;
        public float MaxHp => maxHp;
        public float HpRatio => hp / maxHp;
        public bool IsDead { get; private set; }

        private IAttacker lastAttacker = null; // 가장 최근 자신에게 피해를 입힌 대상

        private void Awake()
        {
            TryGetComponent(out statHolder);
            TryGetComponent(out collider);

            textSpawner = ServiceLocator.Get<IFloatingTextSpawner>();
            killEventHandler = ServiceLocator.Get<IKillEventHandler>();

            if (hpStatSO.IsNull() || hpStatSO.LegacyId == default)
            {
                Logg.LogError($"[{gameObject.name}.Health] invalid hpStatSO", context: this);
                return;
            }

            this.Log($"{gameObject.name} - Awake() in scene({gameObject.scene.name}) done", Logg.LoggingMode.Completed);
        }

        private void OnEnable()
        {
            SyncHpStat();

            textSpawner?.Register(this, FloatingTextEventType.Damage);
            textSpawner?.Register(this, FloatingTextEventType.Heal);

            EnsureHPBar();
        }

        private void OnDisable()
        {
            UnSyncHpStat();

            textSpawner?.UnRegister(this, FloatingTextEventType.Damage);
            textSpawner?.UnRegister(this, FloatingTextEventType.Heal);

            ReleaseHPBar();
        }

        private void EnsureHPBar(GameObject hpBarPrefab)
        {
            if (hpBarPrefab == null) return;
            if (this.characterHpBarPrefab != hpBarPrefab)
                this.characterHpBarPrefab = hpBarPrefab;
            EnsureHPBar();
        }

        private void EnsureHPBar()
        {
            if (characterHpBarPrefab == null) return;
            HPBar.Acquire(this, characterHpBarPrefab);
        }

        private void ReleaseHPBar()
        {
            HPBar.Release(this);
        }

        // 최대 체력 + 현재 체력 조정 (현재 미사용)
        // 강제로 현재 체력을 조정하기 때문에 사망한 캐릭터에 사용 시 부활하므로 주의
        private void SetHp(float amount, bool modifyMax = false)
        {
            if (modifyMax) SetMaxHp(amount, byForce: true);
            else SetCurrentHp(amount, byForce: true);
        }
 
        // 현재 체력 조정
        // byForce: true => 사망 상태 무시하고 체력 변경 및 사망 상태 갱신
        private void SetCurrentHp(float amount, bool byForce = false) 
        {
            if (IsDead && !byForce) return; // 사망 상태인 경우 체력 조정x
            
            Logg.Log($"[{gameObject.name}.{GetType()}] SetCurrentHp ({amount})", Logg.LoggingMode.Completed);
            hp = Mathf.Round(Mathf.Clamp(amount, 0, maxHp)); // 소숫점 버림

            OnHealthRatioChanged?.Invoke(hp / maxHp);
            OnCurrHealthChanged?.Invoke(hp);
            RefreshAliveState();
        }

        private void SetMaxHp(float amount, bool byForce = false)
        {
            if (!byForce && (amount < 0 || amount.IsEqualFloat(0f))) return; // 최대체력 0 이하로 설정 불가능
            
            // 기존 체력 비율 계산 (최초 초기화라면 100%, 아니면 현재 비율 유지)
            float targetRatio = maxHp.IsEqualFloat(0f) ? 1f : HpRatio;
            Logg.Log($"[{gameObject.name}.{GetType()}] SetMaxHp ({amount}), targetRatio: {targetRatio:F2}", Logg.LoggingMode.Completed);
            // 최대체력 변경 및 이벤트 호출
            maxHp = amount;
            OnMaxHealthChanged?.Invoke(maxHp);
            
            // 체력 비율에 맞게 현재 체력 조정
            SetCurrentHp(maxHp * targetRatio, byForce: true);
        }

        #region IDamageable

        private void TakeDamage(float damage)
        {
            SetCurrentHp(hp - damage); 
            Logg.Log($"[{gameObject.name}] Health.TakeDamage(damage: {damage}) - hp is set to: {hp}", Logg.LoggingMode.Completed);
        }
        
        public void TakeDamage(in HitResult hitResult)
        {
            OnDamaged?.Invoke(hitResult);
            TakeDamage(hitResult.Damage);
            lastAttacker = hitResult.Attacker;
        }

        #endregion
        
        private void RefreshAliveState()
        {
            if (!IsDead && (hp.IsEqualFloat(0f) || maxHp.IsEqualFloat(0f)))
            {
                Die();
                return;
            }

            if (IsDead && hp > 0 && maxHp > 0f)
                Revive();
        }

        private void SetColliderEnabled(bool enabled)
        {
            if (collider == null || collider.enabled == enabled) return;
            collider.enabled = enabled;
        }


        private void Die()
        {
            if (IsDead) return;

            IsDead = true;
            SetColliderEnabled(false);
            OnDead?.Invoke();

            if (lastAttacker.IsNotNull())
                killEventHandler?.HandleKillEvent(this, lastAttacker);
            lastAttacker = null;
        }

        private void Revive()
        {
            if (!IsDead) return;

            IsDead = false;
            SetColliderEnabled(true);
            OnRevived?.Invoke();
        }

        private void SyncHpStat()
        {
            if (statHolder.IsNull()) return;
            if (statHolder.BindEvent(hpStatSO, OnHealthStatDirty) is {} bindResult)
            {
                maxHpStat = bindResult;
                OnHealthStatDirty();
            } 
        }

        private void UnSyncHpStat()
        {
            if (statHolder.IsNull()) return;

            statHolder.UnBindEvent(hpStatSO, OnHealthStatDirty);
            maxHpStat = null;
        }

        private void OnHealthStatDirty()
        {
            if (statHolder == null) return;
            if (maxHpStat == null && !statHolder.TryGetStat(hpStatSO, out maxHpStat))
            {
                this.LogWarning($"OnHealthStatDirty() - failed to get maxHp stat from statholder. hpStatSO: {hpStatSO}", context: this);
                return;
            }

            SetMaxHp(maxHpStat.Value);
        }




        #region ISavable
        
        public object CaptureState()
        {
            this.Log($"[{gameObject.name}]CaptureState - hp: {hp}" ,Logg.LoggingMode.Completed); 
            
            return new HealthSaveData
            {
                isDead = IsDead,
                ratio = (maxHp.IsEqualFloat(0f) || hp.IsEqualFloat(0f)) ? 0 : HpRatio
            };
        }

        public bool RestoreState(object state)
        {
            if (state is not HealthSaveData data) return false;
            if (data.ratio is not (float storedHpRatio and >= 0)) return false;

            if (data.isDead) Die();
            SetCurrentHp(maxHp * storedHpRatio);

            this.Log($"[{gameObject.name}] - Health.RestoreState: ratio: {storedHpRatio} -> hp is set to {hp})", Logg.LoggingMode.Completed); 
            return true;
        }

        #endregion

        #region IHealable

        public bool Heal(int amount, bool byForce = false)
        {
            SetCurrentHp(hp + amount, byForce);
            OnHealed?.Invoke(amount);
            return true;
        }

        public bool HealRatio(float ratio, bool byForce = false)
        {
            if (maxHp.IsEqualFloat(0f)) return false;
            return Heal((int)(maxHp * ratio), byForce);
        }

        #endregion

        #region ITypeDependant 

        public void ReceiveType(ScriptableObject typeInfo)
        {
            if (typeInfo.IsNull() || 
                typeInfo is not CharacterTypeSO charSO )
                return;

            EnsureHPBar(charSO.HpBarPrefab);
        }

        #endregion
    }
}
