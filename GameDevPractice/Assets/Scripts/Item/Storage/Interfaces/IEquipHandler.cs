using System;

namespace TH.Item
{
    public interface IEquipHandler
    {
        event EventHandler<EquipArgs> OnEquipmentChanged; // 장비 장착/장착해제 이벤트 델리게이트
    }
    
    public class EquipArgs : EventArgs
    {
        public readonly IGameItem Item;
        public readonly EquipEventState State;

        public EquipArgs(IGameItem item, EquipEventState state)
        {
            this.Item = item;
            this.State = state;
        }
        
        public enum EquipEventState
        {
            Equip,
            UnEquip
        }
    }
}

