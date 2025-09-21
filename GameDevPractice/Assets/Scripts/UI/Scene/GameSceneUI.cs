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
    private float prevLevelUpXp;
    private float nextLevelUpXp;
    
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
        
        ConnectComponents();
        
        return true;
    }

    private Slider GetSlider(GameObject go)
    {
        return Util.FindChild<Slider>(go, "bar");
    }

    private void ConnectComponents()
    {
        var player = FindFirstObjectByType<PlayerController>();
        
        if (player.GetComponent<Health>() is {} pHealth)
            pHealth.OnHealthRatioChanged += SetHPBar;
        if (player.GetComponent<IExperience>() is {} pExp)
            pExp.OnXpChanged += OnExpChanged;
        if (player.TryGetComponent(out ILevel pLevel))
            pLevel.OnLevelChanged += OnLevelUp;
        // if (player.GetComponent<CharacterStats>() is { } pCharacterStats)
        // {
        //     playerStats = pCharacterStats;
        //     // pCharacterStats.OnLevelUp += OnLevelUp;
        // }
        if (player.GetComponent<IStatHolder>() is { } pStatHolder)
        {
            statHolder = pStatHolder;
        }
    }
    
    private void SetHPBar(float ratio)
    {
        sliders[(int)Sliders.HP].value = ratio;
    }

    private void SetEXPBar(float ratio)
    {
        sliders[(int)Sliders.EXP].value = ratio;
    }

    private void SetLevelText(int level)
    {
        GetTMPText((int)TMPTexts.levelText).SetText($"{level}");
    }

    private void OnExpChanged(float xp)
    {
        var denominator = nextLevelUpXp - prevLevelUpXp;
        if (denominator < float.Epsilon) return;
        var numerator = xp - prevLevelUpXp;
        
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
        
        if (level > 0 && statHolder.GetStat(GameStat.ExperienceToLevelUp, level) is { } pResult)
        {
            prevLevelUpXp = (int)pResult;
        }

        if (statHolder.GetStat(GameStat.ExperienceToLevelUp, level) is { } nResult)
        {
            nextLevelUpXp = (int)nResult;
        }
        
        SetEXPBar(0);
    }
}
