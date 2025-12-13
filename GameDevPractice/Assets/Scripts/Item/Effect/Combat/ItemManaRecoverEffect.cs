using UnityEngine;

namespace TH.Item.Data
{
    [CreateAssetMenu(fileName = "ItemManaRecoverEffect", menuName = "Scriptable Objects/Type/ItemEffect/ItemManaRecoverEffect")]
    public class ItemManaRecoverEffect : ItemEffectBase
    {
        public override bool TryApply(in ItemUseContext context)
        {
            return true;
        }
    }
}

