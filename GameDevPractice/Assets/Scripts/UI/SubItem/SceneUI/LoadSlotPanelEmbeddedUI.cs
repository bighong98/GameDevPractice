using System;
using System.Collections.Generic;
using System.Threading;
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
        [SerializeField] private AssetReferenceGameObject loadSlotTemplateReference;
        [SerializeField] private TMP_Text loadSlotEmptyLabel;

        [NonSerialized] private GameObject loadSlotTemplate;
        private LoadSlotListModule loadSlotListModule;
        private CancellationTokenSource showLoadSlotsRequestCTS;
        private CancellationTokenSource linkedShowLoadSlotsRequestCTS;

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

        private void OnDestroy()
        {
            CancelShowLoadSlotsRequest();
        }

        public void ShowLoadSlots(IReadOnlyList<SaveSlotViewData> slots, CancellationToken ownerToken = default)
        {
            var requestToken = CreateShowLoadSlotsToken(ownerToken);
            ShowLoadSlotsAsync(slots, requestToken).Forget();
        }

        private async UniTaskVoid ShowLoadSlotsAsync(IReadOnlyList<SaveSlotViewData> slots, CancellationToken requestToken)
        {
            try
            {
                EnsureReferences();

                int slotCount = slots?.Count ?? 0;
                this.Log($"ShowLoadSlots() slots.Count: {slotCount}", Logg.LoggingMode.Completed);

                if (loadSlotTemplate == null)
                {
                    await EnsureLoadSlotTemplateAsync().AttachExternalCancellation(requestToken);
                }

                requestToken.ThrowIfCancellationRequested();

                if (loadSlotListModule == null || !loadSlotListModule.IsValid)
                {
                    InitLoadSlotListModule();
                    if (loadSlotListModule == null || !loadSlotListModule.IsValid)
                    {
                        return;
                    }
                }

                requestToken.ThrowIfCancellationRequested();
                loadSlotListModule.Populate(slots, HandleSlotSelected);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation when the option menu closes or a newer request is issued.
            }
        }

        private CancellationToken CreateShowLoadSlotsToken(CancellationToken ownerToken)
        {
            CancelShowLoadSlotsRequest();

            showLoadSlotsRequestCTS = new CancellationTokenSource();
            if (!ownerToken.CanBeCanceled)
                return showLoadSlotsRequestCTS.Token;

            linkedShowLoadSlotsRequestCTS =
                CancellationTokenSource.CreateLinkedTokenSource(showLoadSlotsRequestCTS.Token, ownerToken);
            return linkedShowLoadSlotsRequestCTS.Token;
        }

        private void CancelShowLoadSlotsRequest()
        {
            CancelAndDispose(ref linkedShowLoadSlotsRequestCTS);
            CancelAndDispose(ref showLoadSlotsRequestCTS);
        }

        private static void CancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts == null)
                return;

            if (!cts.IsCancellationRequested)
                cts.Cancel();

            cts.Dispose();
            cts = null;
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

            var loadedTemplate = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(
                loadSlotTemplateReference,
                destroyCancellationToken);
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
