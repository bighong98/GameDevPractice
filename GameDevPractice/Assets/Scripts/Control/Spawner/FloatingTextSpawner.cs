using System;
using System.Collections.Generic;
using System.Globalization;
using TH.Attribute;
using TH.Combat;
using TH.Core.Pool;
using TH.Resource;
using UnityEngine;
using UnityEngine.Pool;

namespace TH.Utils
{   
    // 인게임 텍스트 오브젝트 출력 시스템
    // 텍스트 출력 가능성이 있는 오브젝트 개수 * 컴포넌트 개수 만큼의 이벤트 인스턴스가 생성됨
    // -> 이벤트 파라미터가 구조체인 경우 이벤트 호출마다 값 복사가 발생
    // -> 구조체에 값 형식 데이터가 많아질 경우 event Action 대신 커스텀 델리게이트 사용 + in 키워드 사용하여 수정 필요
    public class FloatingTextSpawner : IFloatingTextSpawner
    {
        private readonly Dictionary<FloatingTextEventType, IFloatingTextEventBinder> _binders = new();

        private IObjectPool<IPoolObject> damageTextPool;
        private GameObject damageTextPrefab;

        private const string damageTextPrefabKey = "DamageText";
        
        public FloatingTextSpawner(IResourceLoader resourceLoader)
        {
            AddBinders();
            
            resourceLoader.NotifyResourceLoad += (label) =>
            {
                if (!string.Equals(label, resourceLoader.PreLoadLabel)) return;
                if (!resourceLoader.TryLoad(damageTextPrefabKey, out damageTextPrefab))
                {
                    Logg.LogError($"[{nameof(FloatingTextSpawner)}] failed to get damage text prefab");
                    return;
                }
                // todo: PoolManager 대신 ServiceProvider/BootStrapper에서 초기화하는 서비스 사용 고려
                // todo: 혹은 오브젝트 풀 자체를 생성자에서 주입 고려
                if (PoolManager.Instance.GetPool(damageTextPrefab) is not { } result)
                {
                    Logg.LogError($"[{nameof(FloatingTextSpawner)}] failed to get damage text pool");
                    return;
                }
                
                damageTextPool = result;
            };
        }

        private void AddBinders()
        {
            AddBinder<IDamageable, HitResult, HitEvent>(
                FloatingTextEventType.Damage,
                (subject, h) => subject.OnDamaged += h,
                (subject, h) => subject.OnDamaged -= h,
                (IDamageable subject, in HitResult data) => ShowDamageText(AnchorOf(subject), in data),
                adapter: ph => new HitEvent((in HitResult x) => ph(in x)) 
            );
            
            AddBinder<IExperience, float, Action<float>>(
                FloatingTextEventType.GetXp,
                (subject, h) => subject.OnXpChanged += h,
                (subject, h) => subject.OnXpChanged -= h,
                (IExperience subject, in float v) => ShowExpText(AnchorOf(subject), v),
                adapter: ph => (float v) => ph(in v)
            );
        }

        private static Transform AnchorOf(object s) => (s as Component)?.transform;

        private void AddBinder<TSource, TPayload, TEvent>(
            FloatingTextEventType type,
            Action<TSource, TEvent> subscribe,
            Action<TSource, TEvent> unsubscribe,
            OnEventHandler<TSource, TPayload> onEvent,
            Func<PayloadHandler<TPayload>, TEvent> adapter)
            where TSource : class
            where TEvent : Delegate
        {
#if UNITY_EDITOR
            if (_binders.ContainsKey(type)) { Logg.Log($"[FloatingText] Binder for {type} is being overwritten.", Logg.LoggingMode.InProgress); }
#endif
            
            _binders[type] = new FloatingTextEventBinder<TSource, TPayload, TEvent>(
                subscribe, unsubscribe, onEvent, adapter);
        }

        #region Register/UnRegister (IFloatingTextSpawner)

        public void Register(object source, FloatingTextEventType type)
        {
            if (_binders.TryGetValue(type, out var b)) b.Bind(source);
        }
        public void UnRegister(object source, FloatingTextEventType type)
        {
            if (_binders.TryGetValue(type, out var b)) b.Unbind(source);
        }
        
        public void UnregisterAll(object source)
        {
            foreach (var b in _binders.Values) b.Unbind(source);
        }

        #endregion
        
        
        private void ShowDamageText(Transform anchor, in HitResult hr)
        {
            Logg.Log($"[FTSpawner] print damage ({anchor.name}, {hr.Damage})", Logg.LoggingMode.Completed);
            var s = PoolManager.Instance.GetFromPool<DamageTextController>(damageTextPrefab, null, anchor.position);
            s.SetText(hr.Damage.ToString(CultureInfo.InvariantCulture));
        }

        private void ShowExpText(Transform anchor, float value)
        {
            
        }

        private sealed class FloatingTextEventBinder<TSource, TPayload, TEvent> : IFloatingTextEventBinder 
            where TSource : class
            where TEvent : Delegate
        {
            private readonly Func<object, TSource> _tryCast;
            private readonly Action<TSource, TEvent> _subscribe;
            private readonly Action<TSource, TEvent> _unsubscribe;
            private readonly OnEventHandler<TSource, TPayload> _onEvent;
            private readonly Func<PayloadHandler<TPayload>, TEvent> _adapter;
            
            private readonly Dictionary<TSource, TEvent> _handlers = new();
            
            public FloatingTextEventBinder(
                Action<TSource, TEvent> subscribe,
                Action<TSource, TEvent> unsubscribe,
                OnEventHandler<TSource, TPayload> onEvent,
                Func<PayloadHandler<TPayload>, TEvent> adapter)
            {
                _tryCast   = o => o as TSource;
                _subscribe = subscribe ?? throw new ArgumentNullException(nameof(subscribe));
                _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
                _onEvent   = onEvent ?? throw new ArgumentNullException(nameof(onEvent));
                _adapter   = adapter ?? throw new ArgumentNullException(nameof(adapter));
            }
            
            public void Bind(object o)
            {
                var src = _tryCast(o);
                if (src == null || _handlers.ContainsKey(src)) return;
                
                PayloadHandler<TPayload> ph = (in TPayload payload) => _onEvent(src, in payload);
                TEvent ev = _adapter(ph);

                _handlers[src] = ev;
                _subscribe(src, ev);
            }

            public void Unbind(object o)
            {
                var src = _tryCast(o);
                if (src == null) return;
                if (!_handlers.TryGetValue(src, out var ev)) return;

                _unsubscribe(src, ev);
                _handlers.Remove(src);
            }
        }
    }
    
    
    public delegate void PayloadHandler<TPayload>(in TPayload payload);
    public delegate void OnEventHandler<in TSource, TPayload>(TSource source, in TPayload payload);

    
    public interface IFloatingTextEventBinder
    {
        void Bind(object o);
        void Unbind(object o);
    }
}