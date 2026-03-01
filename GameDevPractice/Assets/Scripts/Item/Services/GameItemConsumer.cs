using Cysharp.Threading.Tasks;
using TH.Resource;
using UnityEngine;
using TH.Core.Service;

namespace TH.Item
{
    public interface IGameItemConsumer
    {
        bool TryConsume(IConsumableItemStorage storage,IGameItemSlot slot,object user = null,int amount = 1);
        bool TryConsume(IConsumableItemStorage storage, ItemTypeSO itemInfo, object user = null, int amount = 1);
    }

    public class GameItemConsumer : IGameItemConsumer
    {
        public bool TryConsume(
            IConsumableItemStorage storage,
            IGameItemSlot slot,
            object user = null,
            int amount = 1)
        {
            // 1) 슬롯 / 아이템 유효성 검사
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: { } item, GetItemInfo: { } info })
                return false;
            if (!info.isUsable)
                return false;

            // 2) 컨텍스트 구성
            int useAmount = Mathf.Max(1, amount);
            var ctx = new ItemUseContext(user, info, useAmount);

            // 3) 저장소 내부 아이템 소비 처리
            if (!storage.TryConsume(slot, useAmount))
                return false;

            // 4) 연결된 Effect들 실행
            var effects = info.itemUseEffects;
            if (effects == null || effects.Count == 0)
            {
                // 데이터 누락: 로그만 찍고 실패 반환하거나, 그냥 true로 보고 소비만 할지 선택
                Debug.LogWarning($"[GameItemConsumer] {info.name} has no effects.");
                return false;
            }

            foreach (var effect in effects)
            {
                if (effect == null) continue;
                if (!effect.TryApply(ctx)) return false;
                // todo: 현재 하나라도 실패하면 전체 실패로 판정 (아이템 효과 일부만 적용되고 소비 판정은 안 될 수 있음) -> 롤백 로직 추가 필요
            }

            if (info.HasItemUseSfx)
            {
                PlayItemUseSFX(info.ItemUseSfx);
                // if (info.ItemUseSFX is {} sfx && sfx.IsAlive())
                //     PlayItemUseSFX(sfx);
                // else info.InitializeAsync().ContinueWith(() => {SoundManager.Instance.Play(Enums.AudioType.Effect, info.ItemUseSFX);});
            }
            return true;
        }

        public bool TryConsume(IConsumableItemStorage storage, ItemTypeSO itemInfo, object user = null, int amount = 1)
        {
            // 1) 아이템 데이터 유효성 검사
            if (!itemInfo.IsNotNull() || !itemInfo.isUsable)
                return false;
            
            // 2) 컨텍스트 구성
            int useAmount = Mathf.Max(1, amount);
            var ctx = new ItemUseContext(user, itemInfo, useAmount);

            // 3) 저장소 내부 아이템 소비 처리
            if (!storage.TryConsume(itemInfo, useAmount))
                return false;

            // 4) 연결된 Effect들 실행
            var effects = itemInfo.itemUseEffects;
            if (effects == null || effects.Count == 0)
            {
                // 데이터 누락: 로그만 찍고 실패 반환하거나, 그냥 true로 보고 소비만 할지 선택
                Debug.LogWarning($"[GameItemConsumer] {itemInfo.name} has no effects.");
                return false;
            }

            foreach (var effect in effects)
            {
                if (effect == null) continue;
                if (!effect.TryApply(ctx)) return false;
                // todo: 현재 하나라도 실패하면 전체 실패로 판정 (아이템 효과 일부만 적용되고 소비 판정은 안 될 수 있음) -> 롤백 로직 추가 필요
            }

            return true;
        }

        #region Helper Methods

        private void PlayItemUseSFX(AudioClip audio) => SoundManager.Instance.Play(Enums.AudioType.Effect, audio);

        #endregion
    }
}
