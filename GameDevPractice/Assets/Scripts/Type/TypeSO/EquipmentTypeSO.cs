using UnityEngine;

// [CreateAssetMenu(fileName = "EquipmentTypeSO", menuName = "Scriptable Objects/Type/Equipment/EquipmentTypeSO")]
public abstract class EquipmentTypeSO : ItemTypeSO
{
    [Header("Equipment")] 
    public Enums.EquipmentType equipmentType;
    public Enums.EquippedItemSlotType slotType;
}
