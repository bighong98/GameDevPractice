using UnityEngine;
using System;


namespace TH.Item
{
    public interface IPlayerInventory : IGameItemStorage, IFilterableStorage, IMutableCapacity, IRearrangeableStorage
    {
        public int MaxCapacity { get; }
    }
}

