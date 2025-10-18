using System;
using RPG.UI;
using TH.Item;
using TH.Resource;
using TH.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TH.UI
{
    public class InventoryUI : PopupUI, IPlayerInventoryUI
    {
        [Header("InventoryUI")]
        [SerializeField] private PlayerStorageUI storageUI;
        [SerializeField] private PlayerEquipmentUI equipmentUI;

        public IPlayerStorageUI StorageUI => storageUI;
        public IEquipmentHolderUI EquipmentUI => equipmentUI;
        public event Action<DragSlotInfo> OnDragDrop;
        public event Action OnExitUICalled;

        // drag
        private bool isDragging = false;
        private IDraggableStorageUI beginDragUI; // 드래그가 시작된 슬롯의 소속
        private int beginDragIdx; // 드래그가 시작된 슬롯의 인덱스

        // itemTooltip
        // private UI_ItemTooltip itemTooltip;
        
        #region Enum

        enum GameObjects
        {
            Contents,
            
            InventoryArea,
            ItemSlots,
            DragDropIconHolder,
            
            PopupArea,
        }

        enum Buttons
        {
            ExitButton,
            SortButton,
            CompressButton,
            
            AllFilterButton,
            EquipmentFilterButton,
            ConsumableFilterButton,
            ResourceFilterButton,
        }

        #endregion
        
        protected override void Awake()
        {
            base.Awake();

            EnsureStorageUI();
            EnsureEquipmentUI();
            
            BindObject(typeof(GameObjects));
            BindButton(typeof(Buttons));
            BindButtonEvents();
            
            // LoadTooltip();
        }
        
        private void Start()
        {
            SubscribeDragEvents();
        }

        #region Initialization

        private void EnsureStorageUI()
        {
            if (storageUI != null) return;
            if (Util.FindChild(gameObject, "InventoryArea", true) is { } found
                && found.TryGetComponent(out IPlayerStorageUI pStorageUI))
            {
                storageUI = (PlayerStorageUI)pStorageUI;
            }
        }
        
        private void EnsureEquipmentUI()
        {
            if (equipmentUI != null) return;
            if (Util.FindChild(gameObject, "EquipmentArea", true) is { } found
                && found.TryGetComponent(out IEquipmentHolderUI pEquipmentUI))
            {
                equipmentUI = (PlayerEquipmentUI)pEquipmentUI;
            }
        }
        
        // private void LoadTooltip()
        // {
        //     // 아이템 툴팁 UI 로드
        //     if (ResourceManager.Instance.Instantiate("UI_ItemTooltip.prefab", transform) is { } tooltipObj)
        //     {
        //         itemTooltip = tooltipObj.GetComponent<UI_ItemTooltip>();
        //         itemTooltip.HideTooltip();
        //     }
        // }

        #endregion
        
        #region Handle Drag

        private void SubscribeDragEvents()
        {
            SubscribeDragBeginEvent(storageUI);
            SubscribeDragBeginEvent(equipmentUI);
            SubscribeDragEndEvent(storageUI);
            SubscribeDragEndEvent(equipmentUI);
        }

        private void SubscribeDragBeginEvent(IDraggableStorageUI sourceUI)
        {
            sourceUI.OnSlotDragged += (index) => { OnDragBegin(sourceUI, index); };
        }
        
        private void SubscribeDragEndEvent(IDraggableStorageUI sourceUI)
        {
            sourceUI.OffSlotDragged += (index) => { OnDragEnd(sourceUI, index); };
        }
        
        private void OnDragBegin(IDraggableStorageUI sourceUI, int index)
        {
            if (isDragging) return; // 이미 드래그 중인 경우 무시
            isDragging = true;
            beginDragUI = sourceUI;
            beginDragIdx = index;
        }

        private void OnDragEnd(IDraggableStorageUI sourceUI, int index)
        {
            if (!isDragging) return; // 드래그 중이 아닐 경우 중지
            if (beginDragUI == null) return; // 드래그 시작 슬롯 데이터가 유효하지 않으면 중지
            
            OnDragDrop?.Invoke(new DragSlotInfo(beginDragUI, beginDragIdx, sourceUI, index)); // 드래그 발생 이벤트 호출
            ClearDragState(); // 드래그 플래그 갱신
        }

        private void ClearDragState()
        {
            isDragging = false;
            beginDragUI = null;
            beginDragIdx = default;
        }

        #endregion

        #region Simple Item Tooltip

        // public void MoveTooltip(Vector2 pos)
        // {
        //     itemTooltip.MoveTooltip(pos);
        // }
        //
        // public void ShowTooltip(IGameItemSlot slot)
        // {
        //     itemTooltip.ShowTooltip(slot);
        // }
        //
        // public void HideTooltip()
        // {
        //     itemTooltip.HideTooltip();
        // }

        #endregion

        #region Bind Button Event

        private void BindButtonEvents()
        {
            GetButton((int)Buttons.ExitButton).onClick.AddListener(() => {OnExitUICalled?.Invoke();});
        }
        
        

        #endregion
        
        protected override void Clear()
        {
            base.Clear();
            ClearDragState();
        }

        public override void OnPopupClosed()
        {
            base.OnPopupClosed();
            Clear();
        }
    }
}

