using System;
using RPG.UI;
using UnityEngine;
using UnityEngine.UI;

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

    private Slider[] sliders = new Slider[(int)Sliders.max];
    private void Awake()
    {
        Init();
    }

    public override bool Init()
    {
        if (base.Init() == false) return false;

        UIManager.Instance.ReserveOperation(() =>
        {
            UIManager.Instance.SetCanvas(gameObject, isInteractable: true);
            canvas = GetComponent<Canvas>();
            canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
        });
        
        BindObject(typeof(GameObjects));
        BindTMPText(typeof(TMPTexts));
        BindImage(typeof(Images));

        sliders[(int)Sliders.HP] = GetSlider(GetObject((int)GameObjects.HPBar));
        sliders[(int)Sliders.MP] = GetSlider(GetObject((int)GameObjects.MPBar));
        sliders[(int)Sliders.EXP] = GetSlider(GetObject((int)GameObjects.PlayerExpBar));
        
        return true;
    }

    private Slider GetSlider(GameObject go)
    {
        return Util.FindChild<Slider>(go, "bar");
    }
}
