using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CountableItem : BaseItem
{
    // private int _maxAmount;
    // public int MaxAmount => _maxAmount;
    //
    // private int _amount;
    // public int Amount => _amount;

    public int MaxAmount;
    public int Amount;
    
    public bool IsMax => Amount >= MaxAmount;
    public bool IsEmpty => (Amount <= 0);

    public CountableItem(int id, int optionGroup, string name, string desc, string itemSprite, string dropSprite, bool isUsable, int maxAmount, int amount = 1) 
        : base (id, type: (int)Enums.ItemType.Countable, optionGroup, name, desc, itemSprite, dropSprite, isUsable)
    {
        MaxAmount = maxAmount;
        Amount = amount;
    }
    
    public void SetAmount(int amount)
    {
        Amount = Mathf.Clamp(amount, 0, MaxAmount);
    }

    public int AddAmount(int amount)
    {
        int total = Amount + amount;
        SetAmount(total);
        // 최대 개수 초과시 초과량 반환 (초과량 숫자만 반환함)
        return (total > MaxAmount) ? (total - MaxAmount) : 0;
    }

    public T SeparateAndClone<T>(int amount) where T : CountableItem
    {
        if (Amount <= 1) return null;

        if (amount > Amount - 1)
            amount = Amount - 1;

        Amount -= amount;
        return Clone<T>(amount);
    }
    
    public T Clone<T>(int amount, out int excess) where T : CountableItem // 초과분 확인 가능
    {
        if (amount > MaxAmount) // 최대치를 초과한 경우
        {
            excess = MaxAmount - amount;
            amount = MaxAmount;
        }
        else
            excess = 0;

        T clone = Clone<T>(amount);
        
        return clone;
    }

    public override T Clone<T>(int amount = 1)
    {
        T clone = base.Clone<T>(amount);
        if (clone is CountableItem countableClone)
            countableClone.SetAmount(amount);
        
        return clone;
    }

}
