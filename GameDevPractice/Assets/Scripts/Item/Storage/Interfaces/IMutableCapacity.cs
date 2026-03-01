
using System;

namespace TH.Item
{
    public interface IMutableCapacity
    {
        public int MaxCapacity { get; } // 설정 가능한 수용량 최댓값
        event Action<int> OnCapacityChanged; 
        bool SetCapacity(int capa, bool byForce = false);
    }
}

