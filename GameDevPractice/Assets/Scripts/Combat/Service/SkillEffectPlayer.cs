using TH.Core.Service;
using TH.Resource;
using UnityEngine;

namespace TH.Combat.Service
{
    public static class SkillEffectPlayer
    {
        public static bool TryPlaySkillEffect(SkillTypeSO skill, Transform origin)
        {
            if (skill == null || !skill.HasSkillEffect || skill.SkillEffectPrefab == null || origin == null)
            {
                return false;
            }

            PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(skill.SkillEffectPrefab, null, origin.position);
            return true;
        }

        public static bool TryPlayOnHitEffect(SkillTypeSO skill, Vector3 position)
        {
            if (skill == null || !skill.HasOnHitEffect || skill.OnHitEffectPrefab == null)
            {
                return false;
            }

            PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(skill.OnHitEffectPrefab, null, position);
            return true;
        }
    }
}
