using UnityEngine;

namespace TH.Item
{
    public class ConsumableItem : CountableItem, IUsableItem
    {
        public ConsumableItem() {}
        public ConsumableItem(ItemTypeSO data, int amount) : base(data, amount) {}
        public bool Use()
        {
            //todo: 사용효과 구현
            if (IsEmpty) return false;
            
            SetAmount(amount - 1);
            return true;
        }

        public bool Use(object user)
        {
            //todo: 사용효과 구현
            return Use();
        }
    }
}

