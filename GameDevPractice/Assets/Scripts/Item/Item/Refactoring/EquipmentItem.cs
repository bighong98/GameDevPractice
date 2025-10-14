using System.Collections.Generic;
using TH.Attribute.Stat;
using UnityEngine;

namespace TH.Item
{
    public class EquipmentItem : GameItem, IUsableItem, IEquipmentItem
    {
        public object Owner { get; private set; } // todo: set Owner
        public IReadOnlyCollection<StatModifierData> EquipmentStats => (GetItemInfo as EquipmentTypeSO)?.equipmentStats;
        public EquipmentItem() {}

        public EquipmentItem(ItemTypeSO data) : base(data)
        {
            
        }
        
        public new T Clone<T>() where T : IGameItem
        {
            return (T)this.MemberwiseClone();
        }

        public bool Use()
        {
            throw new System.NotImplementedException();
        }

        public bool Use(object user)
        {
            throw new System.NotImplementedException();
        }
    }
}

