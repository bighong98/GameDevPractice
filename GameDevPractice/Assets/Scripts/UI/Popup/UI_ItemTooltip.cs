using RPG.Item;
using UnityEngine;
using TH.Utils;

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
            if (item == null)
            {
                Logg.Log("itemTooltip: itemData is null");
                return;
            }

            ShowTooltip();
            GetTMPText((int)TMPTexts.ItemNameText).text = item.GetItemInfo.name;
            GetTMPText((int)TMPTexts.ItemDescText).text = item.GetItemInfo.desc;
        }

        public void ShowTooltip(string itemName, string itemDesc)
        {
            ShowTooltip();
            GetTMPText((int)TMPTexts.ItemNameText).text = itemName;
            GetTMPText((int)TMPTexts.ItemDescText).text = itemDesc;
        }

        public void MoveTooltip(Vector3 pos)
        {
            transform.position = pos;
        }
    }
}
