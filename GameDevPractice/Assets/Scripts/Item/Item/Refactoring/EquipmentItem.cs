using UnityEngine;

namespace TH.Item
{
    public class EquipmentItem : GameItem, IUsableItem
    {
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

