using System;
using UnityEngine;
using RPG.UI;
using RPG.Item;
using TMPro;

public class DetailedItemTooltipUI : PopupUI
{
    #region Enums
    
    enum Buttons
    {
        TooltipRemoveButton,
        TooltipUseButton,
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

    private void Awake()
    {
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

    public bool SetTooltip(Item item, Action removeAction = null, Action useAction = null)
    {
        if (item is not { GetAmount: > 0, GetItemInfo: { } itemInfo } ) return false;

        if (GetImage((int)Images.ItemIconImage) is {} iconImage)
        {
            iconImage.sprite = itemInfo.sprite;
        }
        GetTMPText((int)TMPTexts.ItemNameText)?.SetText(itemInfo.nameString);
        GetTMPText((int)TMPTexts.ItemDescText)?.SetText(itemInfo.desc);

        if (GetButton((int)Buttons.TooltipRemoveButton) is { } removeButton)
        {
            bool removeButtonEnabled = removeAction != null;
            removeButton.gameObject.SetActive(removeButtonEnabled);
            if (removeButtonEnabled)
            {
                removeButton.onClick.AddListener(() =>
                {
                    if (PopupCTS?.Token.IsCancellationRequested ?? true) return;
                    removeAction?.Invoke();
                });
            }
        }

        if (GetButton((int)Buttons.TooltipUseButton) is { } useButton)
        {
            bool useButtonEnabled = useAction != null;
            useButton.gameObject.SetActive(useButtonEnabled);
            if (useButtonEnabled)
            {
                if (Util.FindChild<TextMeshProUGUI>(useButton.gameObject, "text") is { } useButtonText)
                {
                    useButtonText.SetText(GetUseButtonText(itemInfo.itemType));
                }
                useButton.onClick.AddListener(() =>
                {
                    if (PopupCTS?.Token.IsCancellationRequested ?? true) return;
                    useAction?.Invoke();
                });
            }
        }
        
        return true;
    }

    private static string GetUseButtonText(Enums.ItemType itemType)
    {
        return itemType switch
        {
            Enums.ItemType.Equipment => DefaultEquipText,
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
