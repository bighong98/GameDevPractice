using TH.Utils;
using TMPro;
using UnityEngine;

public class PlayerStatusPanelStatEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;

    public TMP_Text NameText => nameText;
    public TMP_Text ValueText => valueText;

    public void SetName(string text)
    {
        if (nameText != null)
            nameText.SetText(text);
    }

    public void SetValue(string text)
    {
        if (valueText != null)
            valueText.SetText(text);
    }
}
