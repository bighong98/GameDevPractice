using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TH.Control.State;
using TH.Utils;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "WaitForSecondConditionSO", menuName = "Scriptable Objects/State Condition/WaitForSecondConditionSO")]
    public class WaitForSecondConditionSO : ActionStateConditionSO
    {
        [SerializeField, Min(0f)] private float delaySeconds = 1f;
        public override bool Decide(IActionStateController controller)
        {
            return false;
        }

        public override IDisposable Bind(IActionStateController controller, Action onTriggered)
        {
            if (!controller.IsNotNull() || onTriggered == null)
                return base.Bind(controller, onTriggered);
            
            var cts = new CancellationTokenSource();
            RunDelayAsync(delaySeconds, onTriggered, cts.Token).Forget();

            return new DisposableDelegate(() =>
            {
                cts.Cancel();
                cts.Dispose();
            });
        }
        
        private static async UniTaskVoid RunDelayAsync(
            float seconds, Action onTriggered, CancellationToken token)
        {
            if (seconds.IsEqualFloat(0f)) return;
            
            // {seconds}초 만큼 대기
            await UniTask.Delay(
                TimeSpan.FromSeconds(seconds),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                token
            );

            // 대기 종료 -> 트리거 호출
            onTriggered.Invoke();
        }
    }
}

