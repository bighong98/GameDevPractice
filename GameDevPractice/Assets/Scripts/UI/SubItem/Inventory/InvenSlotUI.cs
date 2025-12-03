using TH.UI;
using TH.Utils;
using TMPro;
using UnityEngine;

public class InvenSlotUI : BaseSlotUI, IInvenSlotUI
{
    #region Enums

    enum TMPTexts
    {
        ItemAmountText,
    }

    #endregion
    
    protected override void Awake()
    {
        base.Awake();
        BindTMPText(typeof(TMPTexts));
    }

    public void SetAmount(int amount)
    {
        if (GetTMPText((int)TMPTexts.ItemAmountText) is not {} t)
        {
            Logg.LogError($"[{gameObject.name}] InvenSlotUI failed to find {TMPTexts.ItemAmountText.ToString()}");
            return;
        }

        if (amount <= 1)
        {
            t.enabled = false;
            return;
        }
        
        t.SetText(amount.ToString());
        t.enabled = true;
    }

    protected override void HideIcon()
    {
        base.HideIcon();
        GetTMPText((int)TMPTexts.ItemAmountText).enabled = false;
    }

    public void Highlight(int type)
    {
        base.Highlight();
    }

    public void UnHighlight(int type)
    {
        base.UnHighlight();
    }

    public override void Clear()
    {
        base.Clear();
        if (GetTMPText((int)TMPTexts.ItemAmountText) is {} t)
            t.enabled = false;
    }
}
