using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Pool;
using TH.UI.Data;
using TH.Utils;
using TMPro;
using UnityEngine;

public class FloatingTextController : MonoBehaviour, IPoolObject, IFloatingTextController
{
    [SerializeField] private TextMeshPro text;
    [Header("Animation")]
    [SerializeField] private FloatingTextSO animationData;

    private Transform textTrs;
    private IFloatingTextData AnimationData => animationData;
    
    private CancellationTokenSource _animCts;
    
    private Camera cam;

    private void Awake()
    {
        if (text == null && Util.FindChild<TextMeshPro>(gameObject, "text", true) is { } found)
        {
            text = found;
        }

        textTrs = text.transform;
    }

    private void OnEnable()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        LookAtCamera();
    }
    
    #region IFloatingTextController

    public void Set(FloatingTextSO data, string text)
    {
        SetSetting(data);
        SetText(text);
    }

    public void SetSetting(FloatingTextSO data)
    {
        animationData = data;
    }

    public void SetText(string s)
    {
        text.SetText(s);
        Refresh();
    }

    #endregion

    private void Refresh()
    {
        // 텍스트 설정(SO) 값 적용
        ApplySetting();
        // 텍스트 매쉬 업데이트
        text.enabled = true;
        text.havePropertiesChanged = true;
        text.ForceMeshUpdate();
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

        // 기존 애니메이션 UniTask 취소
        CancelAnimationTask();
        // 새 애니메이션 시작
        StartFloatAndFadeTask();
    }

    private void ApplySetting()
    {
        textTrs.localPosition = AnimationData.StartOffset;
        text.color = AnimationData.TextColor;
        text.fontSize = AnimationData.TextSize;
    }

    private void LookAtCamera()
    {
        var rotation = cam.transform.rotation;
        textTrs.LookAt(textTrs.position + rotation * Vector3.forward,
            rotation * Vector3.up);
    }


    #region IPoolObject
    public GameObject Origin { get; set; }
    public void OnCreateFromPool() {}
    public void OnGetFromPool() {}

    public void OnReleaseFromPool()
    {
        CancelAnimationTask();

        // 위치/색상 원복
        textTrs.localPosition = AnimationData.StartOffset;
        if (text)
        {
            text.enabled = false;
            text.color = AnimationData.TextColor;
        }
    }

    public void OnDestroyFromPool()
    {
        CancelAnimationTask();
    }

    public void ReleaseSelf()
    {
        if (Util.IsQuitting) return;
        if (!gameObject.activeSelf) return;
        
        PoolManager.Instance.ReleaseFromPool(this);
    }

    #endregion

    private void CancelAnimationTask()
    {
        if (_animCts == null) return;

        try
        {
            if (!_animCts.IsCancellationRequested)
                _animCts.Cancel();
        }
        catch (Exception) { Logg.LogWarning($"invalid animation CTS"); }
        finally
        {
            _animCts.Dispose();
            _animCts = null;
        }
    }

    private void StartFloatAndFadeTask()
    {
        // 오브젝트 파괴 시 자동 취소되도록 Destroy 토큰과 링크
        var destroyToken = this.GetCancellationTokenOnDestroy();
        _animCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);

        // fire-and-forget
        FloatAndFadeAsync(_animCts.Token).Forget();
    }

    private async UniTask FloatAndFadeAsync(CancellationToken token)
    {
        float t = 0f;

        float fDuration = AnimationData.FadeOutDuration;
        float lTime = AnimationData.LifeTime;
        float fadeStart = Mathf.Max(0f, lTime - fDuration); // 페이드 구간 계산(끝에서부터 fadeOutDuration만큼)
        
        Color c = text.color;

        // 위로 떠오르면서 페이드
        while (t < lTime)
        {
            if (token.IsCancellationRequested)
                break;

            float dt = Time.unscaledDeltaTime;
            t = Mathf.Min(t + dt, lTime);
            
            // 위로 이동
            if (textTrs != null)
                textTrs.localPosition += Vector3.up * (AnimationData.RiseSpeed * dt); 
            
            if (text == null)
                break;

            // 페이드아웃 (마지막 fadeOutDuration(초) 만큼만)
            if (t >= fadeStart && fDuration > 0f)
            {
                float u = Mathf.InverseLerp(fadeStart, lTime, t); // 0to1
                c.a = Mathf.Lerp(1f, 0f, u);
                text.color = c;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        }

        ReleaseSelf();
    }
}
