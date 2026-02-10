using System.Collections.Generic;
using UnityEngine;

namespace TH.Utils
{
    // 플로팅 텍스트 이벤트 바인딩과 배치 출력 기능 계약 인터페이스
    public interface IFloatingTextSpawner
    {
        // 소스 객체 이벤트를 지정 타입 플로팅 텍스트 출력 루틴에 연결
        void Register(object source, FloatingTextEventType eventType);
        // 소스 객체 이벤트를 지정 타입 플로팅 텍스트 출력 루틴에서 해제
        void UnRegister(object source, FloatingTextEventType eventType);

        // 문자열 목록 기반 배치 플로팅 텍스트 출력 요청
        void SpawnBatch(
            FloatingTextEventType eventType,
            Transform anchor,
            IReadOnlyList<string> values,
            FloatingTextBatchLayout layout = FloatingTextBatchLayout.Line);
        // 수치 목록 기반 배치 플로팅 텍스트 출력 요청
        void SpawnBatch(
            FloatingTextEventType eventType,
            Transform anchor,
            IReadOnlyCollection<float> values,
            FloatingTextBatchLayout layout = FloatingTextBatchLayout.Line);
    }

    // 배치 텍스트 배치 방식 열거형
    public enum FloatingTextBatchLayout
    {
        // 중앙 기준 양옆 분산 + 상향 적층 배치 방식
        Spread,
        // 한 열 기준 위쪽 적층 배치 방식
        Line,
    }

    // 플로팅 텍스트 이벤트 타입 열거형
    public enum FloatingTextEventType
    {
        // 피해 이벤트 텍스트 타입
        Damage,
        // 회복 이벤트 텍스트 타입
        Heal,
        // 경험치 획득 이벤트 텍스트 타입
        GetXp,
    }
}

