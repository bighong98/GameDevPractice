using System;
using TH.Combat;
using TH.Core.Service;
using UnityEngine;
using TH.Resource;
using TH.Utils;

public sealed class CombatSystem : ICombatSystem
{
    private DamageRuleSO damageRule;
    private readonly IDamageCalculator damageCalc;

    public CombatSystem(IResourceLoader resourceLoader, IDamageCalculator damageCalc)
    {
        this.damageCalc = damageCalc;
        resourceLoader.NotifyResourceLoad += (label) =>
        {
            if (!string.Equals(label, "PreLoad")) return;
            if (!resourceLoader.TryLoad("DamageRuleSO", out damageRule))
                Logg.LogError($"[CombatSystem] failed to load DamageRuleSO");
        };
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
