using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    // 장비 슬롯 UI 스크립트
    public class UI_EquipmentSlot : ItemSlotBaseUI
    {
        private Color _originalHighlightColor;
        private static readonly Color WarningHighlightColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        private void Awake()
        {
            Init();
        }

        public override bool Init()
        {
            if (base.Init() == false)
                return false;

            _originalHighlightColor = GetImage((int)Images.HighLightImage).color;
            
            return true;
        }
        
        public bool IsValidIndex(int idx) =>
            idx is >= (int)Enums.EquippedItemSlotType.Weapon and (int)Enums.EquippedItemSlotType.Max;
        
        public void ShowWarningHighlight()
        {
            GetImage((int)Images.HighLightImage).color = WarningHighlightColor;
            ShowHighlight();
        }
        public override void HideHighlight()
        {
            GetImage((int)Images.HighLightImage).color = _originalHighlightColor;
            base.HideHighlight();
        }
        
    }
}
    
