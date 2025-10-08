using System;
using RPG.Attribute;
using RPG.Control;
using RPG.Stats;
using RPG.UI;
using UnityEngine;
using UnityEngine.UI;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Resource;
using TH.UI;
using TH.Utils;
using Debug = System.Diagnostics.Debug;

public class GameSceneUI : BaseUI
{
    #region Enums

    enum GameObjects
    {
        HPBar,
        MPBar,
        PlayerExpBar,
    }

    enum TMPTexts
    {
        CharacterNameText,
        levelText,
    }

    enum Images
    {
        characterIconImage,
    }

    enum Sliders
    {
        HP,
        MP,
        EXP,
        max,
    }

    #endregion

    // private readonly Slider[] sliders = new Slider[(int)Sliders.max];
    private readonly ISliderUIHandler[] sliderHandlers = new ISliderUIHandler[(int)Sliders.max];
    
    private IStatHolder statHolder;
    private float floorXp;
    private float ceilXp;
    private float currXp;
    
    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override bool Init() // call by UIManager
    {
        if (base.Init() == false) return false;
        
        canvas = GetComponent<Canvas>();
        canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
        
        BindObject(typeof(GameObjects));
        BindTMPText(typeof(TMPTexts));
        BindImage(typeof(Images));

        sliderHandlers[(int)Sliders.HP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.HPBar)));
        sliderHandlers[(int)Sliders.MP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.MPBar)));
        sliderHandlers[(int)Sliders.EXP] = new SliderUIHandler(GetSlider(GetObject((int)GameObjects.PlayerExpBar)));

        foreach (var sliderHandler in sliderHandlers)
        {
            BindSliderEvent(sliderHandler);
            sliderHandler.OffHighlight();
        }
        
        return true;
    }

    private void OnEnable()
    {
        ConnectComponents();
    }

    private void OnDisable()
    {
        DisConnectComponents();
    }

    private Slider GetSlider(GameObject go)
    {
        if (Util.FindChild<Slider>(go, "bar") is not { } slider)
        {
            slider = go.AddComponent<Slider>();
            //todo: slider 기본 세팅
        }
        
        return slider;
    }

    private void BindSliderEvent(ISliderUIHandler sliderHandler)
    {
        if (sliderHandler.GetSlider is not { gameObject: { } go } || go == null) return;
        var parentGo = go.transform.parent.gameObject; // 임시
        
        BindEvent(parentGo, sliderHandler.OnHighlight, type: Enums.UIEvent.PointerEnter);
        BindEvent(parentGo, sliderHandler.OffHighlight, type: Enums.UIEvent.PointerExit);
    }

    private void ConnectComponents()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        
        if (TryConnectComponent(player, out IStatHolder pStatHolder))
            statHolder = pStatHolder;

        if (TryConnectComponent(player, out Health pHealth))
        {
            pHealth.OnCurrHealthChanged += sliderHandlers[(int)GameObjects.HPBar].SetFloor;
            pHealth.OnMaxHealthChanged += sliderHandlers[(int)GameObjects.HPBar].SetCeil;
        }
        if (TryConnectComponent(player, out IExperience pExp))
            pExp.OnXpChanged += sliderHandlers[(int)GameObjects.PlayerExpBar].SetFloor;
        if (TryConnectComponent(player, out ILevel pLevel))
            pLevel.OnLevelChanged += OnLevelUp;
    }

    private void DisConnectComponents()
    {
        if (Util.IsQuitting) return;
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        if (TryConnectComponent(player, out Health pHealth))
        {
            pHealth.OnCurrHealthChanged -= sliderHandlers[(int)GameObjects.HPBar].SetFloor;
            pHealth.OnMaxHealthChanged -= sliderHandlers[(int)GameObjects.HPBar].SetCeil;
        }

        if (TryConnectComponent(player, out IExperience pExp))
            pExp.OnXpChanged -= sliderHandlers[(int)GameObjects.PlayerExpBar].SetFloor;
        if (TryConnectComponent(player, out ILevel pLevel))
            pLevel.OnLevelChanged -= OnLevelUp;
    }

    private bool TryConnectComponent<T>(in Component from, out T c)
    {
        if (from.TryGetComponent(out T result))
        {
            c = result;
            return true;
        }

        Logg.Log($"[{nameof(GameSceneUI)}.{nameof(TryConnectComponent)}] failed to Find Component: {typeof(T).Name}");
        c = default;
        return false;
    }

    private void SetLevelText(int level)
    {
        GetTMPText((int)TMPTexts.levelText).SetText($"{level}");
    }

    private void OnLevelUp(int level)
    {
        SetLevelText(level);

        if (statHolder == null)
        {
            if (FindFirstObjectByType<PlayerController>() is { } foundPlayer
                && foundPlayer.GetComponent<IStatHolder>() is { } pStatHolder)
            {
                statHolder = pStatHolder;
            }
            else return;
        }
        
        int currLevel = level;
        int prevLevel = level - 1;
        
        if (prevLevel > 0 && statHolder.GetStat(GameStats.ExperienceToLevelUp, prevLevel) is { } baseline)
        {
            Logg.Log($"[{nameof(GameSceneUI)}.{nameof(OnLevelUp)}()] xp baseline is changed: {baseline}", Logg.LoggingMode.Completed);
            sliderHandlers[(int)Sliders.EXP].SetBaseline(baseline);
        }

        if (currLevel > 0 && statHolder.GetStat(GameStats.ExperienceToLevelUp, currLevel) is { } ceil)
        {
            Logg.Log($"[{nameof(GameSceneUI)}.{nameof(OnLevelUp)}()] ceilXp is changed: {ceil}", Logg.LoggingMode.Completed);
            sliderHandlers[(int)Sliders.EXP].SetCeil(ceil);
        }
    }

    protected override void Clear()
    {
        base.Clear();
        foreach (var s in sliderHandlers)
        {
            s.OffHighlight();
        }
    }
}
