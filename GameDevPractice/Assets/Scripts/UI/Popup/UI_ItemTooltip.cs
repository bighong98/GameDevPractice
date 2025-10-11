using TH.Item;
using UnityEngine;
using TH.Utils;
using ItemSlot = RPG.Item.ItemSlot;

namespace RPG.UI
{
    //인벤토리 아이템 설명 툴팁용 스크립트
    public class UI_ItemTooltip : BaseUI
    {
        #region Enums

        enum TMPTexts
        {
            ItemNameText,
            ItemDescText,
        }

        #endregion

        protected override void Awake()
        {
            base.Awake();
            Init();
        }

        public override bool Init()
        {
            if (base.Init() == false)
                return false;

            BindTMPText(typeof(TMPTexts));

            return true;
        }

        public void ShowTooltip() => gameObject.SetActive(true);
        public void HideTooltip() => gameObject.SetActive(false);

        public void ShowTooltip(BaseItem item)
        {
            if (item == null)
            {
                Logg.Log("itemTooltip: itemData is null");
                return;
            }

            ShowTooltip();
            GetTMPText((int)TMPTexts.ItemNameText).text = item.ItemName;
            GetTMPText((int)TMPTexts.ItemDescText).text = item.ItemDesc;
        }

        public void ShowTooltip(ItemSlot item)
        {
            if (item == null || item.GetItemInfo == null)
            {
                Logg.LogError("itemTooltip: itemData is null");
                return;
            }

            ShowTooltip();
            GetTMPText((int)TMPTexts.ItemNameText).SetText(item.GetItemInfo.name);
            GetTMPText((int)TMPTexts.ItemDescText).SetText(item.GetItemInfo.desc);
            // GetTMPText((int)TMPTexts.ItemNameText).text = item.GetItemInfo.name;
            // GetTMPText((int)TMPTexts.ItemDescText).text = item.GetItemInfo.desc;
        }
        
        public void ShowTooltip(IGameItemSlot item)
        {
            if (item == null || item.GetItemInfo == null)
            {
                Logg.LogError("itemTooltip: itemData is null");
                return;
            }

            ShowTooltip();
            GetTMPText((int)TMPTexts.ItemNameText).SetText(item.GetItemInfo.name);
            GetTMPText((int)TMPTexts.ItemDescText).SetText(item.GetItemInfo.desc);
            // GetTMPText((int)TMPTexts.ItemNameText).text = item.GetItemInfo.name;
            // GetTMPText((int)TMPTexts.ItemDescText).text = item.GetItemInfo.desc;
        }

        public void ShowTooltip(string itemName, string itemDesc)
        {
            ShowTooltip();
            GetTMPText((int)TMPTexts.ItemNameText).SetText(itemName);
            GetTMPText((int)TMPTexts.ItemDescText).SetText(itemDesc);
        }

        public void MoveTooltip(Vector3 pos)
        {
            transform.position = pos;
        }
    }
}
