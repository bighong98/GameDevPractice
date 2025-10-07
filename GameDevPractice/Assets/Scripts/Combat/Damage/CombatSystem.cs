using System;
using TH.Combat;
using TH.Core.Service;
using UnityEngine;
using TH.Resource;
using TH.Utils;

public sealed class CombatSystem : ICombatSystem
{
    private DamageRuleSO damageRule;
    private IDamageCalculator damageCalc;

    public CombatSystem()
    {
        if (ServiceLocator.TryGet(out IDamageCalculator calc))
        {
            damageCalc = calc;
        }
        
        ResourceManager.Instance.ReserveOperation(() =>
        {
            damageRule = ResourceManager.Instance.Load<DamageRuleSO>("DamageRuleSO");
        });
    }
    
    public void ApplyHit(in HitRequest hitRequest)
    {
        var result = damageCalc.Resolve(hitRequest, damageRule);
        if (hitRequest.Target is Component { gameObject: { activeSelf: true } })
        {
            Logg.Log($"[{nameof(CombatSystem)}.{nameof(ApplyHit)}]", Logg.LoggingMode.Completed);
            hitRequest.Target.TakeDamage(result);
        }
    }
}
