// 스킬 범위 내 자동 타겟팅 조건 에셋 스크립트
using TH.Combat;
using TH.Control.State;
using TH.Utils;
using UnityEngine;
using TH.Attribute;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AutoTargetFoundConditionSO", menuName = "Scriptable Objects/State Condition/AutoTargetFoundConditionSO")]
    public class AutoTargetFoundConditionSO : ActionStateConditionSO
    {
        private void OnEnable()
        {
            measure = StateConditionMeasures.Polling;
        }

        public override bool Decide(IActionStateController controller)
        {
            if (!controller.IsNotNull()) return base.Decide(controller);
            if (!controller.Components.TryGet(out IAttacker attacker)) return base.Decide(controller);
            if (!controller.Components.TryGet(out ISkillController skillController)) return base.Decide(controller);

            // 이미 공격 중이면 타겟을 찾지 않음
            if (skillController.HasExecutingSkill)
            {
                return false;
            }

            // 스킬을 사용할 준비가 되었는지
            if (!skillController.HasActiveSkill || !skillController.IsActiveSkillReady)
            {
                return false;
            }

            return skillController.TryRefreshAutoTargetCandidate(attacker);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            measure = StateConditionMeasures.Polling;
            base.OnValidate();
        }
#endif
    }
}
