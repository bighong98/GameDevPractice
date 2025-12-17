using System;
using System.Threading;
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
            Logg.LogWarning($"[SimpleLoadingUI] slider is null but invoked");
            return;
        }
        // slider.value = Mathf.Clamp01(ratio);
        ChangeFillSlowly(slider, slider.value, ratio, EasingSpeed).Forget();
    }

    public UniTask ShowAsync()
    {
        return UniTask.CompletedTask;
    }

    public UniTask HideAsync()
    {
        return UniTask.CompletedTask;
    }
    
    private bool easing; // 천천히 움직이는 bar 애니메이션 실행중인지 여부
    private const float EasingSpeed = 0.5f;
    private async UniTask ChangeFillSlowly(Slider s, float from, float to, float speed)
    {
        if (easing) StopBarAnimation(); // 기존 바 애니메이션 중지
        ClarifyToken();
            
        float curr = from;
        easing = true;
        while (!barAnimToken.IsCancellationRequested && !Mathf.Approximately(curr, to))
        {
            try
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, barAnimToken).SuppressCancellationThrow();
            }
            catch (Exception e)
            {
                Logg.LogError($"[{nameof(HPBar)}] error occurred while {nameof(ChangeFillSlowly)}(). {e}");
                break;
            }
            curr = Mathf.MoveTowards(curr, to, speed * Time.deltaTime);
            s.value = curr;
        }
            
        if (s.IsAlive())
            s.value = to;
        easing = false;
    }

    private CancellationTokenSource barAnimCTS = new ();
    private CancellationToken barAnimToken;

    private void StopBarAnimation()
    {
        barAnimCTS.Cancel();
        barAnimCTS.Dispose();

        easing = false;
    }
    private void ClarifyToken()
    {
        if (barAnimToken is { CanBeCanceled: true, IsCancellationRequested: false }) return;
        
        barAnimCTS = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        barAnimToken = barAnimCTS.Token;
    }
}
