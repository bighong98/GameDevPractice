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

    // 카테고리 구성 정보를 담는 매핑 에셋
    private OptionCategoryPanelMapSO categoryPanelMap;

    // 카테고리별로 생성된 패널 캐시
    private readonly Dictionary<Enums.OptionCategory, GameObject> categoryPanels = new();
    private Enums.OptionCategory? activeCategory;

    private const string OptionCategoryPanelMapSOKey = "OptionCategoryPanelMapSO";
    private const string DefaultResetConfirmText = "옵션을 기본값으로 초기화하시겠습니까?";

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override bool Init()
    {
        if (base.Init() == false) return false;
        
        // UI 바인딩은 Init 단계에서 한 번만 수행
        BindObject(typeof(GameObjects));
        BindButton(typeof(Buttons));
        
        // 버튼 이벤트를 연결하고 카테고리 UI 구성
        GetButton((int)Buttons.defaultButton).onClick.AddListener(ShowResetConfirmPopup);
        GetButton((int)Buttons.exitButton).onClick.AddListener(ClosePopupUI);
        BuildCategoryUI();
        
        return true;
    }

    // 풀에서 가져올 때 CTS 초기화
    public override void OnGetFromPool()
    {
        base.OnGetFromPool();
        // 이 팝업에 종속된 팝업을 체인하기 위해 CTS 새로 준비
        CancelAndRenewPopupCTS();
    }

    // 팝업 닫힘 후 옵션 저장
    public override void OnPopupClosed()
    {
        // 옵션 변경을 PlayerPrefs에 반영
        PlayerPrefs.Save();
    }

    public override void ClosePopupUI()
    {
        // 닫히기 전에 활성 패널이 진행 중인 변경 정리 기회 제공
        NotifyActivePanelClosed();
        base.ClosePopupUI(); // 반드시 호출
    }

    private void BuildCategoryUI()
    {
        // 카테고리별 패널을 생성하고 버튼 클릭 시 전환되도록 구성
        // 맵 데이터를 로드해 카테고리/패널 정보 확보
        ResourceManager.Instance.TryLoad(OptionCategoryPanelMapSOKey, out categoryPanelMap);
        if (categoryPanelMap == null)
        {
            Logg.LogWarning("[OptionMenuUI] categoryPanelMap is not set");
            return;
        }

        // 카테고리 영역과 옵션 영역을 가져온다.
        GameObject categoryPanel = GetObject((int)GameObjects.categoryPanelRect);
        GameObject optionPanel = GetObject((int)GameObjects.optionPanelRect);
        if (categoryPanel == null || optionPanel == null)
        {
            Logg.LogWarning("[OptionMenuUI] categoryPanelRect or optionPanelRect is missing");
            return;
        }

        // 생성될 버튼/패널의 부모 트랜스폼 설정
        Transform categoryParent = categoryPanel.transform;
        Transform optionParent = optionPanel.transform;

        // 버튼 템플릿이 없으면 카테고리 UI 구성 불가
        if (categoryPanelMap.CategoryButtonTemplate == null)
        {
            Logg.LogWarning("[OptionMenuUI] categoryButtonTemplate is not set in categoryPanelMap");
            return;
        }

        // 기존 캐시를 비우고 첫 카테고리 자동 선택 준비
        categoryPanels.Clear();
        bool first = true;

        foreach (var entry in categoryPanelMap.Entries)
        {
            // 매핑 정보나 패널 프리팹이 없으면 건너뛴다.
            if (entry == null || entry.panelPrefab == null) continue;

            // 카테고리 버튼 생성 및 라벨 세팅
            OptionCategoryButton button = Instantiate(categoryPanelMap.CategoryButtonTemplate, categoryParent);
            button.gameObject.SetActive(true);
            SetButtonLabel(button, string.IsNullOrEmpty(entry.label) ? entry.category.ToString() : entry.label);

            // 옵션 패널 인스턴스 생성 후 비활성화
            GameObject panelInstance = Instantiate(entry.panelPrefab, optionParent);
            panelInstance.SetActive(false);

            // 패널 캐시 등록 및 클릭 시 해당 카테고리로 전환
            Enums.OptionCategory category = entry.category;
            categoryPanels[category] = panelInstance;
            if (button.Button != null)
                button.Button.onClick.AddListener(() => ShowCategory(category));

            if (first)
            {
                // 최초 진입 시 첫 카테고리 자동 오픈
                first = false;
                ShowCategory(category);
            }
        }
    }

    // 선택된 카테고리 패널을 활성화하고 이전 패널을 숨긴다.
    private void ShowCategory(Enums.OptionCategory category)
    {
        // 기존 활성 패널이 있으면 먼저 비활성화
        if (activeCategory.HasValue &&
            categoryPanels.TryGetValue(activeCategory.Value, out var activePanel))
        {
            activePanel.SetActive(false);
        }

        // 새 카테고리 패널을 활성화하고 상태 갱신
        if (categoryPanels.TryGetValue(category, out var panel))
        {
            panel.SetActive(true);
            activeCategory = category;
        }
    }

    // 기본값 초기화 확인 팝업 표시
    private void ShowResetConfirmPopup()
    {
        if (UIManager.Instance.ShowPopupUI<QuestionPopupUI>() is not { } popup) return;

        // 옵션 메뉴가 닫히면 확인 팝업도 함께 닫히도록 체인
        if (PopupCTS == null || PopupCTS.IsCancellationRequested)
            CancelAndRenewPopupCTS();

        popup.ChainPopupCTS(PopupCTS.Token);

        // 확인 시에만 기본값 복원 실행
        popup.SetQuestion(
            questionString: DefaultResetConfirmText,
            YesAction: () =>
            {
                ResetActivePanelToDefaults();
                popup.ClosePopupUI();
            },
            NoAction: () =>
            {
                popup.ClosePopupUI();
            });
    }

    // 현재 선택된 패널의 기본값 복원 실행
    private void ResetActivePanelToDefaults()
    {
        var optionPanel = GetActivePanel();
        optionPanel?.ApplyDefaults();
    }

    // 메뉴가 닫힐 때 활성 패널에 마무리 동작 알림
    private void NotifyActivePanelClosed()
    {
        var optionPanel = GetActivePanel();
        optionPanel?.NotifyMenuClosed();
    }

    // 현재 활성 패널 인스턴스에서 OptionPanelUIBase 탐색
    private OptionPanelUIBase GetActivePanel()
    {
        // 아직 선택된 카테고리가 없으면 패널도 없다.
        if (!activeCategory.HasValue)
            return null;

        // 캐시에 등록되지 않았거나 파괴된 패널이면 null 반환
        if (!categoryPanels.TryGetValue(activeCategory.Value, out var panel) || panel == null)
            return null;

        // 패널 루트 또는 하위에서 OptionPanelUIBase 탐색
        var optionPanel = panel.GetComponent<OptionPanelUIBase>();
        if (optionPanel == null)
            optionPanel = panel.GetComponentInChildren<OptionPanelUIBase>(true);

        return optionPanel;
    }

    // 카테고리 버튼 라벨 설정
    private static void SetButtonLabel(OptionCategoryButton button, string label)
    {
        if (button == null) return;

        // 명시된 Label 필드가 있으면 우선 사용
        if (button.Label != null)
        {
            button.Label.text = label;
            return;
        }

        // TMP를 우선 탐색하고, 없으면 레거시 Text로 폴백
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
