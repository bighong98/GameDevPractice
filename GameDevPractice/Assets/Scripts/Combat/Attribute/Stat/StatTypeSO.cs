using System;
using UnityEngine;

namespace TH.Attribute.Stat
{
    [CreateAssetMenu(fileName = "StatType", menuName = "Scriptable Objects/GameStat/StatType")]
    public class StatTypeSO : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private int legacyId;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int LegacyId => legacyId;

        private void OnEnable()
        {
            GameStats.Register(this);
        }
    }
}
