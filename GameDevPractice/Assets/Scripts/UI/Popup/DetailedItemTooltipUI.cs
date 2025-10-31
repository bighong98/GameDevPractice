using System;
using UnityEngine;
using UnityEngine.UI;
using RPG.UI;
using RPG.Item;
using TH.Item;
using TH.UI;
using TH.Utils;
using TMPro;
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

public class DetailedItemTooltipUI : PopupUI
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
        ItemDescText,
    }

    enum Images
    {
        ItemIconImage,
    }

    enum GameObjects
    {
        ItemDivideSlider,
    }

    #endregion
    
    private ISliderUIControllerInteger sliderController;

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
    
    public void SetTooltip(IGameItem item, ButtonInfo removeButton, ButtonInfo useButton, ButtonInfo<int> divideButton)
    {
        if (item is not { GetAmount: > 0, GetItemInfo: { } itemInfo } ) return;
        
        if (GetImage((int)Images.ItemIconImage) is {} iconImage)
            iconImage.sprite = itemInfo.sprite;
        GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString);
        GetTMPText((int)TMPTexts.ItemDescText)?.SetText(itemInfo.desc);
        
        SetTooltipButton(Buttons.TooltipRemoveButton, removeButton);
        SetTooltipButton(Buttons.TooltipUseButton, useButton);
        
        SetDivideAction(item, divideButton.ButtonAction);
        SetTooltipButton(Buttons.TooltipDivideButton, divideButton.ButtonAction == null ? null : divideButtonAction);
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
            Util.FindChild<TextMeshProUGUI>(button.gameObject, "text", true) is {} btnStr)
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

    private Action divideButtonAction;
    private Action<int> cachedDivideButtonAction;

    private void SetDivideAction(IGameItem item, Action<int> divideButtonAction)
    {
        if (item is not CountableItem {GetAmount: {} max and > 1 } ) return;
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
            
            sliderController.SetMinMax(1, max, Mathf.FloorToInt((1+max)/2.0f));
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
        if (GetButton((int)Buttons.TooltipDivideButton) is {} divideButton)
            divideButton.onClick.RemoveAllListeners();
    }
}
