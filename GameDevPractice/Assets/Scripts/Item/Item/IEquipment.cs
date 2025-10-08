using UnityEngine;

namespace TH.Item
{
    public interface IEquipment
    {
        object Owner { get; }
        bool Equip(object own);
        bool UnEquip();
    }
}

