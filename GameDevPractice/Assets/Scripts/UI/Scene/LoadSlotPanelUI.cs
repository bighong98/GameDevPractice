using System;
using System.Collections.Generic;
using TH.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public class LoadSlotPanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform loadSlotContent;
        [SerializeField] private GameObject loadSlotTemplate;
        [SerializeField] private TMP_Text loadSlotEmptyLabel;
        [SerializeField] private Button loadSlotCloseButton;

        private readonly List<GameObject> loadSlotEntries = new();

        public event Action<string> SlotSelected;

        private void Awake()
        {
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

        public void HideLoadSlotPanel()
        {
            gameObject.SetActive(false);
        }

        private void HookLoadSlotEvents()
        {
            if (loadSlotCloseButton == null)
                return;

            loadSlotCloseButton.onClick.RemoveAllListeners();
            loadSlotCloseButton.onClick.AddListener(HideLoadSlotPanel);
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
                var slotGo = Instantiate(loadSlotTemplate, loadSlotContent);
                slotGo.name = $"Slot_{saveFile}";
                slotGo.SetActive(true);

                var button = slotGo.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() =>
                    {
                        HideLoadSlotPanel();
                        SlotSelected?.Invoke(saveFile);
                    });
                }

                var label = slotGo.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = saveInfo.DisplayName;

                loadSlotEntries.Add(slotGo);
            }
        }

        private void ClearLoadSlotEntries()
        {
            foreach (var entry in loadSlotEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            loadSlotEntries.Clear();
        }
    }
}
