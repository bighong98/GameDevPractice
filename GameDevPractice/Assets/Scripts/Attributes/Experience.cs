using RPG.Saving;
using RPG.Stats;
using UnityEngine;
using System;

namespace RPG.Attribute
{
    public class Experience : MonoBehaviour, ISavable
    {
        [SerializeField] private float XP = 0;

        public Action<float> OnExperienceGained;
        
        public void GainExperience(float xp)
        {
            XP += xp;
            OnExperienceGained?.Invoke(XP);
        }

        public float GetCurrentXP() => this.XP;

        public struct ExperienceSaveData
        {
            [SerializeField] public float XP;

            public ExperienceSaveData(float xp)
            {
                XP = xp;
            }
        }
        
        public object CaptureState()
        {
            return new ExperienceSaveData(XP);
        }

        public bool RestoreState(object state)
        {
            if (state is ExperienceSaveData data)
            {
                XP = data.XP;
                return true;
            }

            return false;
        }
    }
}

