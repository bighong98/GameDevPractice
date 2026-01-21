using TH.Core;
using TH.Core.Service;
using TH.Item;
using TH.Item.Storage;
using TH.Resource;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.InputSystem;


// PlayerQuickStorage(퀵슬롯 바인딩 모델)와 QuickSlotPanelUI(뷰) 중계
// PlayerStorage(사용자 소지 아이템 모델) <-> PlayerQuickStorage 중계
public class QuickSlotController : MonoBehaviour
{
    private const string ItemTooltipPrefabKey = "UI_ItemTooltip.prefab";
    private const string QuickSlotActionMapName = "QuickSlot";
    private static readonly string[] QuickSlotActionNames =
    {
        "QuickSlot1",
        "QuickSlot2",
        "QuickSlot3",
        "QuickSlot4",
        "QuickSlot5",
    };
    [SerializeField] private QuickSlotPanelUI panelUI;
    
    // 연결된 저장소 (Model)
    private IPlayerStorage playerStorage;
    private IEquipmentHolder equipmentHolder;
    private IQuickStorage quickStorage;
    
    // 외부 서비스     
    private IPlayerHolder playerHolder;
    private string[] quickSlotBindingIds;

    private void Awake()
    {
        playerStorage = ServiceLocator.Get<IPlayerStorage>();
        quickStorage  = ServiceLocator.Get<IQuickStorage>();
        playerHolder = ServiceLocator.Get<IPlayerHolder>();

        playerHolder.OnPlayerInstanceUpdated += UpdatePlayerInstance;
        var currentPlayer = playerHolder.GetPlayerInstance;
        if (currentPlayer != null)
            UpdatePlayerInstance(currentPlayer);
        if (panelUI == null)
            TryGetComponent(out panelUI);

        InitializeEventRegistries();
        BindStorageEvents(playerStorage);
        
BindStorageUIEvents(panelUI);
        CacheQuickSlotBindingIds();
    }

    private void Start()
    {
        RefreshAllSlots();
        RefreshQuickSlotKeyLabels();
    }

    private void OnEnable()
    {
        if (quickStorage != null)
        {
            quickStorage.OnSlotChanged -= HandleQuickSlotChanged;
            quickStorage.OnStorageChanged -= HandleQuickStorageChanged;
            quickStorage.OnSlotChanged += HandleQuickSlotChanged;
            quickStorage.OnStorageChanged += HandleQuickStorageChanged;
        }

        if (playerStorage != null)
        {
            playerStorage.OnStorageChanged -= HandleInventoryStorageChanged;
            playerStorage.OnStorageChanged += HandleInventoryStorageChanged;
        }

        if (equipmentHolder != null)
        {
            equipmentHolder.OnSlotChanged -= HandleEquipmentSlotChanged;
            equipmentHolder.OnSlotChanged += HandleEquipmentSlotChanged;
        }

        SubscribeInputEvents();
        RefreshQuickSlotKeyLabels();
    }

    private void OnDisable()
    {
        if (quickStorage != null)
        {
            quickStorage.OnSlotChanged -= HandleQuickSlotChanged;
            quickStorage.OnStorageChanged -= HandleQuickStorageChanged;
        }

        if (playerStorage != null)
        {
            playerStorage.OnStorageChanged -= HandleInventoryStorageChanged;
        }

        if (equipmentHolder != null)
        {
            equipmentHolder.OnSlotChanged -= HandleEquipmentSlotChanged;
        }

        
        UIManager.Instance.ReleaseUI(ItemTooltipPrefabKey);
UnsubscribeInputEvents();
    }

    private void OnDestroy()
    {
        OnDisable();

        _hoverEnterRegistry.Clear();
        _hoverExitRegistry.Clear();
        _clickRegistry.Clear();

        

        UIManager.Instance.ReleaseUI(ItemTooltipPrefabKey);
UnBindStorageEvents(playerStorage);
    }

    #region Initialization

    private void InitializeEventRegistries()
    {
        _hoverEnterRegistry = new EventHandlerRegistry<IHoverableStorageUI, int>(
            adder: (ui, handler) => ui.OnSlotHovered += handler,
            remover: (ui, handler) => ui.OnSlotHovered -= handler
        );
        _hoverExitRegistry = new EventHandlerRegistry<IHoverableStorageUI, int>(
            adder: (ui, handler) => ui.OffSlotHovered += handler,
            remover: (ui, handler) => ui.OffSlotHovered -= handler
        );
        _clickRegistry = new EventHandlerRegistry<IClickableStorageUI, int>(
            adder: (ui, handler) => ui.OnSlotClicked += handler,
            remover: (ui, handler) => ui.OnSlotClicked -= handler
        );
    
    }

    private void BindStorageUIEvents(IStorageUI storageUI)
    {
        if (storageUI is IHoverableStorageUI hStorageUI)
        {
            SubscribeHoverEnterEvent(hStorageUI);
            SubscribeHoverExitEvent(hStorageUI);
        }
        if (storageUI is IClickableStorageUI cStorageUI)
            SubscribeClickEvent(cStorageUI);
    }

    private void BindStorageEvents(IGameItemStorage storage)
    {
        if (storage is ICountableItemStorage cStorage)
        {
            cStorage.OnCountableAmountModified -= HandleInventoryCountableAmountChanged;
            cStorage.OnCountableAmountModified += HandleInventoryCountableAmountChanged;
        }
    }

    private void UnBindStorageEvents(IGameItemStorage storage)
    {
        if (storage is ICountableItemStorage cStorage)
            cStorage.OnCountableAmountModified -= HandleInventoryCountableAmountChanged;
    }

    private void SubscribeInputEvents()
    {
        if (Util.IsQuitting) return;
        
        UnsubscribeInputEvents();

        InputManager.Instance.OnQuickSlot1Pressed += OnQuickSlot1Input;
        InputManager.Instance.OnQuickSlot2Pressed += OnQuickSlot2Input;
        InputManager.Instance.OnQuickSlot3Pressed += OnQuickSlot3Input;
        InputManager.Instance.OnQuickSlot4Pressed += OnQuickSlot4Input;
        InputManager.Instance.OnQuickSlot5Pressed += OnQuickSlot5Input;

        InputManager.Instance.OnRebindCompleted += HandleRebindCompleted;
        InputManager.Instance.OnRebindCanceled += HandleRebindCanceled;
    }

    private void UnsubscribeInputEvents()
    {
        if (Util.IsQuitting) return;
        
        InputManager.Instance.OnQuickSlot1Pressed -= OnQuickSlot1Input;
        InputManager.Instance.OnQuickSlot2Pressed -= OnQuickSlot2Input;
        InputManager.Instance.OnQuickSlot3Pressed -= OnQuickSlot3Input;
        InputManager.Instance.OnQuickSlot4Pressed -= OnQuickSlot4Input;
        InputManager.Instance.OnQuickSlot5Pressed -= OnQuickSlot5Input;

        InputManager.Instance.OnRebindCompleted -= HandleRebindCompleted;
        InputManager.Instance.OnRebindCanceled -= HandleRebindCanceled;
    }


    private void OnQuickSlot1Input() => UseQuickSlot(0);
    private void OnQuickSlot2Input() => UseQuickSlot(1);
    private void OnQuickSlot3Input() => UseQuickSlot(2);
    private void OnQuickSlot4Input() => UseQuickSlot(3);
    private void OnQuickSlot5Input() => UseQuickSlot(4);

    #endregion

    #region Public API

    // 특정 퀵슬롯을 사용 (버튼 클릭/단축키로 사용)
    const int useAmount = 1;
    public void UseQuickSlot(int quickIndex)
    {
        if (quickStorage == null || playerStorage == null) return;
        if (!IsValidQuickIndex(quickIndex)) return;

        if (!quickStorage.TryGetItem(quickIndex, out var item)
            || item is not { IsValid: true, GetItemInfo: {} itemInfo })
            return;

        bool used = TryUseQuickSlotItem(itemInfo);
        if (used)
            DrawSlot(quickIndex);
    }

    #endregion

    #region Un/Subscribe Event

    // Input Events (UI - View)
    private EventHandlerRegistry<IHoverableStorageUI, int> _hoverEnterRegistry;
    private EventHandlerRegistry<IHoverableStorageUI, int> _hoverExitRegistry;
    private EventHandlerRegistry<IClickableStorageUI, int> _clickRegistry;

    private void SubscribeHoverEnterEvent(IHoverableStorageUI sourceUI)
    {
        _hoverEnterRegistry.Register(sourceUI, (index) => OnSlotHovered(sourceUI, index));
    }

    private void SubscribeHoverExitEvent(IHoverableStorageUI sourceUI)
    {
        _hoverExitRegistry.Register(sourceUI, (index) => OffSlotHovered(sourceUI, index));
    }

    private void SubscribeClickEvent(IClickableStorageUI sourceUI)
    {
        _clickRegistry.Register(sourceUI, (index) => OnSlotClicked(sourceUI, index));
    }


    #endregion

    #region Storage(Model) Event Handle

    private void HandleQuickSlotChanged(IGameItemSlot slot) => HandleQuickSlotChanged(slot.Index);
    private void HandleQuickSlotChanged(int index)
    {
        DrawSlot(index);
    }

    private void HandleQuickStorageChanged()
    {
        RefreshAllSlots();
    }

    private void HandleInventoryStorageChanged()
    {
        RefreshAllSlots();
    }

    private void HandleEquipmentSlotChanged(IGameItemSlot slot)
    {
        RefreshAllSlots();
    }


    private void HandleInventoryCountableAmountChanged(ItemTypeSO data, int amount)
    {
        foreach (var quickSlot in quickStorage.ItemSlots)
        {
            if (quickSlot is not { IsAccessible: true, IsValid: true, GetItemInfo: {} slotItemInfo}) continue;
            if (slotItemInfo == data)
            {
                int index = quickSlot.Index;
                DrawSlot(index, amount);
                panelUI.HighlightSlot(index, (int)SlotHighlightType.Modified);
                panelUI.UnHighlightSlotWithFade(index, (int)SlotHighlightType.Modified);
                break;
            }
        }
    }

    #endregion

    #region Handle Input Event (UI - Slot)
    int lastHighlightedSlotIndex;
    private void OnSlotHovered(IHoverableStorageUI targetUI, int index)
    {
        if (targetUI is not IHighlightableStorageUI highlightableUI) return;
        highlightableUI.UnHighlightSlot(lastHighlightedSlotIndex);

        IGameItem hoveredItem = null;
        if (quickStorage.TryGetItem(index, out var slotItem)
            && slotItem is { IsValid: true })
        {
            highlightableUI.HighlightSlot(index);
            lastHighlightedSlotIndex = index;
            hoveredItem = slotItem;
        }

        if (hoveredItem.IsNotNull())
        {
            UIManager.Instance.ShowUI<ItemTooltipUI>(ItemTooltipPrefabKey, UICanvas.FeedbackOverlay)
                ?.ShowTooltipAt(InputManager.Instance.PointerPos, hoveredItem);
        }
        else
        {
            UIManager.Instance.ReleaseUI(ItemTooltipPrefabKey);
        }

        Logg.Log($"[{GetType().Name}] OnSlotHovered({index}) invoked", Logg.LoggingMode.Completed);
    }

    private void OffSlotHovered(IHoverableStorageUI targetUI, int index)
    {
        if (targetUI is not IHighlightableStorageUI highlightableUI) return;
        if (lastHighlightedSlotIndex != index)
            highlightableUI.UnHighlightSlot(lastHighlightedSlotIndex);
        highlightableUI.UnHighlightSlot(index);
        lastHighlightedSlotIndex = -1;
        UIManager.Instance.ReleaseUI(ItemTooltipPrefabKey);
        Logg.Log($"[{GetType().Name}] OffSlotHovered({index}) invoked", Logg.LoggingMode.Completed);
    }

    private void OnSlotClicked(IClickableStorageUI targetUI, int index)
    {
        Logg.Log($"[{GetType().Name}] OnSlotClicked({index}) invoked", Logg.LoggingMode.Completed);
        UIManager.Instance.ReleaseUI(ItemTooltipPrefabKey);
        UseQuickSlot(index);
    }

    #endregion

    #region Draw UI 
    
    private void RefreshAllSlots()
    {
        if (quickStorage == null || panelUI == null) return;

        int capacity = quickStorage.Capacity;
        for (int i = 0; i < capacity; i++)
        {
            DrawSlot(i);
        }
    }

    private void DrawSlot(int quickIndex)
    {
        if (quickStorage == null || panelUI == null) return;
        if (!IsValidQuickIndex(quickIndex)) return;

        if (panelUI.GetSlotUI(quickIndex) is not {} slotUI || !slotUI.IsNotNull())
            return;

        if (!quickStorage.TryGetItem(quickIndex, out var slotItem)
            || slotItem is not {GetItemInfo: {} itemInfo}
            || !itemInfo.IsNotNull())
        {
            slotUI.Clear(); // 아이템 개수 텍스트 숨기기
            panelUI.UnHighlightEquippingSlot(quickIndex);
            return;
        }

        // 퀵슬롯 등록 아이템이 장비 아이템인 경우
        if (itemInfo.itemType == Enums.ItemType.Equipment)
        {
            if (!IsEquipmentAvailable(itemInfo))
            {
                slotUI.Clear(); // 아이템 개수 텍스트 숨기기
                slotUI.HideIcon(); // 아이콘 숨기기
                panelUI.UnHighlightEquippingSlot(quickIndex);
                return;
            }

            slotUI.Clear(); // 아이템 개수 텍스트 숨기기
            slotUI.SetIcon(itemInfo.sprite);
            
            // 장착 중인 장비 표시
            if (TryFindSlot(equipmentHolder, itemInfo, out _))
                panelUI.HighlightEquippingSlot(quickIndex);
            else
                panelUI.UnHighlightEquippingSlot(quickIndex);
            return;
        }

        if (!TryGetTotalAmount(itemInfo, out var totalAmount))
        {
            slotUI.Clear();
            panelUI.UnHighlightEquippingSlot(quickIndex);
            return;
        }

        Logg.Log($"[{GetType().Name}.DrawSlot({quickIndex})] SetAmount({totalAmount})", Logg.LoggingMode.Completed);
        slotUI.SetIcon(itemInfo.sprite);
        slotUI.SetAmount(totalAmount);
        panelUI.UnHighlightEquippingSlot(quickIndex);
    }

    private void DrawSlot(int quickIndex, int itemAmount)
    {
        if (quickStorage == null || panelUI == null) return;
        if (!IsValidQuickIndex(quickIndex)) return;

        if (panelUI.GetSlotUI(quickIndex) is not {} slotUI || !slotUI.IsNotNull())
            return;

        if (itemAmount <= 0)
        {
            slotUI.Clear();
            return;
        }

        slotUI.SetAmount(itemAmount);
    }

    #endregion

    #region Helper Methods

    private void RefreshQuickSlotKeyLabels()
    {
        if (panelUI == null || InputManager.Instance == null)
            return;

        if (quickSlotBindingIds == null || quickSlotBindingIds.Length == 0)
            CacheQuickSlotBindingIds();

        for (int i = 0; i < QuickSlotActionNames.Length; i++)
        {
            var bindingId = quickSlotBindingIds != null && i < quickSlotBindingIds.Length
                ? quickSlotBindingIds[i]
                : null;
            if (string.IsNullOrEmpty(bindingId))
                continue;

            if (InputManager.Instance.TryGetBindingDisplayString(
                    QuickSlotActionMapName,
                    QuickSlotActionNames[i],
                    bindingId,
                    out var displayString,
                    out _,
                    out _))
            {
                panelUI.SetSlotKeyText(i, displayString);
            }
        }
    }

    private void CacheQuickSlotBindingIds()
    {
        var map = InputManager.Instance.UserInput.asset.FindActionMap(QuickSlotActionMapName, false);
        if (map == null)
            return;

        quickSlotBindingIds = new string[QuickSlotActionNames.Length];
        for (int i = 0; i < QuickSlotActionNames.Length; i++)
        {
            var action = map.FindAction(QuickSlotActionNames[i], false);
            quickSlotBindingIds[i] = FindFirstRebindableBindingId(action);
        }
    }

    private static string FindFirstRebindableBindingId(InputAction action)
    {
        if (action == null)
            return null;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            var expectedControlType = action.expectedControlType;
            if (!string.IsNullOrEmpty(expectedControlType) &&
                !string.Equals(expectedControlType, "Button", System.StringComparison.OrdinalIgnoreCase))
                continue;

            return binding.id.ToString();
        }

        return null;
    }

    private void HandleRebindCompleted(InputManager.RebindResult result)
    {
        RefreshQuickSlotKeyLabels();
    }

    private void HandleRebindCanceled()
    {
        RefreshQuickSlotKeyLabels();
    }

    private bool IsValidQuickIndex(int index)
    {
        return quickStorage != null && index >= 0 && index < quickStorage.Capacity;
    }

    private bool TryGetTotalAmount(ItemTypeSO itemInfo, out int amount)
    {
        amount = -1; // -1 means failure
        return playerStorage is ICountableItemStorage cStorage 
            && cStorage.TryGetCountableAmount(itemInfo, out amount);
    }

    private bool IsEquipmentAvailable(ItemTypeSO itemInfo)
    {
        bool result = TryFindSlot(playerStorage, itemInfo, out _)
            || TryFindSlot(equipmentHolder, itemInfo, out _);
        
        if (!result)
            this.Log($"IsEquipmentAvailable(itemInfo: {itemInfo.nameString}) is false", Logg.LoggingMode.Completed);

        return result;
    }

    private bool TryUseQuickSlotItem(ItemTypeSO itemInfo)
    {
        if (playerStorage == null) return false;
        if (itemInfo == null) return false;

        return playerStorage is IUsableItemStorage usableStorage
            && usableStorage.TryUse(itemInfo, amount: useAmount);
    }


    private bool TryFindSlot(IGameItemStorage storage, ItemTypeSO itemInfo, out IGameItemSlot foundSlot)
    {
        foundSlot = null;
        if (storage == null || itemInfo == null) return false;

        foreach (var slot in storage.ItemSlots)
        {
            if (slot is { IsAccessible: true, HasItem: true, GetItemInfo: {} slotItemInfo }
                && slotItemInfo == itemInfo)
            {
                foundSlot = slot;
                return true;
            }
        }

        return false;
    }


    private void UpdatePlayerInstance(object player)
    {
        UpdateEquipmentHolder(player);
    }

    private void UpdateEquipmentHolder(object player)
    {
        var previousHolder = equipmentHolder;
        IEquipmentHolder newHolder = null;

        if (player is Component c && c.TryGetComponent(out IEquipmentHolder found))
            newHolder = found;

        if (previousHolder == newHolder) return;

        if (previousHolder != null)
            previousHolder.OnSlotChanged -= HandleEquipmentSlotChanged;

        equipmentHolder = newHolder;

        if (equipmentHolder != null)
        {
            equipmentHolder.OnSlotChanged -= HandleEquipmentSlotChanged;
            equipmentHolder.OnSlotChanged += HandleEquipmentSlotChanged;
        }
    }


    #endregion

}
