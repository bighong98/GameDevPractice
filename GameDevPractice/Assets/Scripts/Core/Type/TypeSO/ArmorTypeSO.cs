using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "ArmorTypeSO", menuName = "Scriptable Objects/Type/Item/ArmorTypeSO")]
    public class ArmorTypeSO : EquipmentTypeSO
    {
        [Header("Armor")] 
        [SerializeField] private OutfitKeySO outfitKeySO;
        [SerializeField] private Enums.ArmorType armorType;

        public OutfitKeySO OutfitKey => outfitKeySO;
        public Enums.ArmorType ArmorType => armorType;
    }
}

