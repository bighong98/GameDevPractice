using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.Combat
{
    // 피해를 입을 수 있거나, 공격 대상이 될 수 있는 객체
    public interface IDamageable
    {
        // event Action<HitResult> OnDamaged;
        event HitEvent OnDamaged;
        event Action<IReadOnlyList<float>> OnDamagedBatch;
        void TakeDamage(in HitResult hitResult);
    }

    public delegate void HitEvent(in HitResult hr);
}

