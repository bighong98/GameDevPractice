using UnityEngine;

namespace RPG.Item
{
    public class CountableItem : Item
    {
        public CountableItem() {}
        public CountableItem(ItemTypeSO data, int amount = 1) : base(data)
        {
            Amount = amount;
        }
        
        public bool IsFull => Amount >= ItemData.maxAmount;
        
        public void SetAmount(int num)
        {
            int max = ItemData.maxAmount;
            Amount = Mathf.Clamp(num, 0, max); // 초과분은 버려짐
        }
        
        public int AddAmount(int num)
        {
            int total = Amount + num;
            SetAmount(total);

            int max = ItemData.maxAmount;
            return (total > max) ? (total - max) : 0; // 최대 개수 초과시 초과분 반환(초과하지 않으면 0 반환)
        }

        public T SeparateAndClone<T>(int amount) where T : CountableItem
        {
            if (base.Amount <= 1) return null; // 1개 이하로는 분리 불가능, null 반환

            if (amount > base.Amount - 1) // 예외처리) 아이템 개수보다 더 큰 값을 요구할 경우 1개만 남기고 분리, ex)11개에 SeparateAndClone(90) -> result: Clone(11 - 1)
                amount = Amount - 1;

            Amount -= amount;
            return Clone<T>(amount);
        }

        public T Clone<T>(int amount, out int excess) where T : CountableItem
        {
            int max = ItemData.maxAmount;
            
            if (amount > max)
            {
                excess = max - amount;
                amount = max;
            }
            else
            {
                excess = 0;
            }

            return Clone<T>(amount);
        }

        public T Clone<T>(int amount = 1) where T : CountableItem
        {
            T clone = base.Clone<T>();
            if (clone is CountableItem countableClone)
            {
                countableClone.SetAmount(amount);
            }

            return clone;
        }
        
    }
}

