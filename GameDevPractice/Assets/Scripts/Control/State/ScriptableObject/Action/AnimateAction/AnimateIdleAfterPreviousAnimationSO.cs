using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(
        fileName = "AnimateIdleAfterPreviousAnimationSO",
        menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateIdleAfterPreviousAnimationSO")]
    public class AnimateIdleAfterPreviousAnimationSO : CharacterActionSO
    {
        private static readonly int ForwardSpeed = Animator.StringToHash("forwardSpeed");

        [SerializeField, Range(0f, 1f)] private float idleTransitionDuration = 0.3f;
        [SerializeField, Min(0f)] private float maxWaitSeconds = 1.5f;
        [SerializeField] private bool skipWaitWhenAlreadyLocomotion = true;
        [SerializeField, Min(0f)] private float forwardSpeedBlendOutDuration = 0.12f;

        public override void Execute(IActionStateController controller)
        {
            if (controller == null ||
                !controller.Components.TryGet(out Animator animator) ||
                animator == null)
            {
                return;
            }

            if (!controller.Components.TryGet(out IAttackStateExitSignalSource attackExitSignal) ||
                attackExitSignal == null)
            {
                ApplyIdle(animator, controller.StateToken);
                return;
            }

            WaitAndPlayIdleAsync(animator, attackExitSignal, controller.StateToken).Forget();
        }

        private async UniTaskVoid WaitAndPlayIdleAsync(Animator animator, IAttackStateExitSignalSource attackExitSignal, CancellationToken token)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();

            if (token.IsCancellationRequested || animator == null || attackExitSignal == null)
            {
                return;
            }

            int initialStateHash = animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer).shortNameHash;
            if (skipWaitWhenAlreadyLocomotion && initialStateHash == LocomotionASSHash)
            {
                ApplyIdle(animator, token);
                return;
            }

            int snapshotEpoch = attackExitSignal.CurrentAttackEpoch;
            if (!ShouldWaitForAttackExit(attackExitSignal, snapshotEpoch))
            {
                ApplyIdle(animator, token);
                return;
            }

            bool attackExited = false;
            IDisposable subscription = attackExitSignal.SubscribeAttackExited(OnAttackExited);
            try
            {
                if (!ShouldWaitForAttackExit(attackExitSignal, snapshotEpoch))
                {
                    attackExited = true;
                }

                if (!attackExited)
                {
                    UniTask exitTask = UniTask.WaitUntil(() => attackExited, cancellationToken: token);
                    if (maxWaitSeconds > 0f)
                    {
                        UniTask timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(maxWaitSeconds), cancellationToken: token);
                        await UniTask.WhenAny(exitTask, timeoutTask).SuppressCancellationThrow();
                    }
                    else
                    {
                        await exitTask.SuppressCancellationThrow();
                    }
                }
            }
            finally
            {
                subscription?.Dispose();
            }

            if (token.IsCancellationRequested || animator == null)
            {
                return;
            }

            ApplyIdle(animator, token);

            void OnAttackExited(int exitedEpoch)
            {
                if (exitedEpoch >= snapshotEpoch)
                {
                    attackExited = true;
                }
            }
        }

        private static bool ShouldWaitForAttackExit(IAttackStateExitSignalSource attackExitSignal, int attackEpoch)
        {
            return attackExitSignal.IsAttackActive && attackExitSignal.LastExitedAttackEpoch < attackEpoch;
        }

        private void ApplyIdle(Animator animator, CancellationToken token)
        {
            if (animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer).shortNameHash != LocomotionASSHash)
            {
                animator.CrossFade(
                    stateHashName: LocomotionASSHash,
                    normalizedTransitionDuration: Mathf.Clamp01(idleTransitionDuration),
                    layer: AnimatorBaseLayer);
            }

            BlendForwardSpeedToZeroAsync(animator, token).Forget();
        }

        private async UniTaskVoid BlendForwardSpeedToZeroAsync(Animator animator, CancellationToken token)
        {
            float duration = Mathf.Max(0f, forwardSpeedBlendOutDuration);
            if (duration <= Mathf.Epsilon)
            {
                animator.SetFloat(ForwardSpeed, 0f);
                return;
            }

            float startForwardSpeed = animator.GetFloat(ForwardSpeed);
            float elapsed = 0f;

            while (!token.IsCancellationRequested && animator != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                animator.SetFloat(ForwardSpeed, Mathf.Lerp(startForwardSpeed, 0f, t));
                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }

            if (!token.IsCancellationRequested && animator != null)
            {
                animator.SetFloat(ForwardSpeed, 0f);
            }
        }
    }
}

