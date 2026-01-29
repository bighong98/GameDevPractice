using TH.Attribute;
using TH.Attribute.Stat;
using TH.Utils;
using UnityEngine;

namespace TH.Combat.Service
{
    public class KillEventHandler : IKillEventHandler
    {
        
        private GameStatSO xpRewardSO => GameStats.ExperienceReward;
        
        public void HandleKillEvent(IDamageable victim, IAttacker attacker)
        {
            if (victim.IsNull() || attacker.IsNull())
            {
                this.LogWarning("HandleKillEvent() - invalid victim or attacker instance");
                return;
            }

            if (victim is not Component vc ||
                !vc.TryGetComponent(out IStatHolder victimStatHolder) ||
                victimStatHolder.GetStat(xpRewardSO) is not {} rewardXpAmount)
            {
                this.LogWarning($"HandleKillEvent() - failed to get rewardXp from {victim}");
                return;
            }

            if (attacker is not Component ac ||
                !ac.TryGetComponent(out IExperience attackerExpHolder))
            {
                this.LogWarning($"HandleKillEvent() - failed to get IExperience instance from {attacker}");
                return;
            }

#if UNITY_EDITOR
            float beforeXp = attackerExpHolder.GetCurrXp;
            Logg.Log($"[KillEventHandler] reward:{rewardXpAmount.Value} attacker:{ac.name}({ac.GetInstanceID()}) scene:{ac.gameObject.scene.name} xp:{beforeXp}", Logg.LoggingMode.InProgress, ac);
            attackerExpHolder.GainXp(rewardXpAmount.Value);
            Logg.Log($"[KillEventHandler] xp after:{attackerExpHolder.GetCurrXp}", Logg.LoggingMode.InProgress, ac);
#else
            attackerExpHolder.GainXp(rewardXpAmount.Value);
#endif
        }
    }
}

