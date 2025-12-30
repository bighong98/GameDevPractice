using UnityEngine;
using System;

public interface IGameScanner<out T> where T : Component
{
    T LastTarget { get; }
    event Action<T> OnTargetFound;
    T Scan(Func<T, bool> additionalPredicate = null);

    void SetRadius(float radius);
    void SetRayCount(int rayCount);
    void SetOriginHeightOffset(float offset);
    void SetTriggerInteraction(QueryTriggerInteraction interaction);
}

public class GameScanner<T> : IGameScanner<T> where T : Component
{
    private readonly Component _owner;
    private readonly Transform _transform;
    
    private readonly int _targetMask;
    
    private readonly RaycastHit[] _hitsBuffer = new RaycastHit[16];
    
    public T LastTarget { get; private set; } // 마지막으로 찾은 타겟 캐시
    public event Action<T> OnTargetFound; // 타겟 갱신 시 호출
    
    private float _radius = 8f;
    private int _rayCount = 24; // 360도 분할 개수
    private float _originHeightOffset = 0.8f; // 발밑/지면 충돌을 피하기 위한 오프셋
    private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;
    
    public GameScanner(Component owner, LayerMask targetMask)
    {
        _owner = owner;
        _transform = owner.transform;
        _targetMask = targetMask;
    }
    
    // 탐색 반경 설정
    public void SetRadius(float radius)
    {
        _radius = Mathf.Max(0f, radius);
    }

    // Ray 개수(360도 샘플링 분할) 설정
    public void SetRayCount(int rayCount)
    {
        _rayCount = Mathf.Clamp(rayCount, 4, 256);
    }

    // Ray 시작 높이 오프셋
    public void SetOriginHeightOffset(float offset)
    {
        _originHeightOffset = Mathf.Max(0f, offset);
    }

    // Trigger 처리 방식
    public void SetTriggerInteraction(QueryTriggerInteraction interaction)
    {
        _triggerInteraction = interaction;
    }
    
    public T Scan(Func<T, bool> additionalPredicate = null)
    {
        if (_targetMask == 0) return null;

        Vector3 origin = _transform.position + Vector3.up * _originHeightOffset;

        T best = null;
        float bestDistSqr = float.PositiveInfinity;

        // owner의 forward를 기준으로 360도 샘플링
        for (int i = 0; i < _rayCount; i++)
        {
            float t = i / (float)_rayCount;
            float rad = t * Mathf.PI * 2f;

            // XZ 평면 기준 원형 방향
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                dir,
                _hitsBuffer,
                _radius,
                _targetMask,
                _triggerInteraction);

            for (int h = 0; h < hitCount; h++)
            {
                var hit = _hitsBuffer[h];
                if (hit.collider == null) continue;

                if (!hit.collider.TryGetComponent(out T candidate)) continue;
                if (candidate.gameObject == _owner.gameObject) continue;

                if (additionalPredicate != null && !additionalPredicate(candidate))
                    continue;

                float d2 = (hit.point - origin).sqrMagnitude;
                if (d2 < bestDistSqr)
                {
                    bestDistSqr = d2;
                    best = candidate;
                }
            }
        }

        if (best != null)
        {
            LastTarget = best;
            OnTargetFound?.Invoke(best);
        }

        return best;
    }
}
