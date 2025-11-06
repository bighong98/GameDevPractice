using RPG.Combat;
using UnityEngine;

namespace TH.Resource
{
    public class WeaponTypeHolder : TypeHolder<WeaponTypeSO>
    {
        public Fighter owner; // 무기의 소유자
    }
}

