using UnityEngine;

namespace TH.Item
{
    public interface ICountableItem : IGameItem
    {
        bool IsFull { get; }
        bool TrySetAmount(int num); // return true: 개수 변경 성공, false: 개수 변경 실패 (+ 원래 개수로 원복)
        void SetAmount(int num);
        int AddAmount(int num);
        T SeparateAndClone<T>(int expected) where T : ICountableItem;
        T Clone<T>(int expected, out int excess) where T : ICountableItem;
        T Clone<T>(int expected) where T : ICountableItem;
    }
}

