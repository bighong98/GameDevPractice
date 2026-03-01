// 이동 명령 감지 전이 조건 에셋 스크립트
using System;
using TH.Control.Movement;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "MoveConditionSO", menuName = "Scriptable Objects/State Condition/MoveConditionSO")]
    // MoveConditionSO 상태 전이 판단 조건 ScriptableObject
    public class MoveConditionSO : ActionStateConditionSO
    {
        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null 
                                        || !controller.Components.TryGet(out IMover mover))
                return base.Bind(controller, onTriggered);

            mover.OnDestinationSet += Handler;
            
            return new DisposableDelegate(() =>
            {
                if (mover.IsNotNull())
                    mover.OnDestinationSet -= Handler;
            });
            
            void Handler() { onTriggered.Invoke(); }
        }
    }
}

