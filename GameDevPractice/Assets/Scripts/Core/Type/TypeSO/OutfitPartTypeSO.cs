using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "OutfitPartType", menuName = "Scriptable Objects/Outfit/OutfitPartType")]
    public sealed class OutfitPartTypeSO : ScriptableObject
    {
        [Tooltip("Stable identifier for outfit part taxonomy (e.g. head, body, hair).")]
        public string id;

        [Tooltip("Legacy bridge to equipment slot-based systems. Leave Max when not mapped.")]
        public Enums.EquippedItemSlotType legacyEquipSlot = Enums.EquippedItemSlotType.Max;
    }
}
