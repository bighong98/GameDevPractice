// 도착 이벤트 전이 조건 에셋 스크립트
using UnityEngine;
using System;
using TH.Control.Movement;
using TH.Control.State;
using TH.Utils;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "ArriveConditionSO", menuName = "Scriptable Objects/State Condition/ArriveConditionSO")]
    // ArriveConditionSO 상태 전이 판단 조건 ScriptableObject
    public class ArriveConditionSO : ActionStateConditionSO
    {
        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || !onTriggered.IsNotNull()
                || !controller.Components.TryGet(out IMover mover))
                return base.Bind(controller, onTriggered);

            mover.OnArrived += Handler;
            
            return new DisposableDelegate(() =>
            {
                if (mover.IsNotNull())
                    mover.OnArrived -= Handler;
            });
            
            void Handler() { onTriggered.Invoke(); }
        }
    }
}

