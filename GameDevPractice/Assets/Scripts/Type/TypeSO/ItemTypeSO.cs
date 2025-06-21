using UnityEngine;

[CreateAssetMenu(fileName = "ItemTypeSO", menuName = "Scriptable Objects/Type/ItemTypeSO")]
public class ItemTypeSO : BaseTypeSO
{
    [Header("Item Info")] 
    public Enums.ItemType itemType;
    public int maxAmount = 1; // default: 1
    public string desc;
    public bool isUsable; // Usable = Consumable(소비 가능) + Equipable(장착 가능)
}
