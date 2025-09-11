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
        
        private CancellationTokenSource PopupCTS; // 본 팝업의 토큰 소스
        private CancellationTokenRegistration ownerCTSRegistration;

        private bool decided = false;

        private const string DefaultYesString = "예";
        private const string DefaultNoString = "아니오";

        private Action yesAction;
        private Action noAction;
        
        private void Awake()
        {
            Init();
        }

        private void OnDisable()
        {
            ownerCTSRegistration.Dispose();
            ClearActions();
            CancelPopupCTS();
        }

        private void OnDestroy()
        {
            ownerCTSRegistration.Dispose();
            ClearActions();
            CancelPopupCTS();
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

        public bool SetQuestion(CancellationToken ownerToken, string questionString = null, string yesString = null, string noString = null, Action yesAction = null, Action noAction = null)
        {
            ownerCTSRegistration.Dispose();
            // 팝업 호출 측에서 전달한 Token이 유효하지 않거나, 질문 string이 비어있으면 실행 취소
            if (!ownerToken.CanBeCanceled || ownerToken.IsCancellationRequested || string.IsNullOrEmpty(questionString))
            {
                CancelAndClose();
                return false;
            }
            
            CancelAndRenewCTS();
            ownerCTSRegistration = ownerToken.Register(CancelAndClose);
            
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

        private void CancelPopupCTS()
        {
            if (PopupCTS == null) return;
            
            if (!PopupCTS.IsCancellationRequested)
                PopupCTS.Cancel();
            PopupCTS.Dispose();
            PopupCTS = null;
        }

        private void CancelAndClose()
        {
            CancelPopupCTS();
            ClosePopupUI();
        }

        private void CancelAndRenewCTS()
        {
            CancelPopupCTS();
            PopupCTS = new CancellationTokenSource();
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

        public void Show()
        {
            if (gameObject.activeSelf) return;
            gameObject.SetActive(true);
        }

        // 필요에 따라 ClosePopupUI 대신 단순 비활성화 (오브젝트 풀링x)
        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
        }
    }
}

