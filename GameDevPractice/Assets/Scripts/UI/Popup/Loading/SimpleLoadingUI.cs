using System;
using Cysharp.Threading.Tasks;
using RPG.UI;
using TH.Core.Service;
using TH.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class SimpleLoadingUI : BaseUI, ILoadingUI
{
    #region Enums

    enum GameObjects
    {
        progressBar,
    }

    #endregion

    [SerializeField] private Slider slider;
    private float targetRatio;

    private void OnEnable()
    {
        if (ServiceLocator.Get<ISceneLoader>() is { } sceneLoader)
        {
            sceneLoader.BindProgress(SetProgress);
        }
    }

    public void SetProgress(float ratio)
    {
        SetBar(ratio);
    }

    private void SetBar(float ratio)
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
