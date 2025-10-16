using TH.UI;
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
        GetTMPText((int)TMPTexts.ItemAmountText).SetText(amount.ToString());
    }

    public void Highlight(int type)
    {
        base.Highlight();
    }

    public void UnHighlight(int type)
    {
        base.UnHighlight();
    }
}
