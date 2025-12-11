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

        [SerializeField] private bool hasProjectile;
        [SerializeField] private GameObject projectilePrefab;

        [SerializeField] private bool hasImpactEffect;
        [SerializeField] private GameObject impactParticlePrefab;

        [SerializeField] private AssetReferenceAudioClip attackSFXReference;
    
        public float GetDamage => damage;
        public float GetRange => range;
        public Hand GetGripHand => hand;
    
        public bool HasProjectile => hasProjectile;
        public GameObject GetProjectilePrefab => projectilePrefab;

        public bool HasImpactEffect => hasImpactEffect;
        public GameObject GetImpactEffect => impactParticlePrefab;

        public AssetReferenceAudioClip AttackSFXReference => attackSFXReference;
        public AudioClip AttackSFX { get; private set; }

        public enum Hand
        {
            Right,
            Left,
            Both,
        }

        public async override UniTask InitializeAsync(CancellationToken token = default)
        {
            await base.InitializeAsync(token);
            Logg.Log($"[{GetType().Name}, {nameString}] InitializeAsync() invoked", Logg.LoggingMode.Completed);
            AttackSFX = await ResourceManager.Instance.ExtractAssetRefAsync<AudioClip>(attackSFXReference, token);
            // AttackSFX = await ResourceManager.Instance.ExtractAssetRefAsync(attackSFXReference, token);
        }
    }
}

