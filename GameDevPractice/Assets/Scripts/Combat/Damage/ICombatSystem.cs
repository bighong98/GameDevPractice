using UnityEngine;

namespace TH.Combat
{
    public interface ICombatSystem
    {
        void ApplyHit(in HitRequest hitRequest);
    }
}

