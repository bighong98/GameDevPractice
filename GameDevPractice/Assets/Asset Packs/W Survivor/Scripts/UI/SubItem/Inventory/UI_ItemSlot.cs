using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_ItemSlot : UI_ItemSlotBase
{
    #region Enums

    enum TMPTexts
    {
        ItemAmountText,
    }

    #endregion
    
    private void Awake()
    {
        Init();
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;
        
        BindTMPText(typeof(TMPTexts));

        return true;
    }
    
    #region Item Method // 아이템 관련 함수

    public override void RemoveIcon() // 슬롯 아이템 제거
    {
        base.RemoveIcon();
        HideText();
    }

    #endregion

    #region AmountText Method // 아이템 수량 텍스트 관련 함수
    
    public void SetItemAmount(int amount)
    {
        if (HasItem && amount > 1)
            ShowText();
        else
            HideText(); 
        GetTMPText((int)TMPTexts.ItemAmountText).text = amount.ToString();
    }

    public void ShowText()
    {
        GetTMPText((int)TMPTexts.ItemAmountText).enabled = true;
    }

    public void HideText() => GetTMPText((int)TMPTexts.ItemAmountText).enabled = false;

    #endregion

    #region Access Method // 접근 가능여부 관련 함수

    public override void SetSlotAccessibleState(bool newState)
    {
        if (_isAccessibleSlot == newState) return;
        if (newState)
        {
            GetImage((int)Images.SlotImage).color = Color.white;
        }
        else
        {
            GetImage((int)Images.SlotImage).color = InAccessibleSlotColor;
        }

        _isAccessibleSlot = newState;
    }

    public override void SetItemAccessibleState(bool newState)
    {
        if (_isAccessibleItem == newState) return;
        if (newState)
        {
            GetImage((int)Images.ItemImage).color = Color.white;
            GetTMPText((int)TMPTexts.ItemAmountText).color = Color.black;
        }
        else
        {
            GetImage((int)Images.ItemImage).color = InAccessibleIconColor;
            GetTMPText((int)TMPTexts.ItemAmountText).color = InAccessibleIconColor;
        }

        _isAccessibleItem = newState;
    }

    #endregion
    
    
    
    
}
