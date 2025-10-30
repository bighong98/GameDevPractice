using UnityEngine;

namespace TH.Item
{
    public interface IDividableStorage
    {
        void TryDivide(int index, int expected);
    }
}

