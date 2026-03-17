// 공격 애니메이션 제어 액션 에셋 스크립트
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
    // AnimateAttackSO 애니메이션 제어 액션 ScriptableObject
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

            PlayAttackOnLayer(animator, AnimatorSkillUpperLayer, "upper");
            PlayAttackOnLayer(animator, AnimatorSkillFullBodyLayer, "full");
        }

        public bool TransitionLockRequired { get; } = true;

        public IDisposable BindMinimumCompleted(IActionStateController controller, Action onCompleted)
        {
            if (!controller.IsNotNull() || !onCompleted.IsNotNull() ||
                !controller.Components.TryGet(out Animator animator))
            {
                onCompleted?.Invoke();
                return DisposableDelegate.Empty;
            }

            var cts = new CancellationTokenSource();
            MonitorAnimationAsync(animator, onCompleted, cts.Token).ContinueWith(Handler).Forget();

            return new DisposableDelegate(Handler);

            void Handler()
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }

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

                    if (!TryGetAttackStateInfo(animator, out var stateInfo))
                    {
                        Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - exited Attack state", Logg.LoggingMode.Completed);
                        return;
                    }

                    if (stateInfo.normalizedTime >= Mathf.Max(0f, stateUnlockNormalizedTime) ||
                        animator.GetFloat(CancelAllowHash) > Mathf.Clamp01(cancelAllowThreshold))
                    {
                        Logg.Log($"[{GetType().Name}] MonitorAnimationAsync - unlock state transition", Logg.LoggingMode.Completed);
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
                }
            }
            catch (Exception e)
            {
                Logg.LogError($"[{name}] Animation monitor error: {e}");
            }
            finally
            {
                onCompleted?.Invoke();
            }
        }

        private void PlayAttackOnLayer(Animator animator, int layerIndex, string layerLabel)
        {
            if (!IsValidLayer(animator, layerIndex))
            {
                return;
            }

            if (animator.GetCurrentAnimatorStateInfo(layerIndex).shortNameHash == AttackASSHash)
            {
                LogAttackAnimTrigger(animator, $"crossfade_same_state_{layerLabel}", layerIndex);
                animator.CrossFade(
                    stateHashName: AttackASSHash,
                    normalizedTransitionDuration: Mathf.Clamp01(retriggerTransitionDuration),
                    layer: layerIndex,
                    normalizedTimeOffset: Mathf.Clamp01(retriggerNormalizedTimeOffset));
                LogAttackAnimTrigger(animator, $"crossfade_same_state_after_{layerLabel}", layerIndex);
                return;
            }

            LogAttackAnimTrigger(animator, $"play_from_start_{layerLabel}", layerIndex);
            animator.Play(AttackASSHash, layerIndex, 0f);
            LogAttackAnimTrigger(animator, $"play_from_start_after_{layerLabel}", layerIndex);
        }

        private bool TryGetAttackStateInfo(Animator animator, out AnimatorStateInfo stateInfo)
        {
            if (TryGetAttackStateInfoFromLayer(animator, AnimatorSkillUpperLayer, out stateInfo))
            {
                return true;
            }

            if (TryGetAttackStateInfoFromLayer(animator, AnimatorSkillFullBodyLayer, out stateInfo))
            {
                return true;
            }

            return TryGetAttackStateInfoFromLayer(animator, AnimatorBaseLayer, out stateInfo);
        }

        private static bool TryGetAttackStateInfoFromLayer(Animator animator, int layerIndex, out AnimatorStateInfo stateInfo)
        {
            stateInfo = default;

            if (!IsValidLayer(animator, layerIndex))
            {
                return false;
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layerIndex);
            if (current.shortNameHash == AttackASSHash)
            {
                stateInfo = current;
                return true;
            }

            if (!animator.IsInTransition(layerIndex))
            {
                return false;
            }

            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layerIndex);
            if (next.shortNameHash != AttackASSHash)
            {
                return false;
            }

            stateInfo = next;
            return true;
        }

        private static bool IsValidLayer(Animator animator, int layerIndex)
        {
            return animator != null &&
                   layerIndex >= 0 &&
                   layerIndex < animator.layerCount;
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private void LogAttackAnimTrigger(Animator animator, string mode, int layerIndex)
        {
            if (!IsValidLayer(animator, layerIndex))
            {
                return;
            }

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
            bool inTransition = animator.IsInTransition(layerIndex);
            string currentClip = ResolveCurrentClipName(animator, layerIndex);
            string nextClip = ResolveNextClipName(animator, layerIndex);
            string controllerName = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : "null";

            Logg.Log(
                $"[{nameof(AnimateAttackSO)}.{nameof(Execute)}] mode={mode}, " +
                $"frame={Time.frameCount}, time={Time.time:0.000}, layer={layerIndex}, " +
                $"controller={controllerName}, inTransition={inTransition}, " +
                $"stateHash={stateInfo.shortNameHash}, norm={stateInfo.normalizedTime:0.000}, " +
                $"currentClip={currentClip}, nextClip={nextClip}",
                Logg.LoggingMode.Completed);
        }

        private static string ResolveCurrentClipName(Animator animator, int layerIndex)
        {
            if (!IsValidLayer(animator, layerIndex))
            {
                return "none";
            }

            var clips = animator.GetCurrentAnimatorClipInfo(layerIndex);
            if (clips == null || clips.Length == 0 || clips[0].clip == null)
            {
                return "none";
            }

            return clips[0].clip.name;
        }

        private static string ResolveNextClipName(Animator animator, int layerIndex)
        {
            if (!IsValidLayer(animator, layerIndex))
            {
                return "none";
            }

            var clips = animator.GetNextAnimatorClipInfo(layerIndex);
            if (clips == null || clips.Length == 0 || clips[0].clip == null)
            {
                return "none";
            }

            return clips[0].clip.name;
        }
    }
}

