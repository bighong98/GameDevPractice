using System;
using System.Collections;
using TH.Core.Pool;
using TH.UI.Data;
using TMPro;
using UnityEngine;
using TH.Core.Service;

public class DamageTextController : MonoBehaviour, IPoolObject, IFloatingTextController
{
    [SerializeField] private TextMeshPro text;
    [Header("Animation")]
    [SerializeField] private FloatingTextSO animationData;

    private Transform textTrs;
    private IFloatingTextData AnimationData => animationData;
    
    private Coroutine running; // 재사용 대비 캐시
    
    private Camera cam;
    
    public void SetText(string s)
    {
        textTrs.localPosition = AnimationData.StartOffset;
        
        text.enabled = true;
        text.color = AnimationData.TextColor;
        
        text.SetText(s);
        text.havePropertiesChanged = true;
        text.ForceMeshUpdate();
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

        KillCoroutine();
        running = StartCoroutine(Co_FloatAndFade()); // 애니메이션 효과 코루틴 시작
    }

    private void Awake()
    {
        cam = Camera.main;
        if (text == null && Util.FindChild<TextMeshPro>(gameObject, "text", true) is { } found)
        {
            text = found;
        }

        textTrs = text.transform;
    }

    private void LateUpdate()
    {
        LookAtCamera();
    }

    private void LookAtCamera()
    {
        var rotation = cam.transform.rotation;
        textTrs.LookAt(textTrs.position + rotation * Vector3.forward,
            rotation * Vector3.up);
    }

    private void KillCoroutine()
    {
        if (running == null) return;
        
        StopCoroutine(running);
        running = null;
    }

    public GameObject Origin { get; set; }
    public void OnCreateFromPool()
    {
        
    }

    public void OnGetFromPool()
    {
        
    }

    public void OnReleaseFromPool()
    {
        KillCoroutine(); // 재사용을 위해 코루틴 정리

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
        KillCoroutine();
    }

    public void ReleaseSelf()
    {
        PoolManager.Instance.ReleaseFromPool(this);
    }

    private IEnumerator Co_FloatAndFade()
    {
        float t = 0f;

        float fDuration = AnimationData.FadeOutDuration;
        float lTime = AnimationData.LifeTime;
        float fadeStart = Mathf.Max(0f, lTime - fDuration); // 페이드 구간 계산(끝에서부터 fadeOutDuration만큼)
        
        Color c = text.color;
        
        while (t < lTime)
        {
            float dt = Time.unscaledDeltaTime;
            t = Mathf.Min(t + dt, lTime);
            
            // 1) 위로 이동
            textTrs.localPosition += Vector3.up * (AnimationData.RiseSpeed * dt); 
            
            if (text == null) break;
            // 2) 페이드아웃 (마지막 fadeOutDuration(초) 만큼만)
            if (t >= fadeStart && fDuration > 0f)
            {
                float u = Mathf.InverseLerp(fadeStart, lTime, t); // 0→1
                c.a = Mathf.Lerp(1f, 0f, u);
                text.color = c;
            }

            yield return null;
        }

        running = null;
        ReleaseSelf(); // 수명 종료 시 풀에 반납
    }

    public void Set(FloatingTextSO data, string text)
    {
        SetSetting(data);
        SetText(text);
    }

    public void SetSetting(FloatingTextSO data)
    {
        animationData = data;
    }
}
