using System;
using System.Collections.Generic;
using TH.Core.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public sealed class LoadSlotListModule
    {
        private readonly RectTransform loadSlotContent;
        private readonly GameObject loadSlotTemplate;
        private readonly TMP_Text loadSlotEmptyLabel;
        private readonly List<LoadSlotUI> loadSlotEntries = new();

        public LoadSlotListModule(
            RectTransform loadSlotContent,
            GameObject loadSlotTemplate,
            TMP_Text loadSlotEmptyLabel)
        {
            this.loadSlotContent = loadSlotContent;
            this.loadSlotTemplate = loadSlotTemplate;
            this.loadSlotEmptyLabel = loadSlotEmptyLabel;
        }

        public bool IsValid => loadSlotContent != null && loadSlotTemplate != null;

        public void Populate(IReadOnlyList<SaveSlotViewData> slots, Action<string> onSlotSelected)
        {
            Clear();

            if (slots == null || slots.Count == 0)
            {
                SetEmptyLabelVisible(true);
                return;
            }

            var seenSaveFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var saveInfo in slots)
            {
                string saveFile = saveInfo.SaveFileName;
                if (string.IsNullOrWhiteSpace(saveFile) || !seenSaveFiles.Add(saveFile))
                    continue;

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
                    button.onClick.AddListener(() => onSlotSelected?.Invoke(saveFile));
                }

                var label = slotUI.LabelText;
                if (label != null)
                    label.text = string.IsNullOrWhiteSpace(saveInfo.DisplayName) ? saveFile : saveInfo.DisplayName;

                slotUI.transform.SetSiblingIndex(loadSlotEntries.Count);
                loadSlotEntries.Add(slotUI);
            }

            SetEmptyLabelVisible(loadSlotEntries.Count == 0);
            RebuildLayout();
        }

        public void Clear()
        {
            var targets = new HashSet<LoadSlotUI>();

            foreach (var entry in loadSlotEntries)
            {
                if (entry != null)
                    targets.Add(entry);
            }

            if (loadSlotContent != null)
            {
                for (int i = loadSlotContent.childCount - 1; i >= 0; i--)
                {
                    var child = loadSlotContent.GetChild(i);
                    if (child == null || !child.gameObject.activeSelf)
                        continue;

                    if (child.TryGetComponent<LoadSlotUI>(out var slotUI) && slotUI != null)
                        targets.Add(slotUI);
                }
            }

            foreach (var slotUI in targets)
            {
                if (slotUI == null)
                    continue;

                if (slotUI.Origin != null)
                {
                    if (slotUI.gameObject.activeSelf)
                        PoolManager.Instance.ReleaseFromPool(slotUI);

                    continue;
                }

                UnityEngine.Object.Destroy(slotUI.gameObject);
            }

            loadSlotEntries.Clear();
            RebuildLayout();
        }

        private void SetEmptyLabelVisible(bool isVisible)
        {
            if (loadSlotEmptyLabel != null)
                loadSlotEmptyLabel.gameObject.SetActive(isVisible);
        }

        private void RebuildLayout()
        {
            if (loadSlotContent == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(loadSlotContent);
            if (loadSlotContent.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
    }
}
