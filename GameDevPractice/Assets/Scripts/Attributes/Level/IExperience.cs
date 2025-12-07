using UnityEngine;
using System;

namespace TH.Attribute
{
    public interface IExperience
    {
        event Action<float> OnXpChanged; // xp 변동 시 이벤트(현재 xp 총량 전달)
        event Action<float> OnXpGained; // xp 변동 시 이벤트(xp 변동량 전달)
        float GetCurrXp { get; }
        void SetXp(float xp, bool updateLevel = true);
        void GainXp(float xp);
    }
}

