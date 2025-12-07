using System;
using UnityEngine;

namespace TH.Combat
{
    public interface IHealable
    {
        bool Heal(int amount);
        bool HealRatio(float ratio);

        event Action<float> OnHealed;
    }
}

