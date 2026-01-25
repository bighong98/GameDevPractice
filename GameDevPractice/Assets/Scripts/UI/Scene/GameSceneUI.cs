using System;
using TH.Attribute;
using TH.Control;
using UnityEngine;
using UnityEngine.UI;
using TH.Attribute.Stat;
using TH.UI;
using TH.Utils;
using TH.Core.Service;

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
    
    // 스탯/플레이어 홀더 참조
    private IStatHolder statHolder;
    private IPlayerHolder playerHolder;
    
    protected override void Awake()
    {
        // 베이스 초기화 호출
        base.Awake();
        // UI 구성 초기화
        Init();
    }

    private void Start()
    {
        // 초기 플레이어 인스턴스 즉시 연결
        if (playerHolder.GetPlayerInstance is PlayerController p && p.IsNotNull())
        {
            UpdatePlayerInstance(p);
        }
    }

    public override bool Init() // UIManager 호출
    {
        // 베이스 초기화 검사
        if (base.Init() == false) return false;
        
        // 캔버스/캔버스그룹 확보
        canvas = GetComponent<Canvas>();
        canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
        
        // 바인딩 테이블 구성
        BindObject(typeof(GameObjects));
        BindTMPText(typeof(TMPTexts));
        BindImage(typeof(Images));

        // 슬라이더 핸들러 생성
        sliderHandlers[(int)Sliders.HP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.HPBar)));
        sliderHandlers[(int)Sliders.MP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.MPBar)));
        sliderHandlers[(int)Sliders.EXP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.PlayerExpBar)));

        foreach (var sliderHandler in sliderHandlers)
        {
            // 하이라이트 이벤트 바인딩
            BindSliderEvent(sliderHandler);
            // 초기 하이라이트 해제
            sliderHandler.OffHighlight();
        }

        // 플레이어 홀더 구독
        playerHolder = ServiceLocator.Get<IPlayerHolder>();
        playerHolder.OnPlayerInstanceUpdated += UpdatePlayerInstance;
        
        return true;
    }

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

    private void UpdatePlayerInstance(object o)
    {
        // 종료 중 보호
        if (Util.IsQuitting) return;
        // 플레이어 인스턴스 갱신 처리
        if (o is PlayerController player)
        {
            // 기존 이벤트 해제
            DisConnectComponents(player);
            // 새 이벤트 연결
            ConnectComponents(player);
            // 초기 스냅샷 갱신 요청
            RequestInitialRefresh(player);
        }
    }

    private void RequestInitialRefresh(PlayerController player)
    {
        // 유효성 검사
        if (Util.IsQuitting || !player.IsNotNull()) return;
        // 프리로드 완료 후 초기 스냅샷 갱신 예약
        ResourceManager.Instance.WaitForPreLoadOnlyOnce(() =>
        {
            // 콜백 시점 재검사
            if (Util.IsQuitting || !player.IsNotNull()) return;
            // 홀더 인스턴스 변경 여부 확인
            var holderPlayer = playerHolder?.GetPlayerInstance as PlayerController;
            if (holderPlayer != null && holderPlayer != player) return;
            // 스냅샷 반영
            RefreshPlayerSnapshot(player);
        });
    }

    private void RefreshPlayerSnapshot(PlayerController player)
    {
        // 종료 중 보호
        if (Util.IsQuitting) return;

        // HP 현재/최대 반영
        if (TryConnectComponent(player, out Health pHealth))
        {
            sliderHandlers[(int)Sliders.HP].SetCeil(pHealth.GetMaxHealth);
            sliderHandlers[(int)Sliders.HP].SetFloor(pHealth.GetCurrentHealth);
        }

        // XP 현재값 반영
        if (TryConnectComponent(player, out IExperience pExp))
            sliderHandlers[(int)Sliders.EXP].SetFloor(pExp.GetCurrXp);

        // 레벨 스냅샷 반영 (베이스라인/상한 갱신 포함)
        if (TryConnectComponent(player, out ILevel pLevel))
            OnLevelUp(pLevel.GetCurrLevel);
    }


    private void ConnectComponents(PlayerController player)
    {
        // 스탯 홀더 캐시
        if (TryConnectComponent(player, out IStatHolder pStatHolder))
            statHolder = pStatHolder;

        // 체력 이벤트 연결
        if (TryConnectComponent(player, out Health pHealth))
        {
            pHealth.OnCurrHealthChanged += sliderHandlers[(int)GameObjects.HPBar].SetFloor;
            pHealth.OnMaxHealthChanged += sliderHandlers[(int)GameObjects.HPBar].SetCeil;
        }

        // 경험치 이벤트 연결
        if (TryConnectComponent(player, out IExperience pExp))
        {
            pExp.OnXpChanged += sliderHandlers[(int)GameObjects.PlayerExpBar].SetFloor;
        }

        // 레벨 이벤트 연결
        if (TryConnectComponent(player, out ILevel pLevel))
        {
            pLevel.OnLevelChanged += OnLevelUp;
        }
    }

    private void DisConnectComponents(PlayerController player)
    {
        // 종료 중 보호
        if (Util.IsQuitting) return;

        // 체력 이벤트 해제
        if (TryConnectComponent(player, out Health pHealth))
        {
            pHealth.OnCurrHealthChanged -= sliderHandlers[(int)GameObjects.HPBar].SetFloor;
            pHealth.OnMaxHealthChanged -= sliderHandlers[(int)GameObjects.HPBar].SetCeil;
        }

        // 경험치 이벤트 해제
        if (TryConnectComponent(player, out IExperience pExp))
            pExp.OnXpChanged -= sliderHandlers[(int)GameObjects.PlayerExpBar].SetFloor;
        // 레벨 이벤트 해제
        if (TryConnectComponent(player, out ILevel pLevel))
            pLevel.OnLevelChanged -= OnLevelUp;
    }

    private bool TryConnectComponent<T>(in Component from, out T c)
    {
        // 컴포넌트 탐색 시도
        if (from.TryGetComponent(out T result))
        {
            c = result;
            return true;
        }

        // 누락 로그 출력
        Logg.Log($"[{nameof(GameSceneUI)}.{nameof(TryConnectComponent)}] failed to Find Component: {typeof(T).Name}");
        c = default;
        return false;
    }

    private void SetLevelText(int level)
    {
        // 레벨 텍스트 갱신
        GetTMPText((int)TMPTexts.levelText).SetText($"{level}");
    }

    private void OnLevelUp(int level)
    {
        // 레벨 텍스트 갱신
        SetLevelText(level);

        // 스탯 홀더 확보
        if (statHolder == null)
        {
            if (FindFirstObjectByType<PlayerController>() is { } foundPlayer
                && foundPlayer.GetComponent<IStatHolder>() is { } pStatHolder)
            {
                statHolder = pStatHolder;
            }
            else return;
        }
        
        // 레벨 구간 계산
        int currLevel = level;
        int prevLevel = level - 1;
        
        // 이전 레벨 기준 XP 베이스라인 갱신
        if (prevLevel > 0 && statHolder.GetStat(GameStats.ExperienceToLevelUp, prevLevel) is { } baseline)
        {
            Logg.Log($"[{nameof(GameSceneUI)}.{nameof(OnLevelUp)}()] xp baseline is changed: {baseline}", Logg.LoggingMode.Completed);
            sliderHandlers[(int)Sliders.EXP].SetBaseline(baseline);
        }

        // 현재 레벨 기준 XP 상한 갱신
        if (currLevel > 0 && statHolder.GetStat(GameStats.ExperienceToLevelUp, currLevel) is { } ceil)
        {
            Logg.Log($"[{nameof(GameSceneUI)}.{nameof(OnLevelUp)}()] ceilXp is changed: {ceil}", Logg.LoggingMode.Completed);
            sliderHandlers[(int)Sliders.EXP].SetCeil(ceil);
        }
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

        // 이벤트 구독 해제
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
