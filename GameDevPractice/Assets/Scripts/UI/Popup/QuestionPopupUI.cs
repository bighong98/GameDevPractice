using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace TH.UI
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
        
        protected override void Awake()
        {
            base.Awake();
            Init();
        }

        public override void OnPopupClosed()
        {
            ClearActions();
            base.OnPopupClosed();
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

        public bool SetQuestion(string questionString = null, string yesString = null, string noString = null, Action YesAction = null, Action NoAction = null)
        {
            if (string.IsNullOrEmpty(questionString)) return false; // 질문 텍스트는 비어놓을 수 없음
            if (string.IsNullOrEmpty(yesString)) yesString = DefaultYesString;
            if (string.IsNullOrEmpty(noString)) noString = DefaultNoString;
            
            GetTMPText((int)TMPTexts.QuestionText).SetText(questionString);
            GetTMPText((int)TMPTexts.YesText).SetText(yesString);
            GetTMPText((int)TMPTexts.NoText).SetText(noString);

            ClearActions();
            this.yesAction = YesAction;
            this.noAction = NoAction;
            decided = false;

            return true;
        }

        public void PrewarmGlyphs(string warmCharacters)
        {
            if (string.IsNullOrEmpty(warmCharacters))
                return;

            var warmedFontIds = new HashSet<int>(3);
            TryPrewarmGlyphs(GetTMPText((int)TMPTexts.QuestionText), warmCharacters, warmedFontIds);
            TryPrewarmGlyphs(GetTMPText((int)TMPTexts.YesText), warmCharacters, warmedFontIds);
            TryPrewarmGlyphs(GetTMPText((int)TMPTexts.NoText), warmCharacters, warmedFontIds);
        }

        private static void TryPrewarmGlyphs(TMPro.TMP_Text text, string warmCharacters, HashSet<int> warmedFontIds)
        {
            var font = text?.font;
            if (font == null)
                return;

            int fontId = font.GetInstanceID();
            if (!warmedFontIds.Add(fontId))
                return;

            if (font.atlasPopulationMode != TMPro.AtlasPopulationMode.Dynamic)
                return;

            // Glyph prewarm is best-effort. Missing characters are handled by font/fallback setup.
            font.TryAddCharacters(warmCharacters, out _);
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
            
            decided = true;
            action?.Invoke();
            CancelPopupCTS();
        }
    }
}

