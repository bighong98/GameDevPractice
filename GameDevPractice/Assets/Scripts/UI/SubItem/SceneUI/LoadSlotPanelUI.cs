using System;
using System.Collections.Generic;
using TH.Core.Service;
using TH.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public class LoadSlotPanelUI : PopupUI
    {
        [SerializeField] private RectTransform loadSlotContent;
        [SerializeField] private GameObject loadSlotTemplate;
        [SerializeField] private TMP_Text loadSlotEmptyLabel;
        [SerializeField] private Button loadSlotCloseButton;

        private readonly List<LoadSlotUI> loadSlotEntries = new();

        public event Action<string> SlotSelected;

        protected override void Awake()
        {
            base.Awake();
            HookLoadSlotEvents();
        }

        public void ShowLoadSlots(IReadOnlyList<MainMenuUI.SaveSlotViewData> slots)
        {
            this.Log($"ShowLoadSlots() slots.Count: {slots.Count}", Logg.LoggingMode.Completed);

            if (loadSlotContent == null || loadSlotTemplate == null)
                return;

            PopulateLoadSlotList(slots);
            gameObject.SetActive(true);
        }

        private void HookLoadSlotEvents()
        {
            if (loadSlotCloseButton == null)
                return;

            loadSlotCloseButton.onClick.RemoveAllListeners();
            loadSlotCloseButton.onClick.AddListener(ClosePopupUI);
        }

        private void PopulateLoadSlotList(IReadOnlyList<MainMenuUI.SaveSlotViewData> slots)
        {
            ClearLoadSlotEntries();

            if (loadSlotEmptyLabel != null)
                loadSlotEmptyLabel.gameObject.SetActive(slots == null || slots.Count == 0);

            if (slots == null || slots.Count == 0)
                return;

            foreach (var saveInfo in slots)
            {
                string saveFile = saveInfo.SaveFileName;
                var slotUI = PoolManager.Instance.GetFromPool<LoadSlotUI>(
                    loadSlotTemplate,
                    loadSlotContent,
                    worldPositionStays: false);
                if (slotUI == null)
                    continue;

                slotUI.gameObject.name = $"Slot_{saveFile}";

                var button = slotUI.SlotBotton;
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() =>
                    {
                        ClosePopupUI();
                        SlotSelected?.Invoke(saveFile);
                    });
                }

                var label = slotUI.LabelText;
                if (label != null)
                    label.text = saveInfo.DisplayName;

                loadSlotEntries.Add(slotUI);
            }
        }

        private void ClearLoadSlotEntries()
        {
            foreach (var entry in loadSlotEntries)
            {
                if (entry != null)
                    PoolManager.Instance.ReleaseFromPool(entry);
            }
            loadSlotEntries.Clear();
        }
    }
}
