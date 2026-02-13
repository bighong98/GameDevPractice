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
    /// <summary>
    /// 인벤토리 아이템 툴팁 UI 클래스.
    /// 마우스 호버/클릭 시 아이템 정보를 표시하는 간략 툴팁.
    /// 오브젝트 풀링을 지원하며 화면 경계 내 위치 조정 기능 포함.
    /// </summary>
    public sealed class ItemTooltipUI : BaseUI, IPoolObject
    {
        private const string ItemTooltipLabelMapKey = "ItemTooltipLabelMap";
        private const string CountableUsableFallbackLabel = "\uC18C\uBE44"; // 소비
        private const string CountableResourceFallbackLabel = "\uC7AC\uB8CC"; // 재료

        #region Enums

        /// <summary>TMP 텍스트 바인딩용 enum</summary>
        enum TMPTexts
        {
            ItemNameText,
            ItemTypeText,
        }

        #endregion

        /// <summary>설명 패널들의 부모 Transform</summary>
        [SerializeField] private Transform descParent;
        /// <summary>툴팁 본문 영역 RectTransform</summary>
        [SerializeField] private RectTransform bodyRect;
        /// <summary>설명 패널 프리팩 템플릿</summary>
        [SerializeField] private GameObject descPanelTemplate;
        
        /// <summary>설명 패널 오브젝트 풀</summary>
        private ObjectPool<IPoolObject> descPanelPool;
        /// <summary>마지막으로 표시한 아이템 정보 (캐싱용)</summary>
        private ItemTypeSO lastItemInfo;
        /// <summary>현재 활성화된 설명 패널 목록</summary>
        private readonly List<ItemTooltipDescPanel> descPanels = new();

        /// <summary>화면 경계 내 위치 조정 헬퍼</summary>
        private IScreenSpaceClamper screenClamper;
        private ItemTooltipLabelMapSO cachedLabelMap;
        private bool isItemTypeLabelCacheInitialized;
        private readonly Dictionary<string, string> itemTypeLabelCache = new(StringComparer.Ordinal);

        protected override void Awake()
        {
            base.Awake();
            Init();
        }

        #region Initialization

        /// <summary>
        /// UI 초기화.
        /// Canvas 설정, 화면 클램퍼 초기화, TMP 바인딩, 설명 패널 풀 생성.
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
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

        /// <summary>
        /// 레이아웃 대상 컴포넌트 캐싱.
        /// descParent와 bodyRect 참조를 확보.
        /// </summary>
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

        /// <summary>
        /// 설명 패널 오브젝트 풀 초기화.
        /// PoolManager를 통해 풀을 생성하고 Get 시 부모 설정 콜백 등록.
        /// </summary>
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

        /// <summary>툴팁 표시</summary>
        public void ShowTooltip() => gameObject.SetActive(true);
        /// <summary>툴팁 숨김</summary>
        public void HideTooltip() => gameObject.SetActive(false);

        /// <summary>
        /// 지정된 화면 좌표에 툴팁 표시.
        /// 콘텐츠 변경 시 레이아웃 재계산 후 화면 경계 내로 클램핑.
        /// </summary>
        /// <param name="screenPos">화면 좌표</param>
        /// <param name="item">표시할 아이템</param>
        /// <param name="detailLevel">상세 수준 (Brief/Detailed)</param>
        public void ShowTooltipAt(Vector2 screenPos, IGameItem item, TooltipDetailLevel detailLevel = TooltipDetailLevel.Brief)
        {
            ShowTooltip(item, detailLevel, out bool contentChanged);

            if (contentChanged && transform is RectTransform rectTransform)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            ClampToScreen(screenPos);
        }

        /// <summary>
        /// 툴팁을 지정 좌표로 이동 (Vector2).
        /// </summary>
        /// <param name="pos">목표 화면 좌표</param>
        public void MoveTooltip(Vector2 pos)
        {
            ClampToScreen(pos);
        }

        /// <summary>
        /// 툴팁을 지정 좌표로 이동 (Vector3).
        /// </summary>
        /// <param name="pos">목표 화면 좌표</param>
        public void MoveTooltip(Vector3 pos)
        {
            ClampToScreen(pos);
        }

        #endregion
        
        /// <summary>
        /// 툴팁 표시 내부 로직.
        /// 아이템 정보가 변경된 경우에만 콘텐츠 재구성.
        /// </summary>
        /// <param name="item">표시할 아이템</param>
        /// <param name="detailLevel">상세 수준</param>
        /// <param name="contentChanged">콘텐츠 변경 여부 출력</param>
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

        /// <summary>
        /// 툴팁 콘텐츠 준비.
        /// 아이템 이름, 타입 텍스트 설정 및 설명 패널 생성.
        /// </summary>
        /// <param name="itemInfo">아이템 정보 SO</param>
        /// <param name="detailLevel">상세 수준</param>
        private void PrepareTooltip(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            var labelMap = GetItemLabelMap();
            EnsureItemTypeLabelCache(labelMap);
            UpdateHeaderTexts(itemInfo);
            BuildDescriptionPanels(itemInfo, detailLevel, labelMap);

            this.Log($"PrepareTooltip() - item: {itemInfo.nameString}", Logg.LoggingMode.Completed);
        }

        private void UpdateHeaderTexts(ItemTypeSO itemInfo)
        {
            GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString ?? string.Empty);
            GetTMPText((int)TMPTexts.ItemTypeText)?.SetText(ResolveItemTypeLabel(itemInfo) ?? string.Empty);
        }

        private void EnsureItemTypeLabelCache(ItemTooltipLabelMapSO labelMap)
        {
            if (isItemTypeLabelCacheInitialized || labelMap == null)
                return;

            itemTypeLabelCache.Clear();
            CacheItemTypeLabel(labelMap, Enums.ItemType.Default, false, Enums.ItemType.Default.ToString());
            CacheItemTypeLabel(labelMap, Enums.ItemType.Equipment, false, Enums.ItemType.Equipment.ToString());
            CacheItemTypeLabel(labelMap, Enums.ItemType.Countable, true, CountableUsableFallbackLabel);
            CacheItemTypeLabel(labelMap, Enums.ItemType.Countable, false, CountableResourceFallbackLabel);
            CacheItemTypeLabel(labelMap, Enums.ItemType.Single, false, Enums.ItemType.Single.ToString());
            CacheItemTypeLabel(labelMap, Enums.ItemType.Special, false, Enums.ItemType.Special.ToString());
            isItemTypeLabelCacheInitialized = true;
        }

        private void CacheItemTypeLabel(ItemTooltipLabelMapSO labelMap, Enums.ItemType itemType, bool isUsable, string fallback)
        {
            string itemTypeKey = TooltipLabelKeys.ItemType(itemType, isUsable);
            string baseTypeKey = TooltipLabelKeys.ItemType(itemType);
            string resolved = labelMap.GetLabel(itemTypeKey, labelMap.GetLabel(baseTypeKey, fallback));
            itemTypeLabelCache[itemTypeKey] = resolved;
        }

        private string ResolveItemTypeLabel(ItemTypeSO itemInfo)
        {
            string itemTypeKey = TooltipLabelKeys.ItemType(itemInfo.itemType, itemInfo.isUsable);
            if (itemTypeLabelCache.TryGetValue(itemTypeKey, out var cachedLabel) && !string.IsNullOrWhiteSpace(cachedLabel))
                return cachedLabel;

            return itemInfo.itemType == Enums.ItemType.Countable
                ? (itemInfo.isUsable ? CountableUsableFallbackLabel : CountableResourceFallbackLabel)
                : itemInfo.itemType.ToString();
        }





        /// <summary>
        /// 툴팁 레이아웃 재구성.
        /// 캔버스 강제 업데이트 후 패널/본문 높이 동기화.
        /// </summary>
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

        /// <summary>
        /// 모든 설명 패널의 높이를 콘텐츠에 맞게 동기화.
        /// 각 패널의 RefreshHeight 호출.
        /// </summary>
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

        /// <summary>
        /// 본문 영역 높이를 설명 패널 합계에 맞게 조정.
        /// </summary>
        private void SyncBodyHeight()
        {
            if (bodyRect == null || descParent == null)
                return;

            if (descParent is not RectTransform panelsRect)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelsRect);
            bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelsRect.rect.height);
        }

        /// <summary>
        /// 설명 패널 생성.
        /// 기존 패널 정리 후 ItemTooltipContentBuilder로 섹션 생성.
        /// </summary>
        /// <param name="itemInfo">아이템 정보 SO</param>
        /// <param name="detailLevel">상세 수준</param>
        private void BuildDescriptionPanels(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel, ItemTooltipLabelMapSO labelMap)
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

            var sections = ItemTooltipContentBuilder.BuildDescriptionSections(itemInfo, detailLevel, labelMap);
            foreach (var section in sections)
            {
                AddDescPanel(section);
            }
        }

        private ItemTooltipLabelMapSO GetItemLabelMap()
        {
            if (cachedLabelMap != null)
                return cachedLabelMap;

            if (ResourceManager.Instance.TryLoad<ItemTooltipLabelMapSO>(ItemTooltipLabelMapKey, out var labelMap) && labelMap != null)
            {
                cachedLabelMap = labelMap;
                return cachedLabelMap;
            }

            this.LogWarning($"failed to load addressable key: {ItemTooltipLabelMapKey}", context: this);
            return null;
        }

        /// <summary>
        /// 풀에서 설명 패널을 가져와 텍스트 설정.
        /// </summary>
        /// <param name="content">패널에 표시할 텍스트</param>
        private void AddDescPanel(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || descPanelPool == null)
                return;

            if (descPanelPool.Get() is not ItemTooltipDescPanel panel)
                return;

            panel.SetText(content, false);
            descPanels.Add(panel);
        }

        /// <summary>
        /// 모든 설명 패널을 풀에 반환하고 목록 초기화.
        /// </summary>
        private void ClearDescPanels()
        {
            for (int i = 0; i < descPanels.Count; i++)
            {
                if (descPanels[i] != null && descPanelPool != null)
                    descPanelPool.Release(descPanels[i]);
            }
            descPanels.Clear();
        }

        /// <summary>
        /// 툴팁 위치를 화면 경계 내로 제한.
        /// </summary>
        /// <param name="screenPos">목표 화면 좌표</param>
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

        #region IPoolObject

        public GameObject Origin { get; set; }

        public void OnCreateFromPool()
        {
        }

        public void OnGetFromPool()
        {
        }

        public void OnReleaseFromPool()
        {
            HideTooltip();
        }

        public void OnDestroyFromPool()
        {
        }

        public void ReleaseSelf()
        {
            HideTooltip();
        }

        #endregion
    }
}
