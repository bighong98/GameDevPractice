using System;
using Cysharp.Threading.Tasks;
using TH.UI;
using TH.Core.Service;
using TH.SceneManagement;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

public class SimpleLoadingUI : MonoBehaviour, ILoadingUI
{
    [SerializeField] private Slider slider;
    private float targetRatio;

    private IProgressSubscription sub;

    private void OnEnable()
    {
        if (ServiceLocator.Get<ISceneLoader>() is { } sceneLoader)
        {
            sub = sceneLoader.SubscribeProgress(SetProgress);
        }
    }

    private void OnDisable()
    {
        sub?.Dispose();
        sub = null;
    }

    public void SetProgress(float ratio)
    {
        SetBar(ratio);
    }

    private void SetBar(float ratio)
    {
        if (slider == null)
        {
            Logg.LogError($"[SimpleLoadingUI] slider is null but invoked");
            return;
        }
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
