using System;
using System.Collections.Generic;
using TH.Combat.Service;
using UnityEngine;

namespace TH.Combat
{
    [Serializable]
    public sealed class SkillOnHitProcEntry
    {
        [SerializeField] private bool oncePerAttackInstance = true;
        [SerializeField] private List<SkillConditionSO> conditions = new();
        [SerializeField] private List<SkillOnHitEffectSO> effects = new();

        public bool OncePerAttackInstance => oncePerAttackInstance;
        public bool HasEffects => effects != null && effects.Exists(effect => effect != null);

        public bool Evaluate(in SkillOnHitProcContext context)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null)
                {
                    continue;
                }

                if (!condition.Evaluate(context))
                {
                    return false;
                }
            }

            return true;
        }

        public void Execute(in SkillOnHitProcContext context, ICombatSystem combatSystem)
        {
            if (combatSystem == null || effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                effect.Execute(context, combatSystem);
            }
        }
    }
}
