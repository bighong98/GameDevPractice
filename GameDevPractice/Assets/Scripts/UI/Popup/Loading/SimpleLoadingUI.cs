using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.SceneManagement;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

public class SimpleLoadingUI : MonoBehaviour, ILoadingUI
{
    [SerializeField] private Slider slider;

    // 표시용 값 / 목표 값
    private float _displayValue;
    private float _targetValue;

    // 진행도 바 보간 속도
    private const float FollowSpeed = 0.5f;

    private IProgressSubscription _sub;
    private ISceneLoader _sceneLoader;

    private CancellationTokenSource _animCts;

    private void Awake()
    {
        _sceneLoader = ServiceLocator.Get<ISceneLoader>();
    }

    private void OnEnable()
    {
        // 구독 갱신
        _sub = _sceneLoader.SubscribeProgress(SetProgress);
        _sceneLoader.OnBeforeSceneChanged += ShowAsync;
        _sceneLoader.OnAfterSceneChanged += HideAsync;
        // 바 애니메이션 루프 시작
        StartBarLoop();
    }

    private void OnDisable()
    {
        // 바 애니메이션 루프 중지
        StopBarLoop();
        // 구독 해제
        _sub?.Dispose();
        _sub = null;
        _sceneLoader.OnBeforeSceneChanged -= ShowAsync;
        _sceneLoader.OnAfterSceneChanged -= HideAsync;
        _sceneLoader = null;
    }

    public void SetProgress(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        _targetValue = Mathf.Max(_targetValue, ratio);
    }

    #region Show/Hide

    public void Show()
    {
        if (gameObject.activeSelf) return;
        
        gameObject.SetActive(true);
        ResetBar();
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        
        gameObject.SetActive(false);
    }

    public async UniTask ShowAsync(CancellationToken externalToken)
    {
        this.Log($"ShowAsync called", Logg.LoggingMode.Completed);
        await UniTask.WaitForEndOfFrame(cancellationToken: externalToken);
        Show();
    }

    public async UniTask HideAsync(CancellationToken externalToken)
    {
        await UniTask.WaitForEndOfFrame(cancellationToken: externalToken);
        Hide();
    }

    #endregion

    #region Handle Bar Loop

    private void ResetBar()
    {
        _displayValue = 0f;
        _targetValue = 0f;
        if (slider != null) slider.value = 0f;
    }

    private void StartBarLoop()
    {
        StopBarLoop();

        _animCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        BarLoop(_animCts.Token).Forget();
    }

    private void StopBarLoop()
    {
        if (_animCts == null) return;
        _animCts.Cancel();
        _animCts.Dispose();
        _animCts = null;
    }

    private async UniTask BarLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token).SuppressCancellationThrow();

            if (slider == null) continue;
            if (!gameObject.activeSelf) continue;
            
            _displayValue = Mathf.MoveTowards(_displayValue, _targetValue, FollowSpeed * Time.unscaledDeltaTime);
            slider.value = _displayValue;
        }
    }

    #endregion
    
}
