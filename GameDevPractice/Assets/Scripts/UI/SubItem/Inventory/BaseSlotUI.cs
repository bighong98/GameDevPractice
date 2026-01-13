using TH.UI;
using TH.Core.Pool;
using TH.Utils;
using UnityEngine;
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

    protected virtual void OnDisable()
    {
        UnHighlight();
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
            if (GetImage((int)Images.ItemImage) is {} img)
            {
                img.sprite = sprite;
                ShowIcon();
            }
        }
        else
        {
            Logg.Log("UI_ItemSlotBase: itemSprite is null", Logg.LoggingMode.Completed);
            RemoveIcon();
        }
    }
    
    protected virtual void RemoveIcon()
    {
        if (GetImage((int)Images.ItemImage) is {} img)
        {
            img.sprite = null;
            HideIcon();
        }
    }

    public new virtual void Clear()
    {
        base.Clear();
        RemoveIcon();
    }
    
    protected virtual void ShowIcon()
    {
        if (GetImage((int)Images.ItemImage) is {} img)
            img.enabled = true;
    }
    protected virtual void HideIcon()
    {
        if (GetImage((int)Images.ItemImage) is {} img)
            img.enabled = false;
    }
    
    public virtual void Highlight()
    {
        if (GetImage((int)Images.HighLightImage) is {} img)
            img.enabled = true;
    }
    public virtual void UnHighlight()
    {
        if (GetImage((int)Images.HighLightImage) is {} img)
            img.enabled = false;
    }


    #region Object Pool Method/Property (IPoolObject)

    public GameObject Origin { get; set; }
    public void OnCreateFromPool() { }
    public void OnGetFromPool()
    {
        // 오브젝트 풀에서 재사용될 때 UI 컴포넌트 재바인딩
        if (!_init)
        {
            BindImage(typeof(Images));
        }
    }
    public void OnReleaseFromPool() { }
    public void OnDestroyFromPool() { }
    public void ReleaseSelf() { }

    #endregion
    
}
