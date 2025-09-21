using System;
using RPG.Attribute;
using RPG.Control;
using RPG.Stats;
using RPG.UI;
using UnityEngine;
using UnityEngine.UI;
using TH.Attribute;
using TH.Attribute.Stat;

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

    private readonly Slider[] sliders = new Slider[(int)Sliders.max];

    // private CharacterStats playerStats;
    private IStatHolder statHolder;
    private float floorXp;
    private float ceilXp;
    private float currXp;
    
    private void Awake()
    {
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

        sliders[(int)Sliders.HP] = GetSlider(GetObject((int)GameObjects.HPBar));
        sliders[(int)Sliders.MP] = GetSlider(GetObject((int)GameObjects.MPBar));
        sliders[(int)Sliders.EXP] = GetSlider(GetObject((int)GameObjects.PlayerExpBar));
        
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
        return Util.FindChild<Slider>(go, "bar");
    }

    private void ConnectComponents()
    {
        var player = FindFirstObjectByType<PlayerController>();
        
        // if (player.GetComponent<Health>() is {} pHealth)
        //     pHealth.OnHealthRatioChanged += SetHPBar;
        // if (player.GetComponent<IExperience>() is {} pExp)
        //     pExp.OnXpChanged += OnExpChanged;
        // if (player.TryGetComponent(out ILevel pLevel))
        //     pLevel.OnLevelChanged += OnLevelUp;
        // if (player.GetComponent<CharacterStats>() is { } pCharacterStats)
        // {
        //     playerStats = pCharacterStats;
        //     // pCharacterStats.OnLevelUp += OnLevelUp;
        // }
        // if (player.GetComponent<IStatHolder>() is { } pStatHolder)
        // {
        //     statHolder = pStatHolder;
        // }

        if (TryConnectComponent(player, out Health pHealth))
            pHealth.OnHealthRatioChanged += SetHPBar;
        if (TryConnectComponent(player, out IExperience pExp))
            pExp.OnXpChanged += OnExpChanged;
        if (TryConnectComponent(player, out ILevel pLevel))
        {
            pLevel.OnLevelChanged += OnLevelUp;
        }
        if (TryConnectComponent(player, out IStatHolder pStatHolder))
            statHolder = pStatHolder;
        
    }

    private void DisConnectComponents()
    {
        if (Util.IsQuitting) return;
        var player = FindFirstObjectByType<PlayerController>();
        
        if (TryConnectComponent(player, out Health pHealth))
            pHealth.OnHealthRatioChanged -= SetHPBar;
        if (TryConnectComponent(player, out IExperience pExp))
            pExp.OnXpChanged -= OnExpChanged;
        if (TryConnectComponent(player, out ILevel pLevel))
        {
            pLevel.OnLevelChanged -= OnLevelUp;
        }
        if (TryConnectComponent(player, out IStatHolder pStatHolder))
            statHolder = pStatHolder;
    }

    private bool TryConnectComponent<T>(in Component from, out T c)
    {
        if (from.TryGetComponent(out T result))
        {
            c = result;
            return true;
        }

        Util.Log($"[{nameof(GameSceneUI)}.{nameof(TryConnectComponent)}] failed to Find Component: {typeof(T).Name}");
        c = default;
        return false;
    }
    
    private void SetHPBar(float ratio)
    {
        sliders[(int)Sliders.HP].value = ratio;
    }

    private void SetEXPBar(float ratio)
    {
        Util.Log($"[{nameof(GameSceneUI)}.{nameof(SetEXPBar)}] trying to set xp bar : {ratio}", Util.LoggingMode.Completed);
        sliders[(int)Sliders.EXP].value = ratio;
    }

    private void SetLevelText(int level)
    {
        GetTMPText((int)TMPTexts.levelText).SetText($"{level}");
    }

    private void OnExpChanged(float xp)
    {
        var denominator = ceilXp - floorXp;
        if (denominator.IsEqualFloat(0f)) return;
        var numerator = xp - floorXp;
        
        Util.Log($"[{nameof(GameSceneUI)}.{nameof(OnExpChanged)}()] trying to set xp bar: {numerator} / {denominator}", Util.LoggingMode.Completed);
        SetEXPBar(numerator / denominator);
    }

    private void OnLevelUp(int level)
    {
        SetLevelText(level);

        // if (playerStats == null && FindFirstObjectByType<PlayerController>() is {} foundPlayer
        //     && foundPlayer.GetComponent<CharacterStats>() is {} pStats)
        // {
        //     playerStats = pStats;
        // }
        
        // if (level > 0 && playerStats.GetStat(GameStat.ExperienceToLevelUp, level) is { } pResult)
        // {
        //     prevLevelUpXp = (int)pResult;
        // }
        //
        // if (playerStats.GetStat(GameStat.ExperienceToLevelUp, level) is { } nResult)
        // {
        //     nextLevelUpXp = (int)nResult;
        // }

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
        
        if (prevLevel > 0 && statHolder.GetStat(GameStat.ExperienceToLevelUp, prevLevel) is { } floor)
        {
            Util.Log($"[{nameof(GameSceneUI)}.{nameof(OnLevelUp)}()] floorXp is changed: {floor}", Util.LoggingMode.Completed);
            floorXp = floor;
        }

        if (currLevel > 0 && statHolder.GetStat(GameStat.ExperienceToLevelUp, currLevel) is { } ceil)
        {
            Util.Log($"[{nameof(GameSceneUI)}.{nameof(OnLevelUp)}()] ceilXp is changed: {ceil}", Util.LoggingMode.Completed);
            ceilXp = ceil;
        }
        else
        {
            ceilXp = 0;
        }
        //todo: 여기서 한번 더 xp 슬라이더 갱신할지 고려
    }
}
