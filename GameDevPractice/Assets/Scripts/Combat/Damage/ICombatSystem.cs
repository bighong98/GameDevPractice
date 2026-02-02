using UnityEngine;

namespace TH.Combat.Service
{
    public interface ICombatSystem
    {
        void ApplyHit(in HitRequest hitRequest);
    }
}

