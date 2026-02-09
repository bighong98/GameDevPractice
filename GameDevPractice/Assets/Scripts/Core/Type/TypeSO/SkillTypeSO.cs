using System;
using TH.Attribute.Stat;
using TH.Combat;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "SkillTypeSO", menuName = "Scriptable Objects/Type/Skill/SkillTypeSO")]
    public class SkillTypeSO : ScriptableObject
    {
        [Header("Skill")]
        [SerializeField] private string skillId;
        [SerializeField] private GameStatSO attackSourceStatSO;
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField, Min(0f)] private float range = 2f;
        [SerializeField, Min(0f)] private float cooldown = 1f;
        [SerializeField] private AudioClip castSfx;

        [Header("VFX")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject impactParticlePrefab;

        public string SkillId => string.IsNullOrWhiteSpace(skillId) ? name : skillId;
        public GameStatSO AttackSourceStatSO => attackSourceStatSO;
        public float BaseDamage => baseDamage;
        public DamageType DamageType => damageType;
        public float Range => range;
        public float Cooldown => cooldown;
        public AudioClip CastSFX => castSfx;
        public bool HasProjectile => projectilePrefab != null;
        public GameObject ProjectilePrefab => projectilePrefab;
        public bool HasImpactEffect => impactParticlePrefab != null;
        public GameObject ImpactParticlePrefab => impactParticlePrefab;
    }
}
