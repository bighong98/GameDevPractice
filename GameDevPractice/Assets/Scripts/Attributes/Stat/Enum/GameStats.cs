using UnityEngine;

namespace TH.Attribute.Stat
{
    public enum GameStats
    {
        // HP 관련: 100~199
        Health = 101, // 최대체력
        
        // Exp 관련: 1000~1099
        ExperienceReward = 1001, // 경험치량(몬스터 처치 시, 플레이어에게는 없음)
        ExperienceToLevelUp = 1002, // 레벨업에 필요한 경험치 필요량 (반드시 배열 길이가 (최대레벨-1)이어야함)
        
        Max, // 
    }
}


