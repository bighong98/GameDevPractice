using UnityEngine;

[CreateAssetMenu(fileName = "WeaponTypeSo", menuName = "Scriptable Objects/Type/Equipment/WeaponTypeSO")]
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
    
    public float GetDamage => damage;
    public float GetRange => range;
    public Hand GetGripHand => hand;
    
    public bool HasProjectile => hasProjectile;
    public GameObject GetProjectilePrefab => projectilePrefab;

    public enum Hand
    {
        Right,
        Left,
        Both,
    }

    #region Deprecated

    // public void Spawn(Transform handTransform, Animator animator)
    // {
    //     Instantiate(prefab, handTransform); // todo: Object Pooling 적용
    //     if (weaponAnimatorOverride != null)
    //     {
    //         animator.runtimeAnimatorController = weaponAnimatorOverride;
    //     }
    // }

    #endregion
}
