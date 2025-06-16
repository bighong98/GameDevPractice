using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UI_Popup : UI_Base
{
    protected bool _escapable = true; // ESC로 종료 허용 여부. default: true (허용)
    public bool escapable
    {
        get { return _escapable; }
    }
    
    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        return true;
    }

    public virtual void ClosePopupUI()
    {
        // 상속 클래스에서 해당 함수를 override해서 닫기 전에 필요한 작업 ex - 취소 등을 수행
        UIManager.Instance.ClosePopupUI(this);
    }
}
