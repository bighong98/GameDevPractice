using System;
using TH.Combat;
using TH.Core.Service;
using UnityEngine;
using TH.Resource;

public sealed class CombatSystem : MonoBehaviour, ICombatSystem
{
    [SerializeField] private DamageRuleSO damageRule;
    private IDamageCalculator damageCalc;

    private void Awake()
    {
        if (ServiceLocator.TryGet(out IDamageCalculator calc))
        {
            damageCalc = calc;
        }
        
        ServiceLocator.Replace<ICombatSystem>(this);
        
        if (damageRule == null)
        {
            ResourceManager.Instance.ReserveOperation(() =>
            {
                damageRule = ResourceManager.Instance.Load<DamageRuleSO>("DamageRuleSO.asset");
            });
        }
    }
    
    private void OnDestroy()
    {
        ServiceLocator.UnRegister<ICombatSystem>(); 
    }

    public void ApplyHit(in HitRequest hitRequest)
    {
        var result = damageCalc.Resolve(hitRequest, damageRule);
        if (hitRequest.Target is Component { gameObject: { activeSelf: true } })
        {
            Util.Log($"[{nameof(CombatSystem)}.{nameof(ApplyHit)}]", Util.LoggingMode.Completed);
            hitRequest.Target.TakeDamage(result);
        }
    }
}
