using TH.SaveLoad;
using UnityEngine;
using System;

namespace TH.Attribute
{
    public class Experience : MonoBehaviour, ISavable
    {
        [SerializeField] private float XP = 0;

        public Action<float> OnExperienceChanged;

        private void Awake()
        {
            
        }

        public void GainExperience(float xp)
        {
            XP += xp;
            OnExperienceChanged?.Invoke(XP);
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

