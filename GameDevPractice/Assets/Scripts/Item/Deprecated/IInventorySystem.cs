using UnityEngine;
using RPG.Item;

namespace TH.Item
{
    public interface IInventorySystem
    {
        int AddItem(RPG.Item.Item item, int amount, bool checkInstanceType = false, bool useImmediately = false);
    }
}


