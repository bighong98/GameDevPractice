using UnityEngine;

namespace TH.Combat.Service 
{
    public interface IKillEventHandler
    {
        void HandleKillEvent(IDamageable victim, IAttacker attacker);
    }
}

