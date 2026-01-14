using System;
using System.Collections.Generic;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Item;
using UnityEngine;
using UnityEngine.Pool;
using TH.Utils;
using UnityEngine.UI;
using TH.Resource;

namespace TH.UI
{
    //인벤토리 아이템 설명 툴팁용 스크립트
    public sealed class ItemTooltipUI : BaseUI
    {
        #region Enums

        enum TMPTexts
        {
            ItemNameText,
            ItemTypeText,
        }

        #endregion

        [SerializeField] private Transform descParent;
        [SerializeField] private RectTransform bodyRect;
        [SerializeField] private GameObject descPanelTemplate;
        
        private ObjectPool<IPoolObject> descPanelPool;
        private ItemTypeSO lastItemInfo;
        private readonly List<ItemTooltipDescPanel> descPanels = new();

        private IScreenSpaceClamper screenClamper;

        protected override void Awake()
        {
            base.Awake();
            Init();
        }

        #region Initialization

        public override bool Init()
        {
            if (base.Init() == false)
                return false;

            if (canvas == null && !TryGetComponent(out canvas))
            {
                this.LogWarning("failed to GetComponent<Canvas>, Add Canvas with Default Setting", context: this);
                canvas = gameObject.AddComponent<Canvas>();
            }

            if (transform is not RectTransform rect)
            {
                this.LogWarning($"UI doesnt have valid RectTransform for {nameof(IScreenSpaceClamper)}", context: this);
            }
            else screenClamper = new ScreenSpaceClamper(rect, canvas.rootCanvas);

            BindTMPText(typeof(TMPTexts));
            CacheLayoutTargets();
            InitDescPanelPool();

            return true;
        }

        private void CacheLayoutTargets()
        {
            if (descParent == null)
                return;

            if (bodyRect == null && transform is RectTransform rootRect)
            {
                var bodyTransform = rootRect.Find("Body");
                if (bodyTransform != null)
                    bodyRect = bodyTransform as RectTransform;
            }
        }

        private void InitDescPanelPool()
        {
            if (descPanelTemplate == null || descParent == null) return;

            descPanelPool = PoolManager.Instance.GetPool(
                descPanelTemplate,
                descParent,
                createAction: null,
                getAction: obj =>
                {
                    if (obj is Component component)
                    {
                        component.transform.SetParent(descParent, worldPositionStays: false);
                        component.transform.localScale = Vector3.one;
                    }
                },
                registerPool: false);
        }

        #endregion

        #region Public API

        public void ShowTooltip() => gameObject.SetActive(true);
        public void HideTooltip() => gameObject.SetActive(false);

        public void ShowTooltipAt(Vector2 screenPos, IGameItem item, TooltipDetailLevel detailLevel = TooltipDetailLevel.Brief)
        {
            ShowTooltip(item, detailLevel, out bool contentChanged);

            if (contentChanged && transform is RectTransform rectTransform)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            ClampToScreen(screenPos);
        }

        public void MoveTooltip(Vector2 pos)
        {
            ClampToScreen(pos);
        }

        public void MoveTooltip(Vector3 pos)
        {
            ClampToScreen(pos);
        }

        #endregion
        
        private void ShowTooltip(IGameItem item, TooltipDetailLevel detailLevel, out bool contentChanged)
        {
            contentChanged = false;
            
            if (item.IsNull() || item is not {GetItemInfo: {} itemInfo} || itemInfo.IsNull())
            {
                this.LogWarning("ShowTooltip: invalid item data");
                return;
            }

            ShowTooltip();

            if (lastItemInfo == null || !ReferenceEquals(lastItemInfo, itemInfo))
            {
                contentChanged = true;
                lastItemInfo = itemInfo;
                PrepareTooltip(itemInfo, detailLevel);
                RebuildTooltipLayout();
            }
        }

        private void PrepareTooltip(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString ?? string.Empty);
            GetTMPText((int)TMPTexts.ItemTypeText)?.SetText(itemInfo.itemType.ToString() ?? string.Empty);

            BuildDescriptionPanels(itemInfo, detailLevel);

            this.Log($"PrepareTooltip() - item: {itemInfo.nameString}", Logg.LoggingMode.Completed);
        }

        private void RebuildTooltipLayout()
        {
            Canvas.ForceUpdateCanvases();
            SyncDescPanelsHeight();
            SyncBodyHeight();

            if (bodyRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRect);

            if (transform is RectTransform rectTransform)
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        private void SyncDescPanelsHeight()
        {
            for (int i = 0; i < descPanels.Count; i++)
            {
                var panel = descPanels[i];
                if (panel == null)
                    continue;

                panel.RefreshHeight();

                var panelRect = panel.PanelRect;
                var contentRect = panel.ContentRect;
                var layoutElement = panel.LayoutElement;
                float panelHeight = panelRect != null ? panelRect.rect.height : -1f;
                float contentHeight = contentRect != null ? contentRect.rect.height : -1f;
                float minHeight = layoutElement != null ? layoutElement.minHeight : -1f;
                float preferredHeight = layoutElement != null ? layoutElement.preferredHeight : -1f;
                this.Log($"SyncDescPanelsHeight() - index: {i} panel: {panelHeight} content: {contentHeight} min: {minHeight} pref: {preferredHeight}", Logg.LoggingMode.Completed);
            }
        }

        private void SyncBodyHeight()
        {
            if (bodyRect == null || descParent == null)
                return;

            if (descParent is not RectTransform panelsRect)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelsRect);
            bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelsRect.rect.height);
        }

        private void BuildDescriptionPanels(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            if (descParent == null || descPanelTemplate == null)
            {
                Logg.LogError("[ItemTooltip] descParent or descPanelTemplate is null");
                return;
            }

            ClearDescPanels();

            if (itemInfo == null)
            {
                this.LogWarning("BuildDescriptionPanels - invalid itemInfo");
                return;
            }

            var sections = ItemTooltipContentBuilder.BuildDescriptionSections(itemInfo, detailLevel);
            foreach (var section in sections)
            {
                AddDescPanel(section);
            }
        }

        private void AddDescPanel(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || descPanelPool == null)
                return;

            if (descPanelPool.Get() is not ItemTooltipDescPanel panel)
                return;

            panel.SetText(content, false);
            descPanels.Add(panel);
        }

        private void ClearDescPanels()
        {
            for (int i = 0; i < descPanels.Count; i++)
            {
                if (descPanels[i] != null && descPanelPool != null)
                    descPanelPool.Release(descPanels[i]);
            }
            descPanels.Clear();
        }

        private void ClampToScreen(Vector2 screenPos)
        {
            screenClamper?.ClampToScreen(screenPos);
        }

        private void OnDestroy()
        {
            ClearDescPanels();
            descPanelPool?.Clear();
            descPanelPool = null;
        }
    }
}
