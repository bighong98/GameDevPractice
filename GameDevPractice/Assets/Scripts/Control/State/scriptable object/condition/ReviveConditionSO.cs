using System;
using TH.Attribute;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "ReviveConditionSO", menuName = "Scriptable Objects/State Condition/ReviveConditionSO")]
    // ReviveConditionSO 상태 전이 판단 조건 ScriptableObject
    public class ReviveConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Health health)) return false;

            return !health.IsDead;
        }
        
        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.Components.TryGet(out Health health)
                || onTriggered == null)
                return base.Bind(controller, onTriggered);
            
            health.OnRevived += Handler;

            return new DisposableDelegate(() =>
            {
                if (health.IsNotNull())
                    health.OnRevived -= Handler;
            });

            void Handler() { onTriggered.Invoke(); }
        }
    }
}

