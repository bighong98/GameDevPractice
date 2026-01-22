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
        defaultButton,
    }

    enum GameObjects
    {
        categoryPanelRect,
        optionPanelRect,
    }

    #endregion

    private OptionCategoryPanelMapSO categoryPanelMap;

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
        
        // 버튼 이벤트를 연결하고 카테고리 UI를 구성한다.
        GetButton((int)Buttons.defaultButton).onClick.AddListener(ResetActivePanelToDefaults);
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
        // 닫히기 전에 활성 패널이 진행 중인 변경을 정리할 기회를 준다.
        NotifyActivePanelClosed();
        base.ClosePopupUI(); // 반드시 호출
    }

    private void BuildCategoryUI()
    {
        // 카테고리별 패널을 생성하고 버튼 클릭 시 전환되도록 구성한다.
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

    // 선택된 카테고리 패널을 활성화하고 이전 패널을 숨긴다.
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

    private void ResetActivePanelToDefaults()
    {
        // 현재 선택된 패널의 기본값 복원을 실행한다.
        var optionPanel = GetActivePanel();
        optionPanel?.ApplyDefaults();
    }


    
    private void NotifyActivePanelClosed()
    {
        // 메뉴가 닫힐 때 활성 패널이 마무리 동작을 수행하도록 알린다.
        var optionPanel = GetActivePanel();
        optionPanel?.NotifyMenuClosed();
    }

    // 현재 활성 패널 인스턴스에서 OptionPanelUIBase를 찾는다.
    private OptionPanelUIBase GetActivePanel()
    {
        if (!activeCategory.HasValue)
            return null;

        if (!categoryPanels.TryGetValue(activeCategory.Value, out var panel) || panel == null)
            return null;

        var optionPanel = panel.GetComponent<OptionPanelUIBase>();
        if (optionPanel == null)
            optionPanel = panel.GetComponentInChildren<OptionPanelUIBase>(true);

        return optionPanel;
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
