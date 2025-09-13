using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RPG.UI
{
    public class QuestionPopupUI : PopupUI
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

        private bool decided = false;

        private const string DefaultYesString = "예";
        private const string DefaultNoString = "아니오";

        private Action yesAction;
        private Action noAction;
        
        private void Awake()
        {
            Init();
        }

        public override void OnPopupClosed()
        {
            base.OnPopupClosed();
            ClearActions();
        }

        public override bool Init()
        {
            if (base.Init() == false)
                return false;

            BindButton(typeof(Buttons));
            BindTMPText(typeof(TMPTexts));
            
            GetButton((int)Buttons.YesButton).onClick.AddListener(OnYesButtonPressed);
            GetButton((int)Buttons.NoButton).onClick.AddListener(OnNoButtonPressed);
            
            return true;
        }

        public bool SetQuestion(string questionString = null, string yesString = null, string noString = null, Action yesAction = null, Action noAction = null)
        {
            if (string.IsNullOrEmpty(questionString)) return false; // 질문 텍스트는 비어놓을 수 없음
            if (string.IsNullOrEmpty(yesString)) yesString = DefaultYesString;
            if (string.IsNullOrEmpty(noString)) noString = DefaultNoString;
            
            GetTMPText((int)TMPTexts.QuestionText).SetText(questionString);
            GetTMPText((int)TMPTexts.YesText).SetText(yesString);
            GetTMPText((int)TMPTexts.NoText).SetText(noString);

            ClearActions();

            this.yesAction = yesAction;
            this.noAction = noAction;

            decided = false;

            return true;
        }

        private void OnYesButtonPressed()
        {
            DecideAndClose(yesAction);
        }

        private void OnNoButtonPressed()
        {
            DecideAndClose(noAction);
        }

        private void ClearActions()
        {
            noAction = null;
            yesAction = null;
        }

        private void DecideAndClose(Action action)
        {
            if (decided || (PopupCTS?.IsCancellationRequested ?? true))
            {
                ClosePopupUI();
                return;
            }

            try
            {
                decided = true;
                action?.Invoke();
            }
            catch (Exception e) { Debug.LogError($"[{nameof(QuestionPopupUI)}.{nameof(DecideAndClose)}()] {e.Message}"); }
            finally { CancelAndClose(); }
        }
    }
}

