using TH.Control;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    [RequireComponent(typeof(TH.Attribute.Health))]
    public class CombatTarget : MonoBehaviour, IRaycastable
    {
        public bool HandleRaycast(IPlayerController caller)
        {
            this.Log($"({gameObject.name}) - HandleRaycast({caller})", Logg.LoggingMode.InProgress);
            // if (!TryGetComponent(out IAttacker attacker)
            //     || !attacker.CanAttack(gameObject, out var targetHealth)) return false;
            // attacker.Attack(targetHealth);

            if (!caller.Components.TryGet(out IAttacker attacker)) return false;
            if (!attacker.CanAttack(gameObject, out var self)) return false;
            
            attacker.Attack(self);
            return true;
        }

        public CursorType GetCursorType()
        {
            return CursorType.Combat;
        }
    }
}
