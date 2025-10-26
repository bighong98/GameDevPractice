using UnityEngine;
using System;


namespace TH.Item
{
    public interface IPlayerStorage : 
        IGameItemStorage, 
        IStackableStorage,
        IFilterableStorage, 
        IMutableCapacity, 
        IRearrangeableStorage,
        IUsableItemStorage
    {
        
    }
}

