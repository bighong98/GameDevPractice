using TH.Core.Pool;
using TH.Core.Service;
using TH.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LoadSlotUI : BaseUI, IPoolObject
{
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Button button;
    public TextMeshProUGUI LabelText => labelText;
    public Button SlotBotton => button;

    protected override void Awake()
    {
        base.Awake();
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (labelText == null)
            labelText = Util.FindChild<TextMeshProUGUI>(gameObject, "Label");

        if (button == null)
            TryGetComponent(out button);
    }

    #region IPoolObject

    public GameObject Origin { get; set; }

    public void OnCreateFromPool()
    {
    }

    public void OnGetFromPool()
    {
        EnsureReferences();

        if (transform is RectTransform rectTransform)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.anchoredPosition3D = Vector3.zero;
        }
    }

    public void OnReleaseFromPool()
    {
        if (button != null)
            button.onClick.RemoveAllListeners();

        if (labelText != null)
            labelText.text = string.Empty;

        if (transform is RectTransform rectTransform)
            rectTransform.localScale = Vector3.one;
    }

    public void OnDestroyFromPool()
    {
    }

    public void ReleaseSelf()
    {
        if (!gameObject.activeSelf) return;
        PoolManager.Instance.ReleaseFromPool(this);
    }

    #endregion
}
