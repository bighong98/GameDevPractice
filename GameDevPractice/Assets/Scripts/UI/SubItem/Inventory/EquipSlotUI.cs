using TH.UI;
using UnityEngine;

public class EquipSlotUI : BaseSlotUI, IEquipmentSlotUI
{
    private Color _originalHighlightColor;
    private static readonly Color WarningHighlightColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);

    protected override void Awake()
    {
        base.Awake();
        _originalHighlightColor = GetImage((int)Images.HighLightImage).color;
    }

    public override void Highlight()
    {
        GetImage((int)Images.HighLightImage).color = _originalHighlightColor;
        base.Highlight();
    }
    
    public void Highlight(int type)
    {
        GetImage((int)Images.HighLightImage).color = WarningHighlightColor;
        Highlight();
    }

    public void UnHighlight(int type)
    {
        UnHighlight();
    }
}
