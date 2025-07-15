using UnityEngine;

namespace RPG.Stats
{
    public enum CharacterClass
    {
        Default, // means not initialized
        
        Player,
        EnemyTest1,
        EnemyTest2,
        EnemyTest3,
        
        Max, // means End of CharacterClass Enum (Always must be end of enum)
    }

    public enum GameStat
    {
        Health, // 최대체력
        ExperienceReward, // 경험치량(몬스터 처치 시, 플레이어에게는 없음)
    }
    
    public class CharacterStats : MonoBehaviour
    {
        [Range(1, 99)] 
        [SerializeField] private int startingLevel = 1;
        [SerializeField] private CharacterClass characterClass;
        [SerializeField] private ProgressionSO progression;

        public float GetStat(GameStat statType)
        {
            return progression.GetProgressionStat(statType, characterClass, startingLevel);
        }
    }
}

