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

    public bool SetTooltip(Item item)
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
            bool isRemovable = itemInfo.itemType == Enums.ItemType.Special;
            removeButton.gameObject.SetActive(isRemovable);
            if (isRemovable)
            {
                //todo: onClick Action 추가
            }
        }

        if (GetButton((int)Buttons.TooltipUseButton) is { } useButton)
        {
            bool isUsable = itemInfo.isUsable;
            useButton.gameObject.SetActive(isUsable);
            if (isUsable)
            {
                if (Util.FindChild<TextMeshProUGUI>(useButton.gameObject, "text") is { } useButtonText)
                {
                    useButtonText.SetText(GetUseButtonText(itemInfo.itemType));
                }
                //todo: onClick Action 추가
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
}
