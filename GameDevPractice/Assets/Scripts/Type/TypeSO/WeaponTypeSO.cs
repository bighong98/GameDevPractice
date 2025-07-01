using UnityEngine;

[CreateAssetMenu(fileName = "WeaponTypeSo", menuName = "Scriptable Objects/Type/Equipment/WeaponTypeSO")]
public class WeaponTypeSO : EquipmentTypeSO
{
    [Header("Weapon")] 
    public Enums.WeaponType weaponType;
    public AnimatorOverrideController weaponAnimatorOverride;
    [SerializeField] private float damage;
    [SerializeField] private float range;
    public void Spawn(Transform handTransform, Animator animator)
    {
        Instantiate(prefab, handTransform); // todo: Object Pooling 적용
        if (weaponAnimatorOverride != null)
        {
            animator.runtimeAnimatorController = weaponAnimatorOverride;
        }
    }

    public float GetDamage => damage;
    public float GetRange => range;
}
