// 상태 액션 실행 계약 인터페이스 스크립트
using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    // 상태 진입 시 실행되는 캐릭터 액션 계약 인터페이스
    public interface ICharacterAction
    {
        void Execute(IActionStateController controller);
    }
}

