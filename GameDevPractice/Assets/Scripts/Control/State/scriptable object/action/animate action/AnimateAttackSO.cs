using System;
using System.Diagnostics;
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
        [Header("Attack Feel")]
        [SerializeField, Range(0f, 1f)] private float retriggerTransitionDuration = 0.03f;
        [SerializeField, Range(0f, 1f)] private float retriggerNormalizedTimeOffset = 0.15f;
        [SerializeField, Range(0f, 1.5f)] private float stateUnlockNormalizedTime = 0.85f;
        [SerializeField, Range(0f, 1f)] private float cancelAllowThreshold = 0.8f;

        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out Animator animator)) return;

            IDisposable runtimeLock = null;
            if (!controller.IsTransitionLocked)
            {
                runtimeLock = controller.AcquireTransitionLock(this);
                MonitorAnimationAsync(animator, () => runtimeLock.Dispose(), controller.StateToken)
                    .Forget();
            }

            if (animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer).shortNameHash == AttackASSHash)
            {
                LogAttackAnimTrigger(animator, "crossfade_same_state");
                animator.CrossFade(
                    stateHashName: AttackASSHash, 
                    normalizedTransitionDuration: Mathf.Clamp01(retriggerTransitionDuration), 
                    layer: AnimatorBaseLayer, 
                    normalizedTimeOffset: Mathf.Clamp01(retriggerNormalizedTimeOffset));
                LogAttackAnimTrigger(animator, "crossfade_same_state_after");
            }
            else
            {
                LogAttackAnimTrigger(animator, "play_from_start");
                animator.Play(AttackASSHash, AnimatorBaseLayer, 0f);
                LogAttackAnimTrigger(animator, "play_from_start_after");
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
                    if (stateInfo.normalizedTime >= Mathf.Max(0f, stateUnlockNormalizedTime) ||
                        animator.GetFloat(CancelAllowHash) > Mathf.Clamp01(cancelAllowThreshold))
                    {
                        Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - unlock state transition", Logg.LoggingMode.Completed);
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
                }
            }
            catch (Exception e) { Logg.LogError($"[{name}] Animation monitor error: {e}"); }
            finally { onCompleted?.Invoke(); }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private void LogAttackAnimTrigger(Animator animator, string mode)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer);
            bool inTransition = animator.IsInTransition(AnimatorBaseLayer);
            string currentClip = ResolveCurrentClipName(animator);
            string nextClip = ResolveNextClipName(animator);
            string controllerName = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : "null";

            Logg.Log(
                $"[{nameof(AnimateAttackSO)}.{nameof(Execute)}] mode={mode}, " +
                $"frame={Time.frameCount}, time={Time.time:0.000}, " +
                $"controller={controllerName}, inTransition={inTransition}, " +
                $"stateHash={stateInfo.shortNameHash}, norm={stateInfo.normalizedTime:0.000}, " +
                $"currentClip={currentClip}, nextClip={nextClip}",
                Logg.LoggingMode.Completed);
        }

        private static string ResolveCurrentClipName(Animator animator)
        {
            var clips = animator.GetCurrentAnimatorClipInfo(AnimatorBaseLayer);
            if (clips == null || clips.Length == 0 || clips[0].clip == null)
            {
                return "none";
            }

            return clips[0].clip.name;
        }

        private static string ResolveNextClipName(Animator animator)
        {
            var clips = animator.GetNextAnimatorClipInfo(AnimatorBaseLayer);
            if (clips == null || clips.Length == 0 || clips[0].clip == null)
            {
                return "none";
            }

            return clips[0].clip.name;
        }
    }
}

