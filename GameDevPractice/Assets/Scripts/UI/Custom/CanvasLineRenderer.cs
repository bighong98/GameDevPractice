using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class CanvasLineRenderer : MaskableGraphic
{
    [Header("Settings")]
    [Tooltip("선의 두께")]
    public float thickness = 10f;       
    
    [Tooltip("점을 찍기 위한 최소 거리. 값이 대입되어있지 않으면 'MinDistanceFallback'값으로 로 대체 적용")]
    public float minDistance = 5f;

    [Tooltip("선의 최대 길이 (0 이하일 경우 제한 없음)")]
    public float maxLength = 500f;
    [Tooltip("텍스처 반복 밀도 (0이면 전체 길이에 1번 늘려서 매핑)")]
    public float textureTiling = 0f;

    [Header("Gradient")]
    [Tooltip("선 꼬리 부분의 페이드 아웃 효과 여부")]
    public bool useFadeOut = true;
    [Tooltip("페이드 아웃 길이 (0~1)")]
    [Range(0, 1)] public float fadeLength = 1.0f;

    [Header("Shrink")]
    [Tooltip("입력이 멈춘 뒤 꼬리가 줄어들기까지의 지연 시간")]
    public float shrinkDelay = 0.2f;

    [Tooltip("길이가 임계값 이상일 때 입력 여부와 상관없이 꼬리를 줄입니다")]
    [SerializeField] private bool alwaysShrinkWhenLonger;

    [Tooltip("상시 줄이기 시작 전 지연 시간")]
    [SerializeField] private float alwaysShrinkDelay = 0f;
    [Tooltip("상시 줄이기 속도")]
    [SerializeField] private float alwaysShrinkSpeed = 400f;
    [Tooltip("상시 줄이기 시작하는 길이")]
    [SerializeField] private float alwaysShrinkMinLength = 400;
    [Tooltip("초당 줄어드는 길이")]
    public float shrinkSpeed = 800f;

    [Header("Tail Taper")]
    [Tooltip("테이퍼를 적용할 최소 선 길이")]
    public float tailTaperMinLength = 100f;
    [Tooltip("꼬리에서 뾰족해지는 길이 비율 (0~1)")]
    [Range(0, 1)] public float tailTaperLength = 0.25f;

    // 궤적 포인트 저장 리스트
    [Tooltip("버퍼에 저장할 최대 점 개수")]
    public int maxPoints = 1024;

    [Tooltip("RectTransformUtility에 사용하는 카메라")]
    [SerializeField] private Camera targetCamera;

    private Vector3[] points = new Vector3[0];
    private int head;
    private int count;
    private float headLengthOffset;


    private float persistentShrinkStartTime;
    private bool persistentShrinkActive;
    private float lastInputTime;

    private const float MinDistanceFallback = 0.01f;

    protected override void Awake()
    {
        base.Awake();
        if (minDistance <= 0f)
            minDistance = MinDistanceFallback;

        if (maxPoints < 2)
            maxPoints = 2;

        points = new Vector3[maxPoints];
        head = 0;
        count = 0;
        headLengthOffset = 0f;
        lastInputTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (!alwaysShrinkWhenLonger)
            persistentShrinkActive = false;

        if (count < 2) return;

        float totalDist = GetTotalLength();

        if (alwaysShrinkWhenLonger && !persistentShrinkActive && totalDist >= alwaysShrinkMinLength)
        {
            persistentShrinkActive = true;
            persistentShrinkStartTime = Time.unscaledTime;
        }

        float speed;
        if (persistentShrinkActive)
        {
            if (Time.unscaledTime - persistentShrinkStartTime <= alwaysShrinkDelay) return;
            speed = alwaysShrinkSpeed;
        }
        else
        {
            if (Time.unscaledTime - lastInputTime <= shrinkDelay) return;
            speed = shrinkSpeed;
        }

        if (speed <= 0f) return;

        float shrink = speed * Time.unscaledDeltaTime;
        if (shrink <= 0f) return;

        headLengthOffset += shrink;

        bool removed = false;
        while (count > 1 && points[head].z <= headLengthOffset)
        {
            head = (head + 1) % points.Length;
            count--;
            removed = true;
        }

        if (removed || count > 1)
            SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        persistentShrinkActive = false;
        persistentShrinkStartTime = 0f;
    }


    /// <summary>
    /// 외부(Input System 이벤트 등)에서 호출하여 궤적 포인트를 추가합니다.
    /// </summary>
    /// <param name="screenPosition">입력 장치의 스크린 좌표 (예: Mouse Position)</param>
    public void AddPoint(Vector2 screenPosition)
    {
        Vector2 localPoint;

        // ScreenSpace-Overlay 모드에서는 카메라(3번째 인자)에 null 사용 (현재는 Overlay 환경에서 사용 의도)
        // ScreenSpace-Camera 또는 WorldSpace라면 해당 캔버스의 worldCamera 사용
        // renderMode에 따라 SetCamera()를 통해 참조 전달해서 사용 의도
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPosition, targetCamera, out localPoint
        );

        // 첫 점이거나, 이전 점과의 거리가 최소 거리보다 멀 때만 추가
        if (count == 0 || (GetPoint2(count - 1) - localPoint).sqrMagnitude > minDistance * minDistance)
        {
            lastInputTime = Time.unscaledTime;

            int bufferLength = points.Length;
            if (count == bufferLength)
            {
                // 버퍼가 가득 차면 꼬리를 버리고 새 점을 추가합니다.
                head = (head + 1) % bufferLength;
                count--;
                headLengthOffset = points[head].z;
            }

            float cumulative = headLengthOffset;
            if (count > 0)
            {
                Vector3 last = GetPoint(count - 1);
                Vector2 last2 = new Vector2(last.x, last.y);
                cumulative = last.z + Vector2.Distance(last2, localPoint);
            }

            int insertIndex = GetIndex(count);
            points[insertIndex] = new Vector3(localPoint.x, localPoint.y, cumulative);
            count++;

            // 최대 길이 초과시 끝에서부터 자르기
            if (maxLength > 0) PruneTrail();
            // OnPopulateMesh 호출 예약 (UI 다시 그리기)
            SetVerticesDirty();
        }
    }


    /// <summary>
    /// 궤적을 초기화 (드래그가 끝났을 때 또는 시작할 때 호출)
    /// </summary>
    public void Clear()
    {
        count = 0;
        head = 0;
        headLengthOffset = 0f;
        lastInputTime = Time.unscaledTime;
        SetVerticesDirty();
    }

    public void SetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    private int GetIndex(int i)
    {
        return (head + i) % points.Length;
    }

    private int GetTailIndex()
    {
        return (head + count - 1) % points.Length;
    }

    private Vector3 GetPoint(int i)
    {
        return points[GetIndex(i)];
    }

    private Vector2 GetPoint2(int i)
    {
        Vector3 p = GetPoint(i);
        return new Vector2(p.x, p.y);
    }

    // 궤적 자르기 (꼬리부터)
    private void PruneTrail()
    {
        if (count < 2) return;

        // 1. 현재 전체 길이 계산
        float totalLength = GetTotalLength();

        // 2. 최대 길이를 초과하는 동안 꼬리를 통째로 자름
        while (totalLength > maxLength && count > 1)
        {
            head = (head + 1) % points.Length;
            count--;
            headLengthOffset = points[head].z;
            totalLength = GetTotalLength();
        }
    }

    // 현재 궤적의 총 길이를 계산
    private float GetTotalLength()
    {
        if (count < 2) return 0f;
        float total = points[GetTailIndex()].z - headLengthOffset;
        return Mathf.Max(0f, total);
    }
    // 궤적 메쉬 그리기
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (count < 2) return;

        float totalDist = GetTotalLength();
        float safeTotal = totalDist > 0f ? totalDist : 1f;

        for (int i = 0; i < count; i++)
        {
            Vector3 point3 = GetPoint(i);
            Vector2 point = new Vector2(point3.x, point3.y);
            float currentDistance = point3.z - headLengthOffset;

            // 1. 방향 벡터 및 Normal 계산
            Vector2 dir;
            if (i == 0) dir = (GetPoint2(1) - point).normalized;
            else if (i == count - 1) dir = (point - GetPoint2(i - 1)).normalized;
            else
            {
                Vector2 dirPrev = (point - GetPoint2(i - 1)).normalized;
                Vector2 dirNext = (GetPoint2(i + 1) - point).normalized;
                dir = (dirPrev + dirNext).normalized;
            }

            Vector2 normal = new Vector2(-dir.y, dir.x);

            float widthScale = 1f;
            if (totalDist >= tailTaperMinLength && tailTaperLength > 0f)
            {
                float taperRatio = currentDistance / (totalDist * tailTaperLength);
                widthScale = Mathf.Clamp01(taperRatio);
            }

            float halfWidth = thickness * 0.5f * widthScale;
            Vector2 p1 = point + normal * halfWidth;
            Vector2 p2 = point - normal * halfWidth;

            // 2. UV 계산
            float u = (textureTiling > 0) ? currentDistance / (thickness * textureTiling) : currentDistance / safeTotal;

            // 3. 색상 (Fade Out) 계산 [추가된 부분]
            Color currentVertColor = color;

            if (useFadeOut)
            {
                // 전체 길이 대비 현재 위치의 비율 (0: 꼬리, 1: 머리)
                float ratio = totalDist > 0f ? currentDistance / totalDist : 1f;

                // fadeLength 기준으로 알파값 계산
                // ratio가 fadeLength보다 작으면 투명해짐
                // 예: fadeLength가 0.5면, 뒤쪽 50% 구간에서 Alpha가 0->1로 변함
                float alpha = Mathf.Clamp01(ratio / fadeLength);
                currentVertColor.a *= alpha;
            }

            vh.AddVert(p1, currentVertColor, new Vector2(u, 1f));
            vh.AddVert(p2, currentVertColor, new Vector2(u, 0f));

            if (i > 0)
            {
                int currentBaseIndex = i * 2;
                int prevBaseIndex = (i - 1) * 2;
                vh.AddTriangle(prevBaseIndex, currentBaseIndex, currentBaseIndex + 1);
                vh.AddTriangle(prevBaseIndex, currentBaseIndex + 1, prevBaseIndex + 1);
            }
        }
    }

}