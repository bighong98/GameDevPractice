using System;
using UnityEngine;

namespace TH.Attribute.Stat
{
    [CreateAssetMenu(fileName = "GameStatSO", menuName = "Scriptable Objects/GameStat/GameStatSO")]
    public class GameStatSO : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private int legacyId;
        [SerializeField] private GameStatCategory category;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int LegacyId => legacyId;
        public GameStatCategory Category => category;

        private void OnEnable()
        {
            GameStats.Register(this);
        }
    }

    public enum GameStatCategory
    {
        Attack,
        Resource,
        Attribute,
        Experience,
    }
}
