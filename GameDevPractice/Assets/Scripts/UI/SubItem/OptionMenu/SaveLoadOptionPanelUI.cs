using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.SaveLoad;
using TH.UI;
using TH.Utils;
using UnityEngine;

public class SaveLoadOptionPanelUI : OptionPanelUIBase
{
    [SerializeField] private LoadSlotPanelEmbeddedUI loadSlotPanel;
    [SerializeField] private bool closeOptionMenuWhenLoad = true;

    private ISaveSystem saveSystem;
    private ISaveFileHandler saveFileHandler;
    private readonly LoadSlotConfirmModule loadSlotConfirmModule = new();
    private OptionMenuUI optionMenuUI;

    private void Awake()
    {
        EnsureReferences();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureReferences();

        if (loadSlotPanel == null)
            return;

        loadSlotPanel.SlotSelected -= HandleSlotSelected;
        loadSlotPanel.SlotSelected += HandleSlotSelected;
    }

    private void OnDisable()
    {
        if (loadSlotPanel != null)
            loadSlotPanel.SlotSelected -= HandleSlotSelected;

        loadSlotConfirmModule.CancelActiveRequest();
    }

    public void RefreshSlots()
    {
        EnsureReferences();

        if (loadSlotPanel == null)
        {
            Logg.LogWarning("[SaveLoadOptionPanelUI] loadSlotPanel is not set");
            return;
        }

        if (saveFileHandler == null)
        {
            Logg.LogWarning("[SaveLoadOptionPanelUI] ISaveFileHandler is not available");
            loadSlotPanel.ShowLoadSlots(null);
            return;
        }

        saveFileHandler.RefreshSaveFileList();
        var slots = BuildSlotViewData(saveFileHandler.SaveFiles);
        loadSlotPanel.ShowLoadSlots(slots);
    }

    protected override void SyncFromSettings()
    {
        RefreshSlots();
    }

    protected override void ResetToDefaults()
    {
    }

    private void EnsureReferences()
    {
        if (loadSlotPanel == null)
            loadSlotPanel = GetComponentInChildren<LoadSlotPanelEmbeddedUI>(true);

        saveSystem ??= ServiceLocator.Get<ISaveSystem>();
        saveFileHandler ??= ServiceLocator.Get<ISaveFileHandler>();
        optionMenuUI ??= GetComponentInParent<OptionMenuUI>(true);
    }

    private void HandleSlotSelected(string saveFile)
    {
        EnsureReferences();

        if (optionMenuUI != null && optionMenuUI.TryGetPopupToken(out var optionMenuToken))
        {
            loadSlotConfirmModule.Request(
                saveFile,
                confirmedSaveFile => LoadFromSlotAsync(confirmedSaveFile).Forget(),
                optionMenuToken);
            return;
        }

        loadSlotConfirmModule.Request(
            saveFile,
            confirmedSaveFile => LoadFromSlotAsync(confirmedSaveFile).Forget());
    }
    
    private async UniTask LoadFromSlotAsync(string saveFile)
    {
        EnsureReferences();

        if (saveSystem == null)
        {
            Logg.LogWarning("[SaveLoadOptionPanelUI] ISaveSystem is not available");
            return;
        }

        if (closeOptionMenuWhenLoad && optionMenuUI != null)
            optionMenuUI.ClosePopupUI();

        await saveSystem.LoadLastScene(saveFile);
    }

    private static List<SaveSlotViewData> BuildSlotViewData(IReadOnlyList<SaveFileInfo> saveFiles)
    {
        if (saveFiles == null || saveFiles.Count == 0)
            return new List<SaveSlotViewData>();

        var slots = new List<SaveSlotViewData>(saveFiles.Count);
        foreach (var saveInfo in saveFiles)
        {
            string label = $"{saveInfo.FileName}  {saveInfo.SaveDate:yyyy/MM/dd HH:mm}";
            slots.Add(new SaveSlotViewData(saveInfo.FileName, label));
        }

        return slots;
    }
}
