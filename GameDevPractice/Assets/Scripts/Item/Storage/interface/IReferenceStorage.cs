
namespace TH.Item.Storage
{
    // 실제 아이템을 저장하는 대신 다른 저장소의 아이템 참조 혹은 데이터(ItemTypeSO)만 보관
    // ItemTypeSO를 가지고 있는 경우 원본 아이템의 ItemTypeSO 보관 목적의 가짜 아이템 인스턴스를 생성해서 관리
    public interface IReferenceStorage
    {
        // 아이템 등록
        bool TryStoreReference(IGameItem item);

        // 인덱스 슬롯에 아이템 등록
        bool TryStoreReference(IGameItem item, int index);

        // 인덱스 슬롯에 등록된 아이템 등록 해제
        bool TryRemoveReference(int index);
    }
}
