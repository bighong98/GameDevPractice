using System;
using System.Collections.Generic;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Item;
using TH.Resource;
using TH.UI;
using TH.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using CountableItem = TH.Item.CountableItem;

namespace TH.UI
{
    public struct ButtonInfo
    {
        public string ButtonString;
        public Action ButtonAction;

        public ButtonInfo(string btnString, Action btnAction)
        {
            ButtonString = btnString;
            ButtonAction = btnAction;
        }
    }

    public struct ButtonInfo<T>
    {
        public string ButtonString;
        public Action<T> ButtonAction;

        public ButtonInfo(string btnString, Action<T> btnAction)
        {
            ButtonString = btnString;
            ButtonAction = btnAction;
        }
    }
}

/// <summary>
/// 아이템 상세 툴팁 팝업 UI.
/// 인벤토리에서 아이템 클릭 시 표시되는 상세 정보 팝업.
/// 삭제, 사용, 나누기 버튼 및 슬라이더 UI 포함.
/// </summary>
public class ItemTooltipPopupUI : PopupUI
{
    private const string ItemTooltipLabelMapKey = "ItemTooltipLabelMap";
    #region Enums

    enum Buttons
    {
        TooltipRemoveButton,
        TooltipUseButton,
        TooltipDivideButton,
    }

    enum TMPTexts
    {
        ItemNameText,
    }

    enum Images
    {
        ItemIconImage,
    }

    enum GameObjects
    {
        ItemImagePanel,
        ItemDivideSlider,
    }

    #endregion

    /// <summary>설명 패널들의 부모 Transform</summary>
    
    [SerializeField] private Transform descParent;
    /// <summary>툴팁 본문 영역 RectTransform</summary>
    
    [SerializeField] private RectTransform bodyRect;
    /// <summary>설명 패널 프리팩 템플릿</summary>
    
    [SerializeField] private GameObject descPanelTemplate;

    /// <summary>아이템 이미지 패널 RectTransform</summary>
    
    private RectTransform itemImagePanelRect;
    /// <summary>아이템 이미지 패널 최소 높이</summary>
    
    private float itemImagePanelMinHeight = -1f;

    /// <summary>설명 패널 오브젝트 풀</summary>
    
    private ObjectPool<IPoolObject> descPanelPool;
    /// <summary>마지막으로 표시한 아이템 정보 (캐싱용)</summary>
    
    private ItemTypeSO lastItemInfo;
    /// <summary>현재 활성화된 설명 패널 목록</summary>
    
    private readonly List<ItemTooltipDescPanel> descPanels = new();
    private ItemTooltipLabelMapSO cachedLabelMap;

    /// <summary>나누기 개수 선택용 슬라이더 컨트롤러</summary>
    
    private ISliderUIControllerInteger sliderController;
    /// <summary>나누기 버튼 클릭 시 실행할 액션 (슬라이더 표시)</summary>
    
    private Action divideButtonAction;
    /// <summary>나누기 확정 시 실행할 캐싱된 액션</summary>
    
    private Action<int> cachedDivideButtonAction;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    /// <summary>
    /// UI 초기화.
    /// 컴포넌트 바인딩, 레이아웃 캐싱, 설명 패널 풀 및 슬라이더 초기화.
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    
    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindImage(typeof(Images));
        BindButton(typeof(Buttons));
        BindTMPText(typeof(TMPTexts));
        BindObject(typeof(GameObjects));

        CacheLayoutTargets();
        InitDescPanelPool();

        if (GetObject((int)GameObjects.ItemDivideSlider)
                is not { } divideButton
            || divideButton == null
            || !divideButton.TryGetComponent(out sliderController))
        {
            Logg.LogError($"[DetailedItemTooltipUI] failed to get reference divideSlider");
            return false;
        }

        sliderController.Hide();

        return true;
    }

    /// <summary>
    /// 레이아웃 대상 컴포넌트 캐싱.
    /// bodyRect와 itemImagePanelRect 참조 확보.
    /// </summary>
    
    private void CacheLayoutTargets()
    {
        if (bodyRect == null && transform is RectTransform rootRect)
        {
            var bodyTransform = rootRect.Find("Body");
            if (bodyTransform != null)
                bodyRect = bodyTransform as RectTransform;
        }

        if (itemImagePanelRect == null
            && GetObject((int)GameObjects.ItemImagePanel) is { } imagePanel
            && imagePanel.TryGetComponent(out RectTransform imageRect))
        {
            itemImagePanelRect = imageRect;
            if (itemImagePanelMinHeight <= 0f)
            {
                float initialHeight = imageRect.rect.height;
                if (initialHeight <= 0f)
                    initialHeight = imageRect.sizeDelta.y;
                itemImagePanelMinHeight = initialHeight;
            }
        }

        this.Log($"CacheLayoutTargets() - bodyRect: {(bodyRect != null)} imageRect: {(itemImagePanelRect != null)} minHeight: {itemImagePanelMinHeight}", Logg.LoggingMode.Completed);
    }

    /// <summary>
    /// 설명 패널 오브젝트 풀 초기화.
    /// PoolManager를 통해 풀 생성.
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

    /// <summary>
    /// 툴팁 콘텐츠 설정.
    /// 아이템 정보, 아이콘 및 버튼 액션 구성.
    /// </summary>
    /// <param name="item">표시할 아이템</param>
    /// <param name="removeButton">삭제 버튼 정보</param>
    /// <param name="useButton">사용 버튼 정보</param>
    /// <param name="divideButton">나누기 버튼 정보 (개수 콜백 포함)</param>
    
    public void SetTooltip(IGameItem item, ButtonInfo removeButton, ButtonInfo useButton, ButtonInfo<int> divideButton)
    {
        if (item is not { GetAmount: > 0, GetItemInfo: { } itemInfo }) return;

        if (GetImage((int)Images.ItemIconImage) is { } iconImage)
            iconImage.sprite = itemInfo.sprite;

        UpdateTooltipContent(itemInfo, TooltipDetailLevel.Detailed);

        SetTooltipButton(Buttons.TooltipRemoveButton, removeButton);
        SetTooltipButton(Buttons.TooltipUseButton, useButton);

        SetDivideAction(item, divideButton.ButtonAction);
        SetTooltipButton(Buttons.TooltipDivideButton, divideButton.ButtonAction == null ? null : divideButtonAction);
    }

    /// <summary>
    /// 툴팁 콘텐츠 업데이트.
    /// 아이템 정보가 변경된 경우에만 콘텐츠 재구성.
    /// </summary>
    /// <param name="itemInfo">아이템 정보 SO</param>
    /// <param name="detailLevel">상세 수준</param>
    
    private void UpdateTooltipContent(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
    {
        if (itemInfo == null)
        {
            this.LogWarning("UpdateTooltipContent - invalid itemInfo");
            return;
        }

        if (lastItemInfo != null && ReferenceEquals(lastItemInfo, itemInfo))
            return;

        lastItemInfo = itemInfo;
        PrepareTooltip(itemInfo, detailLevel);
        RebuildTooltipLayout();
    }

    /// <summary>
    /// 툴팁 콘텐츠 준비.
    /// 아이템 이름 텍스트 설정 및 설명 패널 생성.
    /// </summary>
    /// <param name="itemInfo">아이템 정보 SO</param>
    /// <param name="detailLevel">상세 수준</param>
    
    private void PrepareTooltip(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
    {
        GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString ?? string.Empty);
        var labelMap = GetItemLabelMap();
        BuildDescriptionPanels(itemInfo, detailLevel, labelMap);

        this.Log($"PrepareTooltip() - item: {itemInfo.nameString}", Logg.LoggingMode.Completed);
    }

    /// <summary>
    /// 툴팁 레이아웃 재구성.
    /// 캔버스 강제 업데이트 후 패널/본문/루트 높이 동기화.
    /// </summary>
    
    private void RebuildTooltipLayout()
    {
        this.Log("RebuildTooltipLayout() - start", Logg.LoggingMode.Completed);

        Canvas.ForceUpdateCanvases();
        SyncDescPanelsHeight();
        SyncBodyHeight();

        if (bodyRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRect);

        UpdateRootHeight();

        if (transform is RectTransform rectTransform)
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);

        this.Log("RebuildTooltipLayout() - end", Logg.LoggingMode.Completed);
    }

    /// <summary>
    /// 본문 영역 높이를 설명 패널과 이미지 패널 중 큰 값에 맞게 조정.
    /// </summary>
    
    private void SyncBodyHeight()
    {
        if (bodyRect == null || descParent == null)
            return;

        if (descParent is not RectTransform panelsRect)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelsRect);
        float panelsHeight = panelsRect.rect.height;

        if (itemImagePanelMinHeight <= 0f)
        {
            var imageRect = itemImagePanelRect;
            if (imageRect == null
                && GetObject((int)GameObjects.ItemImagePanel) is { } imagePanel
                && imagePanel.TryGetComponent(out RectTransform imageRectFound))
            {
                imageRect = imageRectFound;
                itemImagePanelRect = imageRectFound;
            }

            if (imageRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(imageRect);
                float initialHeight = imageRect.rect.height;
                if (initialHeight <= 0f)
                    initialHeight = imageRect.sizeDelta.y;
                itemImagePanelMinHeight = initialHeight;
            }
        }

        float targetHeight = panelsHeight;
        if (itemImagePanelMinHeight > 0f && panelsHeight < itemImagePanelMinHeight)
        {
            targetHeight = itemImagePanelMinHeight;
            AdjustDescPanelsHeight(panelsHeight, targetHeight);
            panelsRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        }

        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        var panelRect = itemImagePanelRect;
        if (panelRect == null
            && GetObject((int)GameObjects.ItemImagePanel) is { } panel
            && panel.TryGetComponent(out RectTransform panelRectFound))
        {
            panelRect = panelRectFound;
            itemImagePanelRect = panelRectFound;
        }

        if (panelRect != null)
        {
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        }

        this.Log($"SyncBodyHeight() - panels: {panelsHeight} minImage: {itemImagePanelMinHeight} target: {targetHeight}", Logg.LoggingMode.Completed);
    }

    /// <summary>
    /// 설명 패널들의 높이를 균등하게 분배하여 목표 높이에 맞춤.
    /// </summary>
    /// <param name="currentHeight">현재 합계 높이</param>
    /// <param name="targetHeight">목표 높이</param>
    
    private void AdjustDescPanelsHeight(float currentHeight, float targetHeight)
    {
        if (descPanels.Count == 0)
            return;

        float extra = targetHeight - currentHeight;
        if (extra <= 0f)
            return;

        float addPerPanel = extra / descPanels.Count;
        for (int i = 0; i < descPanels.Count; i++)
        {
            var panel = descPanels[i];
            if (panel == null)
                continue;

            var panelRect = panel.PanelRect;
            if (panelRect == null)
                continue;

            float newHeight = panelRect.rect.height + addPerPanel;
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);

            var layoutElement = panel.LayoutElement;
            if (layoutElement == null)
                layoutElement = panelRect.gameObject.AddComponent<LayoutElement>();

            layoutElement.minHeight = newHeight;
            layoutElement.preferredHeight = newHeight;
        }

        this.Log($"AdjustDescPanelsHeight() - current: {currentHeight} target: {targetHeight} extra: {extra} perPanel: {addPerPanel}", Logg.LoggingMode.Completed);
    }

    /// <summary>
    /// 모든 설명 패널의 높이를 콘텐츠에 맞게 동기화.
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
    /// 루트 RectTransform 높이를 자식 요소들의 경계에 맞게 조정.
    /// </summary>
    
    private void UpdateRootHeight()
    {
        if (transform is not RectTransform rootRect)
            return;

        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rootRect);
        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bounds.size.y);
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
            Logg.LogError("[ItemTooltipPopupUI] descParent or descPanelTemplate is null");
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

    private void SetTooltipButton(Buttons buttonType, ButtonInfo buttonInfo, Action afterButtonSelectedTask = null)
    {
        if (GetButton((int)buttonType) is not { } button) return;

        var buttonAction = buttonInfo.ButtonAction;
        var buttonString = buttonInfo.ButtonString;

        bool buttonEnabled = buttonAction != null;
        button.gameObject.SetActive(buttonEnabled);

        if (!buttonEnabled) return;

        if (!string.IsNullOrEmpty(buttonString) &&
            Util.FindChild<TextMeshProUGUI>(button.gameObject, "text", true) is { } btnStr)
        {
            btnStr.SetText(buttonString);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (PopupCTS?.Token.IsCancellationRequested ?? true) return;
            buttonAction?.Invoke();
            afterButtonSelectedTask?.Invoke();
        });
    }

    /// <summary>
    /// 툴팁 하단 상호작용 버튼 설정.
    /// </summary>
    /// <param name="buttonType">버튼 타입</param>
    /// <param name="buttonAction">버튼 클릭 액션</param>
    /// <param name="buttonSetTask">버튼 커스터마이지 액션</param>
    /// <param name="afterButtonSelectedTask">버튼 클릭 후 추가 액션</param>
    // buttonSetTask: 버튼 텍스트 등 상황별로 세팅이 필요한 경우 사용
    // afterButtonSelectedTask: 해당 버튼이 클릭된 후 추가적으로 해야할 작업이 있는 경우 사용
    private void SetTooltipButton(Buttons buttonType, Action buttonAction, Action<Button> buttonSetTask = null, Action afterButtonSelectedTask = null)
    {
        if (GetButton((int)buttonType) is not { } button) return;

        bool buttonEnabled = buttonAction != null;
        button.gameObject.SetActive(buttonEnabled);

        if (buttonEnabled)
        {
            buttonSetTask?.Invoke(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (PopupCTS?.Token.IsCancellationRequested ?? true) return;
                buttonAction?.Invoke();
                afterButtonSelectedTask?.Invoke();
            });
        }
    }

    /// <summary>
    /// 나누기 버튼 액션 설정.
    /// 슬라이더 표시 및 값 확정 콜백 등록.
    /// </summary>
    /// <param name="item">대상 아이템 (최대 개수 참조)</param>
    /// <param name="divideButtonAction">나누기 확정 시 콜백</param>
    
private void SetDivideAction(IGameItem item, Action<int> divideButtonAction)
    {
        if (item is not CountableItem { GetAmount: { } max and > 1 }) return;
        if (sliderController != null)
        {
            if (cachedDivideButtonAction != null)
                sliderController.OnSliderValueConfirmed -= cachedDivideButtonAction;
            sliderController.OnSliderValueConfirmed += divideButtonAction;
            cachedDivideButtonAction = divideButtonAction;
        }

        this.divideButtonAction = () =>
        {
            if (sliderController == null) return;
            if (PopupCTS?.IsCancellationRequested ?? true) return;

            sliderController.SetMinMax(1, max, Mathf.FloorToInt((1 + max) / 2.0f));
            sliderController.Show();
        };
    }

    /// <summary>
    /// 팝업 닫힘 시 호출.
    /// 버튼 리스너 정리 및 서브 아이템(슬라이더) 숨김.
    /// </summary>
    
public override void OnPopupClosed()
    {
        ClearButtonListeners();
        CloseAllSubItems();
        base.OnPopupClosed();
    }

    /// <summary>
    /// 서브 아이템 UI 모두 숨김 (슬라이더 등).
    /// </summary>
    
private void CloseAllSubItems()
    {
        if (sliderController != null)
            sliderController.Hide();
    }

    /// <summary>
    /// 모든 버튼의 onClick 리스너 정리.
    /// </summary>
    
private void ClearButtonListeners()
    {
        if (GetButton((int)Buttons.TooltipRemoveButton) is { } removeButton)
            removeButton.onClick.RemoveAllListeners();
        if (GetButton((int)Buttons.TooltipUseButton) is { } useButton)
            useButton.onClick.RemoveAllListeners();
        if (GetButton((int)Buttons.TooltipDivideButton) is { } divideButton)
            divideButton.onClick.RemoveAllListeners();
    }

    private void OnDestroy()
    {
        ClearDescPanels();
        descPanelPool?.Clear();
        descPanelPool = null;
    }
}

