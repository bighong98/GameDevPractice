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
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator)) return;

            if (animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer).shortNameHash == AttackASSHash)
            {
                animator.CrossFade(
                    stateHashName: AttackASSHash, 
                    normalizedTransitionDuration: 0.1f, 
                    layer: AnimatorBaseLayer, 
                    normalizedTimeOffset: 0.2f);
            }
            else
            {
                animator.Play(AttackASSHash, AnimatorBaseLayer, 0f);
            }
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
            MonitorAnimationAsync(animator, onCompleted, cts.Token).ContinueWith(Handler);
            
            return new DisposableDelegate(Handler);

            void Handler()
            {
                if (!cts.IsCancellationRequested)
                    cts.Cancel();
                cts.Dispose();
            }
        }
        
        private async UniTask MonitorAnimationAsync(Animator animator, Action onCompleted, CancellationToken token)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                
                while (true)
                {
                    if (token.IsCancellationRequested) break;
                    if (animator == null) break;
                    
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    
                    // Attack 상태에서 벗어났으면 종료
                    if (stateInfo.shortNameHash != AttackASSHash)
                    {
                        Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - exited Attack state", Logg.LoggingMode.Completed);
                        return;
                    }
                    
                    // 모션 캔슬 가능 조건 체크
                    if (stateInfo.normalizedTime >= AnimationEndThreshold ||
                        animator.GetFloat(CancelAllowHash) > CancelAllowThreshold)
                    {
                        Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - unlock state transition", Logg.LoggingMode.Completed);
                        return;
                    }
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
                }
            }
            catch (Exception e) { Debug.LogError($"[{name}] Animation monitor error: {e}"); }
            finally { onCompleted?.Invoke(); }
        }
    }
}

