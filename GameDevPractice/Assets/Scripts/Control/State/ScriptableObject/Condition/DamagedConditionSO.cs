using System;
using TH.Attribute;
using TH.Combat;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "DamagedConditionSO", menuName = "Scriptable Objects/State Condition/DamagedConditionSO")]
    // DamagedConditionSO 상태 전이 판단 조건 ScriptableObject
    public sealed class DamagedConditionSO : ActionStateConditionSO
    {
        [Header("Damage Filter")]
        [SerializeField, Min(0f)] private float minDamage = 0f;
        [SerializeField] private bool ignoreWhenDead = true;

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null)
                return base.Bind(controller, onTriggered);
            if (!controller.Components.TryGet(out Health health))
                return base.Bind(controller, onTriggered);

            health.OnDamaged += HandleDamaged;

            return new DisposableDelegate(() =>
            {
                if (health.IsNotNull())
                    health.OnDamaged -= HandleDamaged;
            });

            void HandleDamaged(in HitResult hitResult)
            {
                if (ignoreWhenDead && health.IsDead)
                    return;
                if (hitResult.Damage < minDamage)
                    return;

                onTriggered.Invoke();
            }
        }
    }
}
