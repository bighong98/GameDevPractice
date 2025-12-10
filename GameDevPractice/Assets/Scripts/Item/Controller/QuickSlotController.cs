using System;
using System.Collections;
using TH.Combat;
using TH.Core.Service;
using TH.Item;
using TH.Item.Storage;
using TH.Resource;
using TH.UI;
using TH.Utils;
using UnityEngine;


// PlayerQuickStorage(퀵슬롯 바인딩 모델)와 QuickSlotPanelUI(뷰) 중계
// PlayerStorage(사용자 소지 아이템 모델) <-> PlayerQuickStorage 중계
public class QuickSlotController : MonoBehaviour
{
    [SerializeField] private QuickSlotPanelUI panelUI;
    // 연결된 저장소 (Model)
    private IPlayerStorage playerStorage;
    private IQuickStorage quickStorage;
    // 외부 서비스     
    private IGameItemConsumer itemConsumer;
    
    private InputManager inputManager;
    private IPlayerHolder playerHolder;

    
    private void SubscribeInputEvents()
    {
        if (Util.IsQuitting) return;
        
        UnsubscribeInputEvents();

        inputManager.OnQuickSlot1Pressed += OnQuickSlot1Input;
        inputManager.OnQuickSlot2Pressed += OnQuickSlot2Input;
        inputManager.OnQuickSlot3Pressed += OnQuickSlot3Input;
        inputManager.OnQuickSlot4Pressed += OnQuickSlot4Input;
        inputManager.OnQuickSlot5Pressed += OnQuickSlot5Input;
    }

    private void UnsubscribeInputEvents()
    {
        if (Util.IsQuitting) return;
        
        inputManager.OnQuickSlot1Pressed -= OnQuickSlot1Input;
        inputManager.OnQuickSlot2Pressed -= OnQuickSlot2Input;
        inputManager.OnQuickSlot3Pressed -= OnQuickSlot3Input;
        inputManager.OnQuickSlot4Pressed -= OnQuickSlot4Input;
        inputManager.OnQuickSlot5Pressed -= OnQuickSlot5Input;
    }


    private void OnQuickSlot1Input() => UseQuickSlot(0);
    private void OnQuickSlot2Input() => UseQuickSlot(1);
    private void OnQuickSlot3Input() => UseQuickSlot(2);
    private void OnQuickSlot4Input() => UseQuickSlot(3);
    private void OnQuickSlot5Input() => UseQuickSlot(4);


    
    private void Awake()
    {
        playerStorage = ServiceLocator.Get<IPlayerStorage>();
        quickStorage  = ServiceLocator.Get<IQuickStorage>();
        itemConsumer = ServiceLocator.Get<IGameItemConsumer>();
        playerHolder = ServiceLocator.Get<IPlayerHolder>();
        inputManager = InputManager.Instance;
        
        playerHolder.OnPlayerInstanceUpdated += UpdatePlayerInstance;
        if (panelUI == null)
            TryGetComponent(out panelUI);

        InitializeEventRegistries();
        BindStorageEvents(playerStorage);
        BindStorageUIEvents(panelUI);
    }

    private void Start()
    {
        RefreshAllSlots(); 
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

        SubscribeInputEvents();
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

        UnsubscribeInputEvents();
    }

    private void OnDestroy()
    {
        OnDisable();

        _hoverEnterRegistry.Clear();
        _hoverExitRegistry.Clear();
        _clickRegistry.Clear();

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

        if (!itemConsumer.TryConsume(playerStorage, itemInfo, playerInstance.Value, 1))
            return;

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
        if (quickStorage.TryGetItem(index, out var slotItem)
            && slotItem is {IsValid: true})
        {
            highlightableUI.HighlightSlot(index);
            lastHighlightedSlotIndex = index;
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
        Logg.Log($"[{GetType().Name}] OffSlotHovered({index}) invoked", Logg.LoggingMode.Completed);
    }

    private void OnSlotClicked(IClickableStorageUI targetUI, int index)
    {
        Logg.Log($"[{GetType().Name}] OnSlotClicked({index}) invoked", Logg.LoggingMode.Completed);

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

        if (panelUI.GetSlotUI(quickIndex) is not {} slotUI || !slotUI.IsAlive())
            return;

        if (!quickStorage.TryGetItem(quickIndex, out var slotItem)
            || slotItem is not {GetItemInfo: {} itemInfo}
            || !itemInfo.IsAlive()
            || !TryGetTotalAmount(itemInfo, out var totalAmount))
        {
            slotUI.Clear();
            return;
        }

        Logg.Log($"[{GetType().Name}.DrawSlot({quickIndex})] SetAmount({totalAmount})", Logg.LoggingMode.Completed);
        slotUI.SetIcon(itemInfo.sprite);
        slotUI.SetAmount(totalAmount);
    }

    private void DrawSlot(int quickIndex, int itemAmount)
    {
        if (quickStorage == null || panelUI == null) return;
        if (!IsValidQuickIndex(quickIndex)) return;

        if (panelUI.GetSlotUI(quickIndex) is not {} slotUI || !slotUI.IsAlive())
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

    // Handle Player Instance (maintain valid reference)
    private LazyValue<IHealable> playerInstance;
    
    private void UpdatePlayerInstance(object player)
    {
        playerInstance ??= new LazyValue<IHealable>(() => GetPlayerInstance(playerHolder.GetPlayerInstance));
        playerInstance.Value = GetPlayerInstance(player);
        Logg.Log($"[{GetType().Name}] playerInstance.Value: {playerInstance.Value}", Logg.LoggingMode.Completed);
    }

    private IHealable GetPlayerInstance(object player)
    {
        if (!player.IsAlive() || player is not Component c || !c.TryGetComponent<IHealable>(out var p))
        {
            Logg.LogError($"[{GetType().Name}] failed to Get Player Instance");
            return null;
        }
        return p;
    }

    #endregion

}
