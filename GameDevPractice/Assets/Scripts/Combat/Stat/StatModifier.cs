using UnityEngine;
using StatModCalcType = Enums.StatModCalcType;

public class StatModifier
{
    public readonly float Value;
    public readonly StatModCalcType Type;
    public readonly int Order;
    public readonly object Source;

    public StatModifier(float value, StatModCalcType type, int order, object source = null)
    {
        Value = value;
        Type = type;
        Order = order;
        Source = source;
    }
    
    public StatModifier(float value, StatModCalcType type) : this (value, type, (int) type, null) {} 
    public StatModifier(float value, StatModCalcType type, object source) : this (value, type, (int)type, source) {}

    #region Mod Usage Example (ignore exact class name)

    #region modExample

    // public class Player
    // {
    //     public PlayerStat Strength;
    // }
    //
    // public class Item
    // {
    //     private StatModifier mod1, mod2;
    //      
    //     public void Equip(Player c)
    //     {
    //         // need to store modifiers in variables before adding them to the stat
    //         // source = this -> means mod1 and mod2 are from Item class
    //         mod1 = new StatModifier(10, StatModCalcType.Add, this);
    //         mod2 = new StatModifier(0.1f, StatModCalcType.PerAdd, this);
    //         c.Strength.AddModifier(mod1);
    //         c.Strength.AddModifier(mod2);
    //         
    //         // same with above
    //         c.Strength.AddModifier(new StatModifier(10, StatModCalcType.Add, this));
    //         c.Strength.AddModifier(new StatModifier(0.1f, StatModCalcType.PerAdd, this));
    //     }
    //
    //     public void UnEquip(Player c)
    //     {
    //         // need stored modifiers by AddModifier to find and remove it
    //         //c.Strength.RemoveModifier(mod1);
    //         //c.Strength.RemoveModifier(mod2);
    //         c.Strength.RemoveModifiersFromSource(this);
    //     }
    // }

    #endregion

    #endregion
}
