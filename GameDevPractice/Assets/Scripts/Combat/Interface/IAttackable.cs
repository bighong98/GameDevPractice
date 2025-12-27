using TH.Attribute;
using UnityEngine;
using System;

namespace TH.Combat
{
    // 피해를 입힐 수 있는 객체
    public interface IAttackable
    {
        event Action OnAttack;
        event Action<Health> OnTargetChanged;
        
        bool IsTargetInRange { get; }
        bool IsTargetValid { get; }
        Health Target { get; }
        
        void Attack(Health target);
        bool CanAttack(GameObject combatTarget, out Health targetHealth);
    }
}

