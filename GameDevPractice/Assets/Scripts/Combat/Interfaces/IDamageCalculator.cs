using UnityEngine;
using TH.Attribute;

namespace TH.Combat
{
    public interface IDamageCalculator
    {
        public HitResult Resolve(in HitRequest hitRequest, in DamageRuleSO damageRule);
    }
}

