using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TH.Control.State;
using TH.Utils;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "WaitForSecondsConditionSO", menuName = "Scriptable Objects/State Condition/WaitForSecondsConditionSO")]
    // WaitForSecondsConditionSO 상태 전이 판단 조건 ScriptableObject
    public class WaitForSecondsConditionSO : ActionStateConditionSO
    {
        [SerializeField, Min(0f)] private float delaySeconds = 1f;

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

