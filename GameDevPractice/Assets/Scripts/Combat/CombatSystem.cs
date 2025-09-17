using System;
using TH.Combat;
using UnityEngine;

public sealed class CombatSystem : MonoBehaviour
{
    [SerializeField] private DamageRuleSO damageRule;
    private readonly IDamageCalculator damageCalc = new DamageCalculator();

    private void Awake()
    {
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
