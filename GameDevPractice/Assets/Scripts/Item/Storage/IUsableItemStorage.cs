using UnityEngine;

namespace TH.Item
{
    public interface IUsableItemStorage // 내부 아이템을 사용(소비, 장착/장착해제 등)하는 것이 허가된 스토리지
    {
        bool TryUseItem(int index, object user); // index로 접근, 사용자 전달 및 사용 시도 성공 여부 반환
        bool TryUseItem(int index); // 구현 클래스에서 설정한 기본 사용자 전달
    }
}

