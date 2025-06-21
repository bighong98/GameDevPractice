using UnityEngine;

[CreateAssetMenu(fileName = "ArmorTypeSO", menuName = "Scriptable Objects/Type/Equipment/ArmorTypeSO")]
public class ArmorTypeSO : EquipmentTypeSO
{
    [Header("Armor")] 
    public Enums.ArmorType armorType;
}
