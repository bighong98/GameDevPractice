using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.UI
{
    public class UI_QuestionPopup : PopupUI
    {
        #region Enums
    
        enum TMPTexts
        {
            QuestionText,
            YesText,
            NoText,
        }

        enum Buttons
        {
            YesButton,
            NoButton,
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

            BindButton(typeof(Buttons));
            BindTMPText(typeof(TMPTexts));
        
            return true;
        }
    }
}

