using UnityEngine;

[CreateAssetMenu(fileName = "WeaponTypeSo", menuName = "Scriptable Objects/Type/Equipment/WeaponTypeSO")]
public class WeaponTypeSO : EquipmentTypeSO
{
    [Header("Weapon")] 
    public Enums.WeaponType weaponType;
}
