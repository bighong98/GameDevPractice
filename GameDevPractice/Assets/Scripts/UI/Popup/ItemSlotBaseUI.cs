using UnityEngine;
using TH.Core.Pool;

namespace RPG.UI
{
    // 모든 아이템 슬롯 UI 스크립트가 상속
    public abstract class ItemSlotBaseUI : BaseUI, IPoolObject
    {
        #region Enums
        
        protected enum Images
        {
            SlotImage,
            ItemImage,
            HighLightImage,
        }

        #endregion
        // serializeField for Debug
        [SerializeField] protected int _index; // 슬롯UI 인덱스 (인벤토리 슬롯 데이터 배열과 동기화 필요)
        [SerializeField] protected bool _isAccessibleSlot; // 해당 슬롯 접근 가능 여부 (클릭, 드래그 등 슬롯UI 상호작용 등)
        [SerializeField] protected bool _isAccessibleItem; // 해당 슬롯 내부 아이템 접근 가능 여부
        protected static readonly Color InAccessibleSlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        protected static readonly Color InAccessibleIconColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        
        public int Index => _index;
        public bool IsAccessibleSlot => _isAccessibleSlot;
        public bool IsAccessible => _isAccessibleSlot && _isAccessibleItem;
        public bool HasItem => GetImage((int)Images.ItemImage).sprite != null;
        public UnityEngine.UI.Image IconImage => GetImage((int)Images.ItemImage);
        public Transform IconRect => GetImage((int)Images.ItemImage).transform;
        
        public override bool Init()
        {
            if (base.Init() == false)
                return false;
            _isAccessibleSlot = false;
            _isAccessibleItem = true;
            
            BindImage(typeof(Images));

            return true;
        }

        public void SetSlotIndex(int index) => _index = index;
        
        #region Item Method // 아이템 관련 함수

        public virtual void RemoveIcon() // 슬롯 아이템 제거
        {
            GetImage((int)Images.ItemImage).sprite = null;
            HideIcon();
            // HideText();
        }

        public void SetIcon(Sprite itemSprite) // 슬롯에 아이템 등록
        {
            if (itemSprite != null)
            {
                GetImage((int)Images.ItemImage).sprite = itemSprite;
                ShowIcon();
            }
            else
            {
                Util.Log("UI_ItemSlotBase: itemSprite is null", Util.LoggingMode.Completed);
                RemoveIcon();
            }
        }
        
        public void SwapOrMoveIcon(ItemSlotBaseUI otherSlot)
        {
            if (otherSlot == null || otherSlot == this) return;
            if (!this.IsAccessible || !otherSlot.IsAccessible) return;

            var currIconImage = GetImage((int)Images.ItemImage).sprite;
            if (otherSlot.HasItem)
                SetIcon(otherSlot.GetImage((int)Images.ItemImage).sprite);
            else
                RemoveIcon(); // 이동을 위해 현재 슬롯의 이미지는 제거
            
            otherSlot.SetIcon(currIconImage); // 새 슬롯 위치로 아이템 이전
        }

        #endregion

        #region Icon Method // 아이템 이미지 관련 함수 

        private void ShowIcon() => GetImage((int)Images.ItemImage).enabled = true;
        private void HideIcon() => GetImage((int)Images.ItemImage).enabled = false;
        
        public void SetIconAlpha(float alpha) // 제대로 작동하는지 확인 필요함
        {
            GetImage((int)Images.ItemImage).CrossFadeAlpha(alpha, 0.1f, false);
        }
        #endregion

        #region Highlight Method // 하이라이트 이미지 관련 함수

        public void ShowHighlight() => GetImage((int)Images.HighLightImage).enabled = true;
        public virtual void HideHighlight() => GetImage((int)Images.HighLightImage).enabled = false;

        #endregion
        
        #region Access Method // 접근 가능여부 관련 함수

        public virtual void SetSlotAccessibleState(bool newState)
        {
            if (_isAccessibleSlot == newState) return;
            if (newState)
            {
                GetImage((int)Images.SlotImage).color = Color.white;
            }
            else
            {
                GetImage((int)Images.SlotImage).color = InAccessibleSlotColor;
            }

            _isAccessibleSlot = newState;
        }

        public virtual void SetItemAccessibleState(bool newState)
        {
            if (_isAccessibleItem == newState) return;
            if (newState)
            {
                GetImage((int)Images.ItemImage).color = Color.white;
                // GetTMPText((int)TMPTexts.ItemAmountText).color = Color.black;
            }
            else
            {
                GetImage((int)Images.ItemImage).color = InAccessibleIconColor;
                // GetTMPText((int)TMPTexts.ItemAmountText).color = InAccessibleIconColor;
            }

            _isAccessibleItem = newState;
        }

        #endregion

        #region Object Pooling Method

        public GameObject Origin { get; set; }
        // public PoolKey PoolKey { get; set; }

        public void OnCreateFromPool()
        {
            
        }

        public void OnGetFromPool()
        {
            
        }

        public void OnReleaseFromPool()
        {
            
        }

        public void OnDestroyFromPool()
        {
            
        }

        public void ReleaseSelf()
        {
            Util.LogError($"ReleaseSelf not supported type: {this.GetType().FullName}");
        }

        #endregion
    }
}


