
using System;

namespace TH.Item
{
    public interface IMutableCapacity
    {
        event Action<int> OnCapacityChanged;
        bool SetCapacity(int capa, bool byForce = false);
    }
}

