using TH.UI;
using TH.Utils;

public class QuickSlotUI : BaseSlotUI, IHighlightableSlotUI
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
            Logg.LogError($"[{gameObject.name}] InvenSlotUI failed to find {TMPTexts.ItemAmountText}");
            return;
        }
        
        t.SetText(amount.ToString());
        t.enabled = true;
    }

    new public void HideIcon()
    {
        base.HideIcon();
        if (GetTMPText((int)TMPTexts.ItemAmountText) is {} t)
            t.enabled = false;
    }

    public override void Clear()
    {
        base.Clear();
        if (GetTMPText((int)TMPTexts.ItemAmountText) is {} t)
            t.enabled = false;
        UnHighlight();
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
