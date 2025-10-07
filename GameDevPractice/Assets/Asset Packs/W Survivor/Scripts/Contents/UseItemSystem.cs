using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UseItemSystem
{
    public void Init()
    {
        //todo: 테이블을 통해 아이템 사용 효과 데이터 저장    
    }
    
    public bool UseItem(int optionGroup)
    {
        switch (optionGroup)
        {
            case 1001:
                return HealPlayer(1);
            
            default:
                Debug.Log("UseItemSystem: Invalid optionGroup");
                return false;
        }   
    }
    
    #region Item Effect Function

    private bool HealPlayer(int amount)
    {
        //todo: 플레이어 체력 회복 로직
        return true;
    }

    #endregion
}
