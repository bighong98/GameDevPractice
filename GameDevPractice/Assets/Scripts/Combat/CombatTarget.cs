using System;
using TH.Control;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    [RequireComponent(typeof(TH.Attribute.Health))]
    public class CombatTarget : MonoBehaviour, IRaycastable
    {
        public event Action<CombatTarget> OnInvalidated;

        public bool HandleRaycast(IRaycastHolder caller)
        {
            this.Log($"({gameObject.name}) - HandleRaycast({caller})", Logg.LoggingMode.Completed);

            if (!caller.Components.TryGet(out IAttacker attacker)) return false;
            if (!attacker.CanAttack(gameObject, out var self)) return false;

            attacker.SetTarget(self, forceNotify: true);

            return true;
        }

        public CursorType GetCursorType()
        {
            return CursorType.Combat;
        }

        private void OnDisable()
        {
            OnInvalidated?.Invoke(this);
        }
    }
}
