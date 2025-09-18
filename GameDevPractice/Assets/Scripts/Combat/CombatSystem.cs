using System;
using TH.Combat;
using TH.Core.Service;
using UnityEngine;

public sealed class CombatSystem : MonoBehaviour
{
    [SerializeField] private DamageRuleSO damageRule;
    private IDamageCalculator damageCalc;

    private void Awake()
    {
        if (ServiceLocator.TryGet(out IDamageCalculator calc))
        {
            damageCalc = calc;
        }
        
        if (damageRule == null)
        {
            ResourceManager.Instance.ReserveOperation(() =>
            {
                damageRule = ResourceManager.Instance.Load<DamageRuleSO>("DamageRuleSO.asset");
            });
        }
    }

    public void ApplyHit(in HitRequest hitRequest)
    {
        var result = damageCalc.Resolve(hitRequest, damageRule);
        if (hitRequest.Target is Component { gameObject: { activeSelf: true } })
        {
            hitRequest.Target.TakeDamage(result);
        }
    }
}
