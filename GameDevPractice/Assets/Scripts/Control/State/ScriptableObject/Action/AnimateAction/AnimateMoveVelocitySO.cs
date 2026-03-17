// 속도 기반 이동 애니메이션 제어 액션 에셋 스크립트
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TH.Control.State;
using TH.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateMoveVelocitySO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateMoveVelocitySO")]
    // AnimateMoveVelocitySO 애니메이션 제어 액션 ScriptableObject
    public class AnimateMoveVelocitySO : CharacterActionSO, IOnEnterLoopAction
    {
        [SerializeField] private bool enableSamplingLog = true;
        [SerializeField, Min(0.05f)] private float samplingInterval = 0.2f;

        private static readonly Dictionary<int, float> LastSampleTimeByAnimatorId = new();
        private static readonly int ForwardSpeed = Animator.StringToHash("forwardSpeed");

public override void Execute(IActionStateController controller)
        {
            if (controller.Components.TryGet(out TH.Combat.ISkillController skillController) &&
                skillController.HasExecutingSkill &&
                !skillController.CanMoveWhileCasting)
            {
                return;
            }

            if (!controller.Components.TryGet(out NavMeshAgent agent) ||
                !controller.Components.TryGet(out Animator anim))
            {
                return;
            }

            Vector3 velocity = agent.desiredVelocity.sqrMagnitude > 0.0001f ? agent.desiredVelocity : agent.velocity;
            velocity.y = 0f;
            float forwardSpeed = velocity.magnitude;

            anim.SetFloat(ForwardSpeed, forwardSpeed, 0.08f, Time.deltaTime);
            TryLogSampling(anim, agent, forwardSpeed);
        }

        public async UniTask ExecuteLoopAsync(IActionStateController controller, CancellationToken token)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();

            while (!token.IsCancellationRequested)
            {
                Execute(controller);
                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }
        }

    

        private void TryLogSampling(Animator anim, NavMeshAgent agent, float sampledForwardSpeed)
        {
            if (!enableSamplingLog || anim == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            int animatorId = anim.GetInstanceID();
            float interval = Mathf.Max(0.05f, samplingInterval);

            if (LastSampleTimeByAnimatorId.TryGetValue(animatorId, out float lastSampleTime) &&
                now - lastSampleTime < interval)
            {
                return;
            }

            LastSampleTimeByAnimatorId[animatorId] = now;

            AnimatorStateInfo current = anim.GetCurrentAnimatorStateInfo(AnimatorBaseLayer);
            bool inTransition = anim.IsInTransition(AnimatorBaseLayer);
            AnimatorStateInfo next = inTransition ? anim.GetNextAnimatorStateInfo(AnimatorBaseLayer) : default;

            float desiredSpeed = agent != null ? agent.desiredVelocity.magnitude : 0f;
            float actualSpeed = agent != null ? agent.velocity.magnitude : 0f;
            float appliedForwardSpeed = anim.GetFloat(ForwardSpeed);

            this.Log(
                $"[MoveSample] go={anim.gameObject.name}, sampledForward={sampledForwardSpeed:F3}, appliedForward={appliedForwardSpeed:F3}, desiredSpeed={desiredSpeed:F3}, actualSpeed={actualSpeed:F3}, inTransition={inTransition}, currentShort={current.shortNameHash}, currentFull={current.fullPathHash}, currentNorm={current.normalizedTime:F3}, nextShort={next.shortNameHash}, nextFull={next.fullPathHash}, nextNorm={next.normalizedTime:F3}",
                Logg.LoggingMode.Completed);
        }
}

}
