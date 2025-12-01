using UnityEngine;

namespace TH.Item
{
    public abstract class ItemEffectBase : ScriptableObject, IGameItemEffect
    {
        // 공용 로직을 구현 목적의 추상 레이어
        public abstract bool TryApply(in ItemUseContext context);
    }

    public interface IGameItemEffect
    {
        bool TryApply(in ItemUseContext context);
    }
}

