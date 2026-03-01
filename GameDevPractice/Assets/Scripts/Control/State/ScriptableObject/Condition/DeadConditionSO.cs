// 사망 상태 전이 조건 에셋 스크립트
using System;
using TH.Attribute;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "DeadConditionSO", menuName = "Scriptable Objects/State Condition/DeadConditionSO")]
    // DeadConditionSO 상태 전이 판단 조건 ScriptableObject
    public class DeadConditionSO : ActionStateConditionSO
    {
        public override bool Decide(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Health health))
                return base.Decide(controller);

            return health.IsDead;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.Components.TryGet(out Health health)
                || onTriggered == null)
                return base.Bind(controller, onTriggered);
            
            health.OnDead += Handler;

            return new DisposableDelegate(() =>
            {
                if (health.IsNotNull())
                    health.OnDead -= Handler;
            });

            void Handler() { onTriggered.Invoke(); }
        }
    }
}

