using TH.Attribute;
using UnityEngine;
using System;

namespace TH.Combat
{
    // 피해를 입힐 수 있는 객체
    public interface IAttacker
    {
        event Action OnAttack;
        event Action<Health> OnTargetSet;
        event Action OnAttackReady;
        
        bool IsTargetInRange { get; }
        bool IsTargetValid { get; }
        Health Target { get; }

        void Attack();
        void SetTarget(Health target, bool forceNotify = false);
        bool CanAttack(GameObject combatTarget, out Health targetHealth);
    }
}

