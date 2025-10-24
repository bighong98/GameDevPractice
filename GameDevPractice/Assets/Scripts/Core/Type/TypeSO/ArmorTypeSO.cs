using UnityEngine;

[CreateAssetMenu(fileName = "ArmorTypeSO", menuName = "Scriptable Objects/Type/Item/ArmorTypeSO")]
public class ArmorTypeSO : EquipmentTypeSO
{
    [Header("Armor")] 
    public Enums.ArmorType armorType;
}
