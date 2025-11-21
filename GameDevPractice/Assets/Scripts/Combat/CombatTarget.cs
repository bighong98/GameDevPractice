using System.Collections;
using System.Collections.Generic;
using TH.Control;
using UnityEngine;

namespace TH.Combat
{
    [RequireComponent(typeof(TH.Attribute.Health))]
    public class CombatTarget : MonoBehaviour, IRaycastable
    {
        public bool HandleRaycast(PlayerController caller)
        {
            if (caller.GetComponent<Fighter>() is not { } fighter ||
                !fighter.CanAttack(gameObject, out var targetHealth)) return false;
            
            fighter.Attack(targetHealth);
            return true;
        }

        public CursorType GetCursorType()
        {
            return CursorType.Combat;
        }
    }
}
