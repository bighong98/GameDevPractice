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
            this.Log($"({gameObject.name}) - HandleRaycast({caller})", Logg.LoggingMode.Completed);

            if (!caller.Components.TryGet(out IAttacker attacker)) return false;
            if (!attacker.CanAttack(gameObject, out var self)) return false;
            
            attacker.SetTarget(self);
            return true;
        }

        public CursorType GetCursorType()
        {
            return CursorType.Combat;
        }
    }
}
