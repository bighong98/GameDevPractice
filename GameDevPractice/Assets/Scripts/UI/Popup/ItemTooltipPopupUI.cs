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

public class ItemTooltipPopupUI : PopupUI
{
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

    [SerializeField] private Transform descParent;
    [SerializeField] private RectTransform bodyRect;
    [SerializeField] private GameObject descPanelTemplate;

    private RectTransform itemImagePanelRect;
    private float itemImagePanelMinHeight = -1f;

    private ObjectPool<IPoolObject> descPanelPool;
    private ItemTypeSO lastItemInfo;
    private readonly List<ItemTooltipDescPanel> descPanels = new();

    private ISliderUIControllerInteger sliderController;
    private Action divideButtonAction;
    private Action<int> cachedDivideButtonAction;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

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

    private void PrepareTooltip(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
    {
        GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString ?? string.Empty);

        BuildDescriptionPanels(itemInfo, detailLevel);

        this.Log($"PrepareTooltip() - item: {itemInfo.nameString}", Logg.LoggingMode.Completed);
    }

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


    private void UpdateRootHeight()
    {
        if (transform is not RectTransform rootRect)
            return;

        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rootRect);
        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bounds.size.y);
    }


    private void BuildDescriptionPanels(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
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

    // 툴팁 하단 상호작용 버튼 세팅
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

    public override void OnPopupClosed()
    {
        ClearButtonListeners();
        CloseAllSubItems();
        base.OnPopupClosed();
    }

    private void CloseAllSubItems()
    {
        if (sliderController != null)
            sliderController.Hide();
    }

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

