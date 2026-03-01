
namespace TH.Item.Storage
{
    public interface IPlayerStorage : 
        IGameItemStorage, 
        IFilterableStorage, 
        IMutableCapacity, 
        IRearrangeableStorage,
        ICountableItemStorage,
        IConsumableItemStorage,
        IDividableStorage,
        IReplaceableStorage,
        IStoreAndUse
    {
        
    }
}

