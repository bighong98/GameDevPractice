using UnityEngine;

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
    
    public float GetDamage => damage;
    public float GetRange => range;
    public Hand GetGripHand => hand;
    
    public bool HasProjectile => hasProjectile;
    public GameObject GetProjectilePrefab => projectilePrefab;

    public bool HasImpactEffect => hasImpactEffect;
    public GameObject GetImpactEffect => impactParticlePrefab;

    public enum Hand
    {
        Right,
        Left,
        Both,
    }
}
