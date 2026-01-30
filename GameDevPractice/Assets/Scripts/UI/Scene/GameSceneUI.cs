using System;
using TH.Attribute;
using TH.Control;
using UnityEngine;
using UnityEngine.UI;
using TH.Attribute.Stat;
using TH.UI;
using TH.Utils;
using TH.Core.Service;
using Cysharp.Threading.Tasks;

public class GameSceneUI : SceneUI
{
    #region Enums

    // UI 오브젝트 바인딩 키
    enum GameObjects
    {
        HPBar,
        MPBar,
        PlayerExpBar,
        QuickSlotPanel,
    }

    // 텍스트 바인딩 키
    enum TMPTexts
    {
        CharacterNameText,
        levelText,
    }

    // 이미지 바인딩 키
    enum Images
    {
        characterIconImage,
    }

    // 슬라이더 바인딩 키
    enum Sliders
    {
        HP,
        MP,
        EXP,
        max,
    }

    #endregion

    // 슬라이더 UI 핸들러 캐시
    private readonly ISliderUIHandler[] sliderHandlers = new ISliderUIHandler[(int)Sliders.max];
    
    // 플레이어 인스턴스 홀더 참조
    private IPlayerHolder playerHolder;
    
    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override bool Init()
    {
        // 최초 1회 초기화 필요 여부 검사 
        if (base.Init() == false) return false;

        // Graphic 컴포넌트 연결
        BindObject(typeof(GameObjects));
        BindTMPText(typeof(TMPTexts));
        BindImage(typeof(Images));

        // 슬라이더 핸들러 생성
        sliderHandlers[(int)Sliders.HP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.HPBar)));
        sliderHandlers[(int)Sliders.MP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.MPBar)));
        sliderHandlers[(int)Sliders.EXP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.PlayerExpBar)));

        foreach (var sliderHandler in sliderHandlers)
        {
            // UI 이벤트 바인딩
            BindSliderEvent(sliderHandler);
            // 초기 하이라이트 해제
            sliderHandler.OffHighlight();
        }

        return true;
    }

    private void Start()
    {
        BindPlayerUpdateEvent();
    }

    #region Bind Event

    private void BindPlayerUpdateEvent()
    {
        playerHolder = ServiceLocator.Get<IPlayerHolder>();

        // 즉시 플레이어 인스턴스 및 이벤트 갱신
        UpdatePlayerInstance(playerHolder.GetPlayerInstance);
        // 플레이어 인스턴스 갱신 이벤트 구독
        playerHolder.OnPlayerInstanceUpdated += UpdatePlayerInstance;
    }

    private void BindSliderEvent(ISliderUIHandler sliderHandler)
    {
        // 슬라이더/게임오브젝트 유효성 검사
        if (sliderHandler.GetSlider is not { gameObject: { } go } || go == null) return;
        // 부모 오브젝트 기준 이벤트 바인딩 (임시 처리)
        var parentGo = go.transform.parent.gameObject;
        
        // 마우스 오버 하이라이트
        BindEvent(parentGo, sliderHandler.OnHighlight, type: Enums.UIEvent.PointerEnter);
        // 마우스 아웃 하이라이트 해제
        BindEvent(parentGo, sliderHandler.OffHighlight, type: Enums.UIEvent.PointerExit);
    }

    #endregion

    #region Handle Event

    private void UpdatePlayerInstance(object o)
    {
        // 어플리케이션 상태 및 플레이어 인스턴스 유효성 검사
        if (Util.IsQuitting) return;
        if (o.IsNull() || o is not PlayerController player) return;
        
        // 기존 이벤트 해제
        DisConnectComponents(player);
        // 새 이벤트 연결
        ConnectComponents(player);
    }

    private void ConnectComponents(PlayerController player)
    {
        // 플레이어 체력(hp, maxHp) 정보 반영 및 이벤트 연결
        if (TryConnectComponent(player, out Health pHealth))
        {
            var sliderHandler = sliderHandlers[(int)Sliders.HP];
            
            sliderHandler.SetCeil(pHealth.MaxHp);
            sliderHandler.SetFloor(pHealth.Hp);

            pHealth.OnCurrHealthChanged += sliderHandler.SetFloor;
            pHealth.OnMaxHealthChanged += sliderHandler.SetCeil;
        }
        // 마나(MP) 스탯 반영 및 이벤트 연결
        if (TryConnectComponent(player, out IStatHolder pStatHolder))
        {
            // BindStatChanged는 스탯 생성 이후 자동 연결/초기값 동기화 처리
            pStatHolder.BindStatChanged(GameStats.Mana, OnManaChanged);
        }


        // 플레이어 레벨 반영 및 이벤트 연결
        if (TryConnectComponent(player, out ILevel pLevel))
        {
            HandleOnLevelUp(pLevel.GetCurrLevel);
            pLevel.OnLevelChanged += HandleOnLevelUp;
        }

        // 플레이어 경험치 반영 및 이벤트 연결
        if (TryConnectComponent(player, out IExperience pExp))
        {
            var sliderHandler = sliderHandlers[(int)Sliders.EXP];

            sliderHandler.SetFloor(pExp.GetCurrXp);
            sliderHandler.SetCeil(pExp.GetCurrXpToLevelUp);
            sliderHandler.SetBaseline(pExp.GetCurrBaselineXp);

            pExp.OnXpChanged += sliderHandler.SetFloor;
            pExp.OnXpToLevelUpChanged += sliderHandler.SetCeil;
            pExp.OnXpBaselineChanged += sliderHandler.SetBaseline;
        }
    }

    private void DisConnectComponents(PlayerController player)
    {
        // 종료 중 보호
        if (Util.IsQuitting) return;

        // 체력 이벤트 해제
        if (TryConnectComponent(player, out Health pHealth))
        {
            var sliderHandler = sliderHandlers[(int)Sliders.HP];

            pHealth.OnCurrHealthChanged -= sliderHandler.SetFloor;
            pHealth.OnMaxHealthChanged -= sliderHandler.SetCeil;
        }
        // 마나(MP) 이벤트 해제
        if (TryConnectComponent(player, out IStatHolder pStatHolder))
        {
            pStatHolder.UnbindStatChanged(GameStats.Mana, OnManaChanged);
        }


        // 레벨 이벤트 해제
        if (TryConnectComponent(player, out ILevel pLevel))
            pLevel.OnLevelChanged -= HandleOnLevelUp;

        // 경험치 이벤트 해제
        if (TryConnectComponent(player, out IExperience pExp))
        {
            var sliderHandler = sliderHandlers[(int)Sliders.EXP];

            pExp.OnXpChanged -= sliderHandler.SetFloor;
            pExp.OnXpToLevelUpChanged -= sliderHandler.SetCeil;
            pExp.OnXpBaselineChanged -= sliderHandler.SetBaseline;
        }
    }

    private bool TryConnectComponent<T>(in Component from, out T c)
    {
        bool result = from.TryGetComponent(out c);
        if (!result) Logg.Log($"[{nameof(GameSceneUI)}.{nameof(TryConnectComponent)}] failed to Find Component: {typeof(T).Name}");
        
        return result;
    }

    private void HandleOnLevelUp(int level)
    {
        // 레벨 텍스트 갱신
        SetLevelText(level);
    }

    private void OnManaChanged(float mana)
    {
        var sliderHandler = sliderHandlers[(int)Sliders.MP];
        sliderHandler.SetCeil(mana);
        sliderHandler.SetFloor(mana);
    }


    #endregion 

    public override void RefreshUI()
    {
        // UI 새로고침 로그
        Logg.Log($"[{GetType().Name}] RefreshUI() invoked", Logg.LoggingMode.Completed);
        // 베이스 새로고침 호출
        base.RefreshUI();
    }

    private Slider GetSlider(GameObject go)
    {
        // 하위 바 오브젝트에서 Slider 탐색
        if (Util.FindChild<Slider>(go, "bar") is not { } slider)
        {
            // 없으면 임시 추가
            slider = go.AddComponent<Slider>();
            // todo: slider 기본 설정
        }
        
        // 획득된 Slider 반환
        return slider;
    }

    private void SetLevelText(int level)
    {
        // 레벨 텍스트 갱신
        GetTMPText((int)TMPTexts.levelText).SetText($"{level}");
    }

    protected override void Clear()
    {
        // 베이스 정리
        base.Clear();
        // 하이라이트 해제
        foreach (var s in sliderHandlers)
        {
            s.OffHighlight();
        }

        // 플레이어 인스턴스 갱신 이벤트 구독 해제
        playerHolder.OnPlayerInstanceUpdated -= UpdatePlayerInstance;
    }

    public override bool GetQuickSlotPanelUI(out QuickSlotPanelUI quickSlotPanelUI)
    {
        // 퀵슬롯 패널 컴포넌트 탐색
        if (GetObject((int)GameObjects.QuickSlotPanel) is {} go && go.IsNotNull()
            && go.TryGetComponent<QuickSlotPanelUI>(out quickSlotPanelUI))
        {
            return true;
        }

        // 실패 처리
        quickSlotPanelUI = default;
        return false;
    }
}
