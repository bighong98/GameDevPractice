using UnityEngine;
using System;


namespace TH.Item
{
    public interface IPlayerStorage : 
        IGameItemStorage, 
        IFilterableStorage, 
        IMutableCapacity, 
        IRearrangeableStorage,
        IUsableItemStorage,
        ICountableItemStorage
    {
        
    }
}

