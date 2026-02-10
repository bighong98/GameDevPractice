using UnityEngine;

namespace TH.UI.Data
{
    // 풀링된 플로팅 텍스트 단일 인스턴스 제어 계약 인터페이스
    public interface IFloatingTextController
    {
        // 설정 데이터와 텍스트를 동시에 지정하는 편의 진입점
        void Set(FloatingTextSO data, string text);
        // 애니메이션/색상/폰트 설정 데이터 교체
        void SetSetting(FloatingTextSO data);
        // 단일 텍스트 콘텐츠 갱신
        void SetText(string text);
    }
}

