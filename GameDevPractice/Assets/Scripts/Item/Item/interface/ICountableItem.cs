using UnityEngine;

namespace TH.Item
{
    public interface ICountableItem : IGameItem
    {
        bool IsFull { get; }
        void SetAmount(int num);
        int AddAmount(int num);
        T SeparateAndClone<T>(int expected) where T : ICountableItem;
        T Clone<T>(int expected, out int excess) where T : ICountableItem;
        T Clone<T>(int expected) where T : ICountableItem;
    }
}

