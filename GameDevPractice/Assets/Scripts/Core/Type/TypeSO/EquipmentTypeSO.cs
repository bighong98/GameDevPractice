using TH.Attribute.Stat;
using UnityEngine;
using System.Collections.Generic;

// [CreateAssetMenu(fileName = "EquipmentTypeSO", menuName = "Scriptable Objects/Type/Equipment/EquipmentTypeSO")]
public abstract class EquipmentTypeSO : ItemTypeSO
{
    [Header("Equipment")] 
    public Enums.EquipmentType equipmentType;
    public Enums.EquippedItemSlotType slotType;
    public List<StatModifierData> equipmentStats;
}
