using TH.Combat.Service;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    public sealed class SkillAnimationEventRelay : MonoBehaviour
    {
        private ISkillController skillController;
        private IAttacker attacker;

        private void Awake()
        {
            skillController = GetComponent(typeof(ISkillController)) as ISkillController;
            attacker = GetComponent(typeof(IAttacker)) as IAttacker;
        }

        public void PlaySkillVfxMarker(string markerName)
        {
            if (skillController == null)
            {
                return;
            }

            SkillTypeSO skill = ResolveCurrentSkill();
            if (skill == null)
            {
                return;
            }

            var context = new SkillEffectPlayContext(
                attacker as Component,
                null,
                default,
                hasHitPoint: false,
                attackInstanceId: 0);
            SkillEffectPlayer.TryPlaySkillEffect(skill, SkillEffectTrigger.OnAnimMarker, context, markerName);
        }

        public void PlaySkillVfxMarker()
        {
            PlaySkillVfxMarker(string.Empty);
        }

        private SkillTypeSO ResolveCurrentSkill()
        {
            if (skillController.HasExecutingSkill)
            {
                return skillController.ExecutingSkill;
            }

            if (skillController.HasResolvedSkill)
            {
                return skillController.ResolvedSkill;
            }

            return skillController.HasActiveSkill ? skillController.ActiveSkill : null;
        }
    }
}
