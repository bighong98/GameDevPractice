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

        #region Deprecated

        // private async UniTask MonitorAnimationAsync(Animator animator, Action onCompleted, CancellationToken token)
        // {
        //     try
        //     {
        //         await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        //         bool enteredAttack = false;
        //         
        //         while (true)
        //         {
        //             if (token.IsCancellationRequested) break;
        //             if (animator == null) return;
        //             
        //             bool inTransition = animator.IsInTransition(AnimatorBaseLayer); // layer index
        //             var curr = animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer);
        //             var next = inTransition
        //                 ? animator.GetNextAnimatorStateInfo(AnimatorBaseLayer)
        //                 : default;
        //             
        //             bool currIsAttack = curr.shortNameHash == AttackASSHash;
        //             bool nextIsAttack = inTransition && next.shortNameHash == AttackASSHash;
        //             bool isOrWillBeAttack = currIsAttack || nextIsAttack;
        //             
        //             // "Attack에 실제로 진입했다"를 한 번 확인하기 전에는
        //             // 전환 중 current가 Attack이 아니라고 바로 종료하지 않도록 방어
        //             if (!enteredAttack)
        //             {
        //                 if (isOrWillBeAttack)
        //                     enteredAttack = true;
        //
        //                 await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        //                 continue;
        //             }
        //             
        //             // Attack에서 완전히 벗어났으면 종료
        //             if (!isOrWillBeAttack)
        //             {
        //                 Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - exited Attack state", Logg.LoggingMode.Completed);
        //                 return;
        //             }
        //             
        //             var stateInfo = nextIsAttack ? next : curr;
        //             
        //             // 모션 캔슬 가능 조건 체크
        //             if (stateInfo.normalizedTime >= AnimationEndThreshold ||
        //                 animator.GetFloat(CancelAllowHash) > CancelAllowThreshold)
        //             {
        //                 Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - unlock state transition", Logg.LoggingMode.Completed);
        //                 return;
        //             }
        //             
        //             await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        //         }
        //     }
        //     catch (Exception e) { Debug.LogError($"[{name}] Animation monitor error: {e}"); }
        //     finally { onCompleted?.Invoke(); }
        // }
        
        // private async UniTask MonitorAnimationAsync(Animator animator, Action onCompleted, CancellationToken token)
        // {
        //     try
        //     {
        //         await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        //         
        //         var trs = animator.transform;
        //         var agent = trs.GetComponent<UnityEngine.AI.NavMeshAgent>();
        //         int frameCount = 0;
        //         
        //         while (true)
        //         {
        //             if (token.IsCancellationRequested) break;
        //             if (animator == null) break;
        //             
        //             var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        //
        //             if ((++frameCount) % 10 == 0)
        //             {
        //                 var pos = trs.position;
        //                 var rotY = trs.eulerAngles.y;
        //
        //                 var deltaPos = animator.deltaPosition;
        //                 var deltaRot = animator.deltaRotation.eulerAngles;
        //
        //                 var nextPos = agent.nextPosition;
        //                 var nextDelta = nextPos - pos;
        //
        //                 this.Log(
        //                     $"pos={pos:F3} rotY={rotY:F1} | " +
        //                     $"applyRootMotion={animator.applyRootMotion} " +
        //                     $"anim.deltaPos={deltaPos:F4} anim.deltaRot={deltaRot:F2} | " +
        //                     $"agent.updatePos={agent.updatePosition} updateRot={agent.updateRotation} | " +
        //                     $"vel={agent.velocity:F3} desiredVel={agent.desiredVelocity:F3} | " +
        //                     $"isStopped={agent.isStopped} hasPath={agent.hasPath} pathPending={agent.pathPending} remDist={agent.remainingDistance:F3} | " +
        //                     $"nextPos={nextPos:F3} (next-pos)={nextDelta:F3} | " +
        //                     $"stateHash={stateInfo.shortNameHash} normTime={stateInfo.normalizedTime:F3}",
        //                     Logg.LoggingMode.InProgress
        //                 );
        //             }
        //             
        //             
        //             // Attack 상태에서 벗어났으면 종료
        //             if (stateInfo.shortNameHash != AttackASSHash)
        //             {
        //                 Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - exited Attack state", Logg.LoggingMode.Completed);
        //                 return;
        //             }
        //             
        //             // 모션 캔슬 가능 조건 체크
        //             if (stateInfo.normalizedTime >= AnimationEndThreshold ||
        //                 animator.GetFloat(CancelAllowHash) > CancelAllowThreshold)
        //             {
        //                 Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - unlock state transition", Logg.LoggingMode.Completed);
        //                 return;
        //             }
        //             
        //             await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        //         }
        //     }
        //     catch (Exception e) { Debug.LogError($"[{name}] Animation monitor error: {e}"); }
        //     finally { onCompleted?.Invoke(); }
        // }

        #endregion
        
    }
}

