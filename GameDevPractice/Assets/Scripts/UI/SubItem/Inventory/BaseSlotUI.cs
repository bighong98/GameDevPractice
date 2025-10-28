using RPG.UI;
using TH.Core.Pool;using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public abstract class BaseSlotUI : BaseUI, ISlotUI, IPoolObject
{
    #region Enums
        
    protected enum Images
    {
        SlotImage,
        ItemImage,
        HighLightImage,
    }
    
    #endregion
    
    [SerializeField] protected int index; // 슬롯UI 인덱스 (인벤토리 슬롯 데이터 배열과 동기화 필요)

    public int Index => index;
    public Image IconImage => GetImage((int)Images.ItemImage);
    public Transform IconRect => GetImage((int)Images.ItemImage).transform;

    protected override void Awake()
    {
        base.Awake();
        BindImage(typeof(Images));
    }

    public void SetIndex(int index)
    {
        this.index = index;
    }

    public void SetVisibility(bool state)
    {
        if (state && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        else if (!state && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void SetIcon(Sprite sprite)
    {
        if (sprite != null)
        {
            GetImage((int)Images.ItemImage).sprite = sprite;
            ShowIcon();
        }
        else
        {
            Logg.Log("UI_ItemSlotBase: itemSprite is null", Logg.LoggingMode.Completed);
            RemoveIcon();
        }
    }
    
    protected virtual void RemoveIcon() // 슬롯 아이템 제거
    {
        GetImage((int)Images.ItemImage).sprite = null;
        HideIcon();
    }

    public new virtual void Clear()
    {
        base.Clear();
        RemoveIcon();
    }
    
    protected virtual void ShowIcon() => GetImage((int)Images.ItemImage).enabled = true;
    protected virtual void HideIcon() => GetImage((int)Images.ItemImage).enabled = false;
    
    public virtual void Highlight() => GetImage((int)Images.HighLightImage).enabled = true;
    public virtual void UnHighlight() => GetImage((int)Images.HighLightImage).enabled = false;


    #region Object Pool Method/Property (IPoolObject)

    public GameObject Origin { get; set; }
    public void OnCreateFromPool() { }
    public void OnGetFromPool() { }
    public void OnReleaseFromPool() { }
    public void OnDestroyFromPool() { }
    public void ReleaseSelf() { }

    #endregion
    
}
