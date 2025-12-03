using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TH.Utils
{
    // <키(참조 타입 제약) - 이벤트 헨들러 델리게이트> 바인딩 헬퍼 클래스
    // 람다 형식 이벤트 핸들러를 사용해야할 때 key 기반으로 이벤트 핸들러(델리게이트) 캐싱 및 중복 등록 방지
    // Register(TKey, Action<TValue>) 호출 시 델리게이트 누적 방지를 위해 내부적으로 UnRegister(TKey)를 호출함

    /// <summary> 사용 예시
    /// 선언
    /// _hoverEnterRegistry = new EventHandlerRegistry<IHoverableStorageUI, int>(
    ///     adder: (ui, handler) => ui.OnSlotHovered += handler,
    ///     remover: (ui, handler) => ui.OnSlotHovered -= handler
    /// );
    /// 델리게이트 등록
    /// private void SubscribeHoverEnterEvent(IHoverableStorageUI sourceUI)
    /// {
    ///     _hoverEnterRegistry.Register(sourceUI, (index) => OnSlotHovered(sourceUI, index));
    /// }
    /// </summary>
    
    public sealed class EventHandlerRegistry<TKey, TValue> //: IEventHandlerRegistry<TKey, TValue> 
        where TKey : class 
    {
        public EventHandlerRegistry(Action<TKey, Action<TValue>> adder, Action<TKey, Action<TValue>> remover) {
            _adder = adder;
            _remover = remover;
        }
        
        // event.Add/Remove 프로퍼티를 델리게이트 형식으로 캐싱
        private readonly Action<TKey, Action<TValue>> _adder;
        private readonly Action<TKey, Action<TValue>> _remover;
        // 이벤트 핸들러 캐시
        private readonly Dictionary<TKey, Action<TValue>> handlers = new();
        private IReadOnlyDictionary<TKey, Action<TValue>> readonlyHandlers;
        public IReadOnlyDictionary<TKey, Action<TValue>> EventHandlers 
            => readonlyHandlers ??= new ReadOnlyDictionary<TKey, Action<TValue>>(handlers);

        // 이벤트 핸들러 캐시 확인
        public bool TryGet(TKey key, out Action<TValue> result)
        {
            return handlers.TryGetValue(key, out result);
        }
        // 신규 이벤트 핸들러 구독 및 캐싱
        public void Register(TKey key, Action<TValue> value)
        {
            UnRegister(key);
            handlers[key] = value;
            _adder(key, value);
        }
        // 기존 이벤트 핸들러 구독 해제 및 캐싱 해제
        public void UnRegister(TKey key)
        {
            if (!handlers.TryGetValue(key, out var cached)) 
                return;

            handlers.Remove(key);
            _remover(key, cached);
        }
        // 모든 이벤트 핸들러 구독 해제 처리 및 내부 캐시 정리
        public void Clear()
        {
            foreach (var (key, handler) in handlers)
            {
                if (!key.IsAlive()) continue;
                _remover(key, handler);
            }

            handlers.Clear();
        }
    }

    // public interface IEventHandlerRegistry<TKey, TValue> 
    //     where TKey : class 
    // {
    //     IReadOnlyDictionary<TKey, Action<TValue>> EventHandlers { get; }
    //     void Register(TKey key, Action<TValue> value);
    //     void UnRegister(TKey key);
    // }

}
