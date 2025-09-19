using UnityEngine;
using System;

namespace TH.Attribute
{
    public interface IExperience
    {
        event Action<float> OnXpChanged;
        float GetCurrXp { get; }
        void SetXp(float xp, bool updateLevel = true);
        void GainXp(float xp);
    }
}

