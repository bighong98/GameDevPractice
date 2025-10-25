using System;
using System.Collections;
using TH.Core.Pool;
using TH.Utils;
using TMPro;
using UnityEngine;

public class DamageTextHandler : MonoBehaviour, IPoolObject, IFloatingTextHandler
{
    [Header("Animation")]
    [SerializeField] private float riseSpeed = 1.5f;      // 위로 떠오르는 속도 (units/sec)
    [SerializeField] private float lifeTime = 0.9f;       // 전체 표시 시간 (sec)
    [SerializeField] private float fadeOutDuration = 0.35f; // 끝부분 페이드아웃 시간 (sec)
    [SerializeField] private Vector3 startOffset = new Vector3(0f, 0.0f, 0f); // 스폰 시 위치 오프셋(선택)
    
    // 재사용 대비 캐시
    private Coroutine running;
    private Vector3 startLocalPos;
    private Color32 baseColor;
    
    private TextMeshPro text;
    private Camera cam;
    
    public void SetText(string s)
    {
        text.SetText(s);
    }

    private void LateUpdate()
    {
        var rotation = cam.transform.rotation;
        transform.LookAt(transform.position + rotation * Vector3.forward,
            rotation * Vector3.up);
    }

    public GameObject Origin { get; set; }
    public void OnCreateFromPool()
    {
        cam = Camera.main;
        if (Util.FindChild<TextMeshPro>(gameObject, "text") is { } found)
        {
            text = found;
            baseColor = text.color;
        }
        startLocalPos = transform.localPosition;
    }

    public void OnGetFromPool()
    {
        if (running != null) StopCoroutine(running);
        if (text)
        {
            text.enabled = true;
            // 알파=1로 리셋
            var c = text.color; c.a = 255; text.color = c;
        }

        // 시작 위치 리셋 + 오프셋 적용
        transform.localPosition = startLocalPos + startOffset;

        // 애니메이션 시작
        running = StartCoroutine(Co_FloatAndFade());
    }

    public void OnReleaseFromPool()
    {
        // 재사용을 위해 상태 정리
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        // 위치/색상 원복
        transform.localPosition = startLocalPos;
        if (text)
        {
            text.color = baseColor;
            text.enabled = false; // 풀에 들어갈 땐 꺼두기(선택)
        }
    }

    public void OnDestroyFromPool()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }
    }

    public void ReleaseSelf()
    {
        PoolManager.Instance.ReleaseFromPool(this);
    }

    private IEnumerator Co_FloatAndFade()
    {
        float t = 0f;

        // 페이드 구간 계산(끝에서부터 fadeOutDuration만큼)
        float fadeStart = Mathf.Max(0f, lifeTime - fadeOutDuration);

        // 색상 캐시
        Color c = text ? (Color)text.color : Color.white;
        c.a = 1f;

        while (t < lifeTime)
        {
            float dt = Time.deltaTime;
            t += dt;

            // 1) 위로 이동
            transform.position += Vector3.up * (riseSpeed * dt);

            // 2) 페이드아웃 (끝부분에만)
            if (text)
            {
                if (t >= fadeStart && fadeOutDuration > 0f)
                {
                    float u = Mathf.InverseLerp(fadeStart, lifeTime, t); // 0→1
                    c.a = Mathf.Lerp(1f, 0f, u);
                    text.color = c;
                }
            }

            yield return null;
        }

        running = null;
        // 수명 종료 시 풀로 반납
        ReleaseSelf();
    }
}
