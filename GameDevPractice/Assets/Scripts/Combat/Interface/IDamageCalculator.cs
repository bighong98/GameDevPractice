using UnityEngine;

namespace TH.Combat
{
    public interface IDamageCalculator
    {
        public HitResult Resolve(in HitRequest hitRequest, in DamageRuleSO damageRule);
    }
}

