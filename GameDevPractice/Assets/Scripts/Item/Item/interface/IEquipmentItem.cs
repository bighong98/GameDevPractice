using System.Collections.Generic;
using TH.Attribute.Stat;
using UnityEngine;

namespace TH.Item
{
    public interface IEquipmentItem
    {
        object Owner { get; }
        IReadOnlyCollection<StatModifierData> EquipmentStats { get; }
    }
}
