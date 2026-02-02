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
        Attack, // 공격 관련 능력치
        Resource, // 캐릭터의 자원 관련 능력치 (hp, mp)
        Attribute, // 힘민지
        Experience, // 경험치 관련
        Others, // 나머지
        Defence, // 방어 관련 능력치
    }
}
