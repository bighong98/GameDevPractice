using UnityEngine;
using System;


namespace TH.Item
{
    public interface IPlayerInventory : 
        IGameItemStorage, 
        IStackableStorage,
        IFilterableStorage, 
        IMutableCapacity, 
        IRearrangeableStorage,
        IUsableItemStorage
    {
        
    }
}

