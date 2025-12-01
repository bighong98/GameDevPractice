using UnityEngine;
using System;
using TH.Item.Storage;


namespace TH.Item.Storage
{
    public interface IPlayerStorage : 
        IGameItemStorage, 
        IFilterableStorage, 
        IMutableCapacity, 
        IRearrangeableStorage,
        IUsableItemStorage,
        ICountableItemStorage,
        IConsumableItemStorage,
        IDividableStorage
    {
        
    }
}

