using UnityEngine;
using UnityEngine.Serialization;

namespace TH.UI.Data // todo: 네임스페이스 정리
{
    // 플로팅 텍스트 1개 프리셋의 이동/생존/색상/크기 데이터 SO
    [CreateAssetMenu(fileName = "FloatingTextSO", menuName = "Scriptable Objects/UI/FloatingTextSO")]
    public class FloatingTextSO : ScriptableObject, IFloatingTextData
    {
        // 텍스트 상승 속도 값 units/sec 기준
        [SerializeField] float riseSpeed = 1.5f;
        // 텍스트 전체 생존 시간 값 sec 기준
        [SerializeField] float lifeTime = 0.9f;
        // 생존 종료 직전 투명도 감소 구간 길이 sec 기준
        [SerializeField] float fadeOutDuration = 0.35f;
        // 월드 앵커 위치 기준 초기 표시 오프셋 값
        [SerializeField] Vector3 startOffset = new Vector3(0f, 0.0f, 0f);
        // 직렬화 필드명 변경 이력 호환 유지용 attribute
        [FormerlySerializedAs("color")] [SerializeField] Color textColor;
        // 기본 텍스트 폰트 크기 값
        [SerializeField] float textSize;

        // 상승 속도 프로퍼티
        public float RiseSpeed => riseSpeed;
        // 생존 시간 프로퍼티
        public float LifeTime => lifeTime;
        // 페이드아웃 시간 프로퍼티
        public float FadeOutDuration => fadeOutDuration;
        // 시작 오프셋 프로퍼티
        public Vector3 StartOffset => startOffset;
        // 텍스트 색상 프로퍼티
        public Color TextColor => textColor;
        // 텍스트 크기 프로퍼티
        public float TextSize => textSize;
    }

    // 플로팅 텍스트 컨트롤러가 소비하는 최소 데이터 계약 인터페이스
    public interface IFloatingTextData
    {
        // 상승 속도 값
        public float RiseSpeed { get; }
        // 생존 시간 값
        public float LifeTime { get; }
        // 페이드아웃 시간 값
        public float FadeOutDuration { get; }
        // 시작 오프셋 값
        public Vector3 StartOffset { get; }
        // 텍스트 색상 값
        public Color TextColor { get; }
        // 텍스트 폰트 크기 값
        public float TextSize {get;}
    }
}


