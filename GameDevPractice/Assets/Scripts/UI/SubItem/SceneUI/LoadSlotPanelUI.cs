using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
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
        [SerializeField] private AssetReferenceGameObject loadSlotTemplateReference;
        [SerializeField] private TMP_Text loadSlotEmptyLabel;
        [SerializeField] private Button loadSlotCloseButton;

        private LoadSlotListModule loadSlotListModule;

        public event Action<string> SlotSelected;

        protected override void Awake()
        {
            base.Awake();
            InitLoadSlotListModule();
            HookLoadSlotEvents();
            EnsureLoadSlotTemplateAsync().Forget();
        }

        public override void OnReleaseFromPool()
        {
            base.OnReleaseFromPool();
            loadSlotListModule?.Clear();
        }

        public void ShowLoadSlots(IReadOnlyList<SaveSlotViewData> slots)
        {
            ShowLoadSlotsAsync(slots).Forget();
        }

        private async UniTaskVoid ShowLoadSlotsAsync(IReadOnlyList<SaveSlotViewData> slots)
        {
            int slotCount = slots?.Count ?? 0;
            this.Log($"ShowLoadSlots() slots.Count: {slotCount}", Logg.LoggingMode.Completed);

            if (loadSlotTemplate == null)
            {
                await EnsureLoadSlotTemplateAsync();
            }

            if (loadSlotListModule == null || !loadSlotListModule.IsValid)
            {
                InitLoadSlotListModule();
                if (loadSlotListModule == null || !loadSlotListModule.IsValid)
                {
                    return;
                }
            }

            loadSlotListModule.Populate(slots, HandleSlotSelected);
            gameObject.SetActive(true);
        }

        private void InitLoadSlotListModule()
        {
            loadSlotListModule = new LoadSlotListModule(loadSlotContent, loadSlotTemplate, loadSlotEmptyLabel);
        }

        private void HookLoadSlotEvents()
        {
            if (loadSlotCloseButton == null)
                return;

            loadSlotCloseButton.onClick.RemoveAllListeners();
            loadSlotCloseButton.onClick.AddListener(ClosePopupUI);
        }

        private void HandleSlotSelected(string saveFile)
        {
            ClosePopupUI();
            SlotSelected?.Invoke(saveFile);
        }

        private async UniTask<bool> EnsureLoadSlotTemplateAsync()
        {
            if (loadSlotTemplate != null)
            {
                return true;
            }

            if (loadSlotTemplateReference == null || !loadSlotTemplateReference.RuntimeKeyIsValid())
            {
                return false;
            }

            var loadedTemplate = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(loadSlotTemplateReference, destroyCancellationToken);
            if (loadedTemplate == null)
            {
                return false;
            }

            loadSlotTemplate = loadedTemplate;
            InitLoadSlotListModule();
            return true;
        }
    }
}
