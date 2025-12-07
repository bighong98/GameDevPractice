using UnityEngine;
using UnityEngine.Serialization;

namespace TH.UI.Data // todo: 네임스페이스 정리
{
    [CreateAssetMenu(fileName = "FloatingTextSO", menuName = "Scriptable Objects/FloatingTextSO")]
    public class FloatingTextSO : ScriptableObject, IFloatingTextData
    {
        [SerializeField] float riseSpeed = 1.5f;      // 위로 떠오르는 속도 (units/sec)
        [SerializeField] float lifeTime = 0.9f;       // 전체 표시 시간 (sec)
        [SerializeField] float fadeOutDuration = 0.35f; // 끝부분 페이드아웃 시간 (sec)
        [SerializeField] Vector3 startOffset = new Vector3(0f, 0.0f, 0f); // 스폰 시 위치 오프셋(선택)
        [FormerlySerializedAs("color")] [SerializeField] Color textColor;
        [SerializeField] float textSize;
        public float RiseSpeed => riseSpeed;
        public float LifeTime => lifeTime;
        public float FadeOutDuration => fadeOutDuration;
        public Vector3 StartOffset => startOffset;
        public Color TextColor => textColor;
        public float TextSize => textSize;
    }

    public interface IFloatingTextData
    {
        public float RiseSpeed { get; }
        public float LifeTime { get; }
        public float FadeOutDuration { get; }
        public Vector3 StartOffset { get; }
        public Color TextColor { get; }
        public float TextSize {get;}
    }
}


