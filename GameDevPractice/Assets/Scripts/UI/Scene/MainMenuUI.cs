using System;
using System.Collections.Generic;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public class MainMenuUI : SceneUI
    {
        #region Enum

        enum Buttons
        {
            Btn_NewGame,
            Btn_Continue,
            Btn_Load,
            Btn_Options,
            Btn_Quit,
        }

        #endregion

        public readonly struct SaveSlotViewData
        {
            public string SaveFileName { get; }
            public string DisplayName { get; }

            public SaveSlotViewData(string saveFileName, string displayName)
            {
                SaveFileName = saveFileName;
                DisplayName = displayName;
            }
        }

        public event Action NewGameRequested;
        public event Action ContinueRequested;
        public event Action LoadRequested;
        public event Action OptionRequested;
        public event Action QuitRequested;
        public event Action<string> LoadSlotSelected;

        [SerializeField] private GameObject loadSlotPanelPrefab;
        [SerializeField] private LoadSlotPanelUI loadSlotPanelUI;

        private const string loadSlotPanelKey = "LoadSlotPanel";

        protected override void Awake()
        {
            base.Awake();

            BindButton(typeof(Buttons));

            BindMenuButton(Buttons.Btn_NewGame, OnStartButtonClicked);
            BindMenuButton(Buttons.Btn_Continue, OnContinueButtonClicked);
            BindMenuButton(Buttons.Btn_Options, OnOptionButtonClicked);
            BindMenuButton(Buttons.Btn_Load, OnLoadButtonClicked);
            BindMenuButton(Buttons.Btn_Quit, OnQuitButtonClicked);
        }

        private void BindMenuButton(Buttons button, Action action)
        {
            var target = GetButton((int)button);
            if (target == null)
                return;

            target.onClick.AddListener(() => action?.Invoke());
        }

        public void SetContinueButtonEnabled(bool enabled)
        {
            SetButtonInteractable(Buttons.Btn_Continue, enabled);
        }

        public void SetLoadButtonEnabled(bool enabled)
        {
            SetButtonInteractable(Buttons.Btn_Load, enabled);
        }

        private void SetButtonInteractable(Buttons button, bool interactable)
        {
            var target = GetButton((int)button);
            if (target != null)
                target.interactable = interactable;
        }

        private void OnStartButtonClicked()
        {
            NewGameRequested?.Invoke();
        }

        private void OnContinueButtonClicked()
        {
            ContinueRequested?.Invoke();
        }

        private void OnLoadButtonClicked()
        {
            LoadRequested?.Invoke();
        }

        private void OnOptionButtonClicked()
        {
            OptionRequested?.Invoke();
        }

        private void OnQuitButtonClicked()
        {
            QuitRequested?.Invoke();
        }

        public void ShowLoadSlots(IReadOnlyList<SaveSlotViewData> slots)
        {
            this.Log($"ShowLoadSlots() slots.Count: {slots.Count}", Logg.LoggingMode.Completed);

            var panel = UIManager.Instance.ShowPopupUI<LoadSlotPanelUI>(loadSlotPanelKey);
            panel.SlotSelected += HandleSlotSelected;
            panel.ShowLoadSlots(slots);
        }

        private void HandleSlotSelected(string saveFile)
        {
            LoadSlotSelected?.Invoke(saveFile);
        }
    }
}
