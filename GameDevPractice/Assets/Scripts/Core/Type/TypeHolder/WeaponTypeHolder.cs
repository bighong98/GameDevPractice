using TH.Combat;
using UnityEngine;

namespace TH.Resource
{
    public class WeaponTypeHolder : TypeHolder<WeaponTypeSO>
    {
        public IFighter owner; // 무기의 소유자
    }
}

