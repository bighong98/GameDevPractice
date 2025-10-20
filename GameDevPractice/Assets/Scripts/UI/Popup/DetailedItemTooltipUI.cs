using System;
using UnityEngine;
using UnityEngine.UI;
using RPG.UI;
using RPG.Item;
using TH.Item;
using TH.UI;
using TMPro;

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

    #endregion

    private const string DefaultConsumeText = "사용";
    private const string DefaultEquipText = "장착";
    private const string DefaultUnEquipText = "장착해제";
    private const string DefaultDivideText = "개수 분리";

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

        return true;
    }

    public bool SetTooltip(Item item, ItemSlotBaseUI slotUI, Action removeAction = null, Action useAction = null, Action divideAction = null)
    {
        if (item is not { GetAmount: > 0, GetItemInfo: { } itemInfo } ) return false;

        if (GetImage((int)Images.ItemIconImage) is {} iconImage)
        {
            iconImage.sprite = itemInfo.sprite;
        }
        GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString);
        GetTMPText((int)TMPTexts.ItemDescText)?.SetText(itemInfo.desc);

        SetTooltipButton(Buttons.TooltipRemoveButton, removeAction);
        SetTooltipButton(Buttons.TooltipUseButton, useAction, buttonSetTask: (button) =>
        {
            if (Util.FindChild<TextMeshProUGUI>(button.gameObject, "text") is { } useButtonText)
            {
                useButtonText.SetText(GetUseButtonText(slotUI, itemInfo.itemType));
            }
        });
        SetTooltipButton(Buttons.TooltipDivideButton, divideAction);
        
        return true;
    }

    public void SetTooltip(IGameItem item, ButtonInfo removeButton, ButtonInfo useButton, ButtonInfo divideButton)
    {
        if (item is not { GetAmount: > 0, GetItemInfo: { } itemInfo } ) return;
        
        if (GetImage((int)Images.ItemIconImage) is {} iconImage)
            iconImage.sprite = itemInfo.sprite;
        GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString);
        GetTMPText((int)TMPTexts.ItemDescText)?.SetText(itemInfo.desc);
        
        SetTooltipButton(Buttons.TooltipRemoveButton, removeButton);
        SetTooltipButton(Buttons.TooltipUseButton, useButton);
        SetTooltipButton(Buttons.TooltipDivideButton, divideButton);
    }
    
    public bool SetTooltip(IGameItem item, ItemSlotBaseUI slotUI, Action removeAction = null, Action useAction = null, Action divideAction = null)
    {
        if (item is not { GetAmount: > 0, GetItemInfo: { } itemInfo } ) return false;

        if (GetImage((int)Images.ItemIconImage) is {} iconImage)
        {
            iconImage.sprite = itemInfo.sprite;
        }
        GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString);
        GetTMPText((int)TMPTexts.ItemDescText)?.SetText(itemInfo.desc);

        SetTooltipButton(Buttons.TooltipRemoveButton, removeAction);
        SetTooltipButton(Buttons.TooltipUseButton, useAction, buttonSetTask: (button) =>
        {
            if (Util.FindChild<TextMeshProUGUI>(button.gameObject, "text") is { } useButtonText)
            {
                useButtonText.SetText(GetUseButtonText(slotUI, itemInfo.itemType));
            }
        });
        SetTooltipButton(Buttons.TooltipDivideButton, divideAction);
        
        return true;
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
            button.onClick.AddListener(() =>
            {
                if (PopupCTS?.Token.IsCancellationRequested ?? true) return;
                buttonAction?.Invoke();
                afterButtonSelectedTask?.Invoke();
            });
        }
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
            
        button.onClick.AddListener(() =>
        {
            if (PopupCTS?.Token.IsCancellationRequested ?? true) return;
            buttonAction?.Invoke();
            afterButtonSelectedTask?.Invoke();
        });
    }

    private static string GetUseButtonText(ItemSlotBaseUI slotUI, Enums.ItemType itemType)
    {
        return itemType switch
        {
            Enums.ItemType.Equipment => slotUI is EquipmentSlotUI ? DefaultUnEquipText : DefaultEquipText,
            _ => DefaultConsumeText
        };
    }

    public override void OnPopupClosed()
    {
        ClearButtonListeners();
        base.OnPopupClosed();
    }

    private void ClearButtonListeners()
    {
        if (GetButton((int)Buttons.TooltipRemoveButton) is { } removeButton)
        {
            removeButton.onClick.RemoveAllListeners();
        }
        if (GetButton((int)Buttons.TooltipUseButton) is { } useButton)
        {
            useButton.onClick.RemoveAllListeners();
        }
    }
}
