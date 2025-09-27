using System;
using Cysharp.Threading.Tasks;
using RPG.UI;
using TH.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class SimpleLoadingUI : BaseUI, ILoadingUI
{
    #region Enums

    enum GameObjects
    {
        progress,
    }

    #endregion

    [SerializeField] private Slider slider;
    
    private void Awake()
    {
        BindObject(typeof(GameObjects));
        if (GetObject((int)GameObjects.progress) is { } holder
            && slider.TryGetComponent(out Slider s))
        {
            slider = s;
        }
    }

    public void SetProgress(float ratio)
    {
        slider.value = Mathf.Clamp01(ratio);
    }

    public UniTask ShowAsync()
    {
        return UniTask.CompletedTask;
    }

    public UniTask HideAsync()
    {
        return UniTask.CompletedTask;
    }
}
