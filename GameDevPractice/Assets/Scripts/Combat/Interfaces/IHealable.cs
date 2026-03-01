using System;
using UnityEngine;

namespace TH.Combat
{
    public interface IHealable
    {
        bool Heal(int amount, bool byForce = false);
        bool HealRatio(float ratio, bool byForce = false);

        event Action<float> OnHealed;
    }
}

