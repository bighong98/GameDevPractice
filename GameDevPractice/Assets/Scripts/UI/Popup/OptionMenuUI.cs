using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class OptionMenuUI : PopupUI
{
    #region Enum

    enum GameObjects
    {
        soundVolumeSlider,
        musicVolumeSlider,
        edgeScrollingToggle,
    }

    enum Buttons
    {
        exitButton,
    }

    #endregion
    
    private Slider sfxSlider;
    private Slider bgmSlider;
    private Toggle edgeScrollingToggle;
    
    private readonly string BGM_VOLUME_KEY = $"{Enums.AudioType.Bgm}Volume";
    private readonly string SFX_VOLUME_KEY = $"{Enums.AudioType.Effect}Volume";
    private readonly string EDGE_SCROLLING_KEY = "EdgeScrolling";

    private float prevBgmValue;
    private float prevSfxValue;

    private void Awake()
    {
        Init();
    }

    public override bool Init()
    {
        if (base.Init() == false) return false;
        
        BindObject(typeof(GameObjects));
        BindButton(typeof(Buttons));
        
        sfxSlider = GetObject((int)GameObjects.soundVolumeSlider).GetOrAddComponent<Slider>();
        bgmSlider = GetObject((int)GameObjects.musicVolumeSlider).GetOrAddComponent<Slider>();
        edgeScrollingToggle = GetObject((int)GameObjects.edgeScrollingToggle).GetOrAddComponent<Toggle>();
        
        edgeScrollingToggle.onValueChanged.AddListener(
            (bool toggleValue) => 
            {
                // GameManager.Instance.NotifyEdgeScrollingToggled(toggleValue);
                PlayerPrefs.SetInt(EDGE_SCROLLING_KEY, toggleValue ?  1 : 0);
            });
        
        GetButton((int)Buttons.exitButton).onClick.AddListener(() =>
        {
            ClosePopupUI();
        });
        
        return true;
    }

    public void OnEnable()
    {
        RefreshSlider();
        edgeScrollingToggle.isOn = (PlayerPrefs.GetInt(EDGE_SCROLLING_KEY) == 1 ? true : false);
    }

    private void RefreshSlider()
    {
        // 슬라이더 값 초기화
        float bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY);

        sfxSlider.value = sfxVolume;
        bgmSlider.value = bgmVolume;
    }

    private void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePopupUI();
        }
        
        float newBgmValue = bgmSlider.value;
        float newSfxValue = sfxSlider.value;

        if (!Mathf.Approximately(newBgmValue, prevBgmValue)) // bgmValue 값이 변동된 경우
        {
            prevBgmValue = newBgmValue; // 변동값 값 캐싱
            PlayerPrefs.SetFloat(BGM_VOLUME_KEY, newBgmValue); // PlayerPrefs에 변동값 반영
            SoundManager.Instance.ChangeSoundVolume(Enums.AudioType.Bgm, newBgmValue); // 즉시 bgm 소리 크기에 적용
        }

        if (!Mathf.Approximately(newSfxValue, prevSfxValue)) // sfxValue 값이 변동된 경우
        {
            prevSfxValue = newSfxValue;
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, newSfxValue);
            SoundManager.Instance.ChangeSoundVolume(Enums.AudioType.Effect, newSfxValue);
        }
    }

    public override void OnGetFromPool()
    {
        base.OnGetFromPool();
        UIManager.Instance.OnOptionMenuUIOpen();
    }

    public override void OnPopupClosed()
    {
        PlayerPrefs.Save(); // 창 닫을 때 변동사항 저장
        UIManager.Instance.OnOptionMenuUIClose();
    }
    public override void ClosePopupUI()
    {
        base.ClosePopupUI(); // 반드시 호출
        UIManager.Instance.OnOptionMenuUIClose(); //
    }
}
