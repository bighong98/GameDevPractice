using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Utils;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "WeaponTypeSo", menuName = "Scriptable Objects/Type/Item/WeaponTypeSO")]
    public class WeaponTypeSO : EquipmentTypeSO
    {
        [Header("Weapon")] 
        public Enums.WeaponType weaponType;
        public AnimatorOverrideController weaponAnimatorOverride;
        public GameObject EquippedPrefab;
    
        [SerializeField] private float damage;
        [SerializeField] private float range;
        [SerializeField] private Hand hand;

        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject impactParticlePrefab;
        [SerializeField] private AssetReferenceAudioClip attackSFXReference;
    
        public float GetDamage => damage;
        public float GetRange => range;
        public Hand GetGripHand => hand;
    
        public bool HasProjectile { get; protected set; }
        public GameObject GetProjectilePrefab => projectilePrefab;

        public bool HasImpactEffect { get; protected set; }
        public GameObject GetImpactEffect => impactParticlePrefab;

        public bool HasAttackSFX {get; protected set;}
        public AudioClip AttackSFX { get; private set; }

        public enum Hand
        {
            Right,
            Left,
            Both,
        }

        // BaseTypeSO.OnValidate() 타이밍에 자동 호출됨
        public override void RefreshStates()
        {
            base.RefreshStates();
            HasProjectile = projectilePrefab.IsAlive();
            HasImpactEffect = impactParticlePrefab.IsAlive();
            HasAttackSFX = IsAssetRefAssigned(attackSFXReference);
            Logg.Log($"[{GetType().Name}, {nameString}] RefreshStates() - hasProjectile: {HasProjectile}, hasImpactEffect: {HasImpactEffect}, HasAttackSFX: {HasAttackSFX}", Logg.LoggingMode.Completed);
        }

        public async override UniTask InitializeAsync(CancellationToken token = default)
        {
            await base.InitializeAsync(token);
            AttackSFX = await GetStateFromAssetReference(attackSFXReference, token);
        }
    }
}

