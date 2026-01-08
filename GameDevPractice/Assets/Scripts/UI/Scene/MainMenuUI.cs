using System;
using System.Collections.Generic;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public class MainMenuUI : BaseUI
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
        public event Action QuitRequested;
        public event Action<string> LoadSlotSelected;

        [SerializeField] private GameObject loadSlotPanelPrefab;
        [SerializeField] private LoadSlotPanelUI loadSlotPanelUI;
        private bool loadSlotEventsHooked;

        protected override void Awake()
        {
            base.Awake();

            BindButton(typeof(Buttons));

            BindMenuButton(Buttons.Btn_NewGame, StartNewGameAsync);
            BindMenuButton(Buttons.Btn_Continue, ContinueGameAsync);
            BindMenuButton(Buttons.Btn_Load, LoadGameAsync);
            BindMenuButton(Buttons.Btn_Quit, QuitGameAsync);

            ResolveLoadSlotPanelUI();
        }

        private void OnEnable()
        {
            var panel = ResolveLoadSlotPanelUI();
            if (panel != null)
                panel.HideLoadSlotPanel();
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

        private void StartNewGameAsync()
        {
            NewGameRequested?.Invoke();
        }

        private void ContinueGameAsync()
        {
            ContinueRequested?.Invoke();
        }

        private void LoadGameAsync()
        {
            LoadRequested?.Invoke();
        }

        private void QuitGameAsync()
        {
            QuitRequested?.Invoke();
        }

        public void ShowLoadSlots(IReadOnlyList<SaveSlotViewData> slots)
        {
            this.Log($"ShowLoadSlots() slots.Count: {slots.Count}", Logg.LoggingMode.Completed);
            var panel = ResolveLoadSlotPanelUI();
            if (panel == null)
                return;

            panel.ShowLoadSlots(slots);
        }

        private LoadSlotPanelUI ResolveLoadSlotPanelUI()
        {
            if (loadSlotPanelUI != null)
            {
                EnsureLoadSlotEvents();
                return loadSlotPanelUI;
            }

            if (loadSlotPanelPrefab == null)
                return null;

            var parent = GetComponentInParent<Canvas>()?.transform ?? transform;
            var panelGo = Instantiate(loadSlotPanelPrefab, parent);
            panelGo.name = loadSlotPanelPrefab.name;
            loadSlotPanelUI = panelGo.GetComponent<LoadSlotPanelUI>();
            if (loadSlotPanelUI == null)
                loadSlotPanelUI = panelGo.AddComponent<LoadSlotPanelUI>();

            EnsureLoadSlotEvents();
            return loadSlotPanelUI;
        }

        private void EnsureLoadSlotEvents()
        {
            if (loadSlotPanelUI == null || loadSlotEventsHooked)
                return;

            loadSlotPanelUI.SlotSelected += HandleSlotSelected;
            loadSlotEventsHooked = true;
        }

        private void HandleSlotSelected(string saveFile)
        {
            LoadSlotSelected?.Invoke(saveFile);
        }
    }
}
