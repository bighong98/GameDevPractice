using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateAttackSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateAttackSO")]
    public class AnimateAttackSO : CharacterActionSO, IStateTransitionLock
    {
        private static readonly int AttackAnimHash = Animator.StringToHash("attack");
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator)) return;
            animator.SetTrigger(AttackAnimHash);
        }

        public bool TransitionLockRequired { get; } = true;
        public IDisposable BindMinimumCompleted(IActionStateController controller, Action onCompleted)
        {
            if (!controller.IsNotNull() || !onCompleted.IsNotNull()
                || !controller.Components.TryGet(out Animator animator))
            {
                onCompleted?.Invoke();
                return DisposableDelegate.Empty;
            }

            var cts = new CancellationTokenSource();
            MonitorAnimationAsync(animator, onCompleted, cts.Token).Forget();
            
            return new DisposableDelegate(() =>
            {
                cts.Cancel();
                cts.Dispose();
            });
        }
        
        private async UniTaskVoid MonitorAnimationAsync(Animator animator, Action onCompleted, CancellationToken token)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);

                while (true)
                {
                    if (animator == null) return;
                    //todo: layerIndex 1개 이상 생기면 인덱스 지정
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    
                    // 모션 캔슬 허용 파라미터가 임계치(CancelAllowThreshold)를 넘었는지
                    // 애니메이션 진행도가 종료 임계치(AnimationEndThreshold)를 넘었는지
                    // -> 둘 중 하나라도 만족하면 모션 캔슬 가능 판정
                    if (animator.GetFloat(CancelAllowHash) > CancelAllowThreshold 
                        || stateInfo.normalizedTime >= AnimationEndThreshold)
                    {
                        onCompleted?.Invoke();
                        return; 
                    }
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
                }
            }
            catch (Exception e) { Debug.LogError($"[{name}] Animation monitor error: {e}"); }
        }
    }
}

