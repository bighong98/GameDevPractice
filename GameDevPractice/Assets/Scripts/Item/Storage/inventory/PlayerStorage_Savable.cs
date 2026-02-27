using System.Collections.Generic;
using TH.Utils;
using UnityEngine.SceneManagement;

namespace TH.Item
{
    // 플레이어 인벤토리 저장/복원 규약 구현 partial
    public sealed partial class PlayerStorage
    {
        #region ISavable (save/load)

        private const string InventoryIdentifier = "playerInventory";
        // 저장 시스템 고정 식별자
        public string UniqueIdentifier => InventoryIdentifier;
        // 글로벌 저장 대상 여부
        public bool IsGlobal { get; } = true;
        // 레지스트리 등록 상태 플래그
        public bool IsRegistered {get; set;} = false;
        // 글로벌 대상 고정 씬 값
        public Scene TargetScene { get; } = default;

        // 현재 슬롯 상태 직렬화 데이터 생성
        public object CaptureState()
        {
            this.Log($"CaptureState", Logg.LoggingMode.Completed);
            List<IGameItem> items = new();

            foreach (var slot in slots)
            {
                if (slot is not { HasItem: true, GetItem: { } item }) continue;
                items.Add(item.Clone<IGameItem>());
            }

            return items;
        }

        // 저장 데이터 기반 슬롯 상태 복원
        public bool RestoreState(object state)
        {
            this.Log($"RestoreState", Logg.LoggingMode.Completed);

            Clear();

            List<IGameItem> items = ExtractSaveData(state);
            _hasRestoredState = items is { Count: > 0 };

            if (items is { Count: > 0 })
            {
                foreach (var item in items)
                {
                    TryStore(itemBuilder.GetItemFromData(item.GetItemInfo, item.GetAmount));
                }
            }

            NotifyStorageChanged();

            return true;
        }

        // 저장소 기본 상태 초기화
        public void ResetToDefaultState()
        {
            _hasRestoredState = false;
            Clear();
            NotifyStorageChanged();
        }

        // 저장 데이터 입력 형태 정규화 유틸리티
        private static List<IGameItem> ExtractSaveData(object state)
        {
            switch (state)
            {
                case List<IGameItem> l: return l;
                case Dictionary<string, object> stateDict:
                {
                    foreach (var s in stateDict.Values)
                        if (s is List<IGameItem> { } dl)
                            return dl;
                    break;
                }
            }
            return null;
        }

        #endregion
    }
}
