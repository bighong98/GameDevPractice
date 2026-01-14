using System.Collections.Generic;
using TH.Core.Service;
using TH.UI;
using TH.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionMenuUI : PopupUI
{
    #region Enum
    
    enum Buttons
    {
        exitButton,
    }

    enum GameObjects
    {
        categoryPanelRect,
        optionPanelRect,
    }

    #endregion

    [SerializeField] private OptionCategoryPanelMapSO categoryPanelMap;

    private readonly Dictionary<Enums.OptionCategory, GameObject> categoryPanels = new();
    private Enums.OptionCategory? activeCategory;


    private const string OptionCategoryPanelMapSOKey = "OptionCategoryPanelMapSO";

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override bool Init()
    {
        if (base.Init() == false) return false;
        
        BindObject(typeof(GameObjects));
        BindButton(typeof(Buttons));
        GetButton((int)Buttons.exitButton).onClick.AddListener(ClosePopupUI);
        BuildCategoryUI();
        
        return true;
    }

    public override void OnGetFromPool()
    {
        base.OnGetFromPool();
    }

    public override void OnPopupClosed()
    {
        PlayerPrefs.Save(); // ì°??«ì„ ??ë³€?™ì‚¬???€??
    }

    public override void ClosePopupUI()
    {
        base.ClosePopupUI(); // ë°˜ë“œ???¸ì¶œ
    }

    private void BuildCategoryUI()
    {
        ResourceManager.Instance.TryLoad(OptionCategoryPanelMapSOKey, out categoryPanelMap);
        if (categoryPanelMap == null)
        {
            Logg.LogWarning("[OptionMenuUI] categoryPanelMap is not set");
            return;
        }

        GameObject categoryPanel = GetObject((int)GameObjects.categoryPanelRect);
        GameObject optionPanel = GetObject((int)GameObjects.optionPanelRect);
        if (categoryPanel == null || optionPanel == null)
        {
            Logg.LogWarning("[OptionMenuUI] categoryPanelRect or optionPanelRect is missing");
            return;
        }

        Transform categoryParent = categoryPanel.transform;
        Transform optionParent = optionPanel.transform;

        if (categoryPanelMap.CategoryButtonTemplate == null)
        {
            Logg.LogWarning("[OptionMenuUI] categoryButtonTemplate is not set in categoryPanelMap");
            return;
        }

        categoryPanels.Clear();
        bool first = true;

        foreach (var entry in categoryPanelMap.Entries)
        {
            if (entry == null || entry.panelPrefab == null) continue;

            OptionCategoryButton button = Instantiate(categoryPanelMap.CategoryButtonTemplate, categoryParent);
            button.gameObject.SetActive(true);
            SetButtonLabel(button, string.IsNullOrEmpty(entry.label) ? entry.category.ToString() : entry.label);

            GameObject panelInstance = Instantiate(entry.panelPrefab, optionParent);
            panelInstance.SetActive(false);

            Enums.OptionCategory category = entry.category;
            categoryPanels[category] = panelInstance;
            if (button.Button != null)
                button.Button.onClick.AddListener(() => ShowCategory(category));

            if (first)
            {
                first = false;
                ShowCategory(category);
            }
        }
    }

    private void ShowCategory(Enums.OptionCategory category)
    {
        if (activeCategory.HasValue &&
            categoryPanels.TryGetValue(activeCategory.Value, out var activePanel))
        {
            activePanel.SetActive(false);
        }

        if (categoryPanels.TryGetValue(category, out var panel))
        {
            panel.SetActive(true);
            activeCategory = category;
        }
    }

    private static void SetButtonLabel(OptionCategoryButton button, string label)
    {
        if (button == null) return;

        if (button.Label != null)
        {
            button.Label.text = label;
            return;
        }

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

}
