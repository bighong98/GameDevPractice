using System;
using RPG.Stats;
using TH.Core.Pool;
using UnityEngine;

public class CharacterTypeHolder : TypeHolder<CharacterTypeSO>
{
    private Action LevelUpEffectAction;
    public override void OnCreateFromPool()
    {
        base.OnCreateFromPool();
        if (Type is PlayerTypeSO { levelUpEffect: {} effect } 
            && GetComponent<CharacterStats>() is {} charStats)
        {
            LevelUpEffectAction = () =>
            {
                PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(effect, null, transform.position);
            };
            charStats.OnLevelUp += ShowLevelUpEffect;
        }
    }

    private void ShowLevelUpEffect(int dummy)
    {
        LevelUpEffectAction?.Invoke();
    }
}
