using UnityEngine;
using TH.Resource;

namespace TH.Item
{
    public class CountableItem : GameItem, ICountableItem
    {
        public CountableItem() { }

        public CountableItem(ItemTypeSO data, int amount = 1) : base(data)
        {
            base.amount = amount;
        }

        public bool IsFull => amount >= itemData.maxAmount;
        
        public void SetAmount(int num)
        {
            int max = itemData.maxAmount;
            amount = Mathf.Clamp(num, 0, max);
        }

        public int AddAmount(int num)
        {
            int total = amount + num;
            SetAmount(total);

            int max = itemData.maxAmount;
            return (total > max) ? (total - max) : 0;
        }

        // 개수 변경 시도 + 실패 시 원복
        public bool TrySetAmount(int num)
        {
            int originAmount = amount;
            SetAmount(num);
            if (amount == num) return true;
            else
            {
                SetAmount(originAmount);
                return false;
            }
        }

        public T SeparateAndClone<T>(int expected) where T : ICountableItem
        {
            if (amount <= 1) return default;

            if (expected > amount - 1)
                expected = amount - 1;

            amount -= expected;
            return Clone<T>(amount);
        }

        public T Clone<T>(int expected, out int excess) where T : ICountableItem
        {
            int max = itemData.maxAmount;
            if (expected > max)
            {
                excess = max - expected;
                expected = max;
            }
            else excess = 0;

            return Clone<T>(expected);
        }

        public T Clone<T>(int expected) where T : ICountableItem
        {
            T clone = base.Clone<T>();
            if (clone is ICountableItem cItemClone)
            {
                cItemClone.SetAmount(expected);
            }

            return clone;
        }
    }
}

