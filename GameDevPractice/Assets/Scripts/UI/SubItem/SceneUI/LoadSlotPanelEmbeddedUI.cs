using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using TMPro;
using UnityEngine;

namespace TH.UI
{
    public class LoadSlotPanelEmbeddedUI : BaseUI
    {
        [SerializeField] private RectTransform loadSlotContent;
        [SerializeField] private GameObject loadSlotTemplate;
        [SerializeField] private AssetReferenceGameObject loadSlotTemplateReference;
        [SerializeField] private TMP_Text loadSlotEmptyLabel;

        private LoadSlotListModule loadSlotListModule;

        public event Action<string> SlotSelected;

        protected override void Awake()
        {
            base.Awake();
            EnsureReferences();
            InitLoadSlotListModule();
            EnsureLoadSlotTemplateAsync().Forget();
        }

        private void OnDisable()
        {
            loadSlotListModule?.Clear();
        }

        public void ShowLoadSlots(IReadOnlyList<SaveSlotViewData> slots)
        {
            ShowLoadSlotsAsync(slots).Forget();
        }

        private async UniTaskVoid ShowLoadSlotsAsync(IReadOnlyList<SaveSlotViewData> slots)
        {
            EnsureReferences();

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

        private void EnsureReferences()
        {
            if (loadSlotContent == null)
                loadSlotContent = Util.FindChild<RectTransform>(gameObject, "LoadSlotContent", true);

            if (loadSlotEmptyLabel == null)
                loadSlotEmptyLabel = Util.FindChild<TMP_Text>(gameObject, "EmptyLabel", true);
        }

        private void InitLoadSlotListModule()
        {
            loadSlotListModule = new LoadSlotListModule(loadSlotContent, loadSlotTemplate, loadSlotEmptyLabel);
        }

        private void HandleSlotSelected(string saveFile)
        {
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
