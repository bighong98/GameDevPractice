using System;
using System.Collections.Generic;
using System.Globalization;
using TH.Attribute;
using TH.Combat;
using TH.Resource;
using TH.UI.Data;
using UnityEngine;
using TH.Core.Service;

namespace TH.Utils
{   
    // 인게임 텍스트 오브젝트 출력 시스템
    // 텍스트 출력 가능성이 있는 오브젝트 개수 * 컴포넌트 개수 만큼의 이벤트 인스턴스가 생성됨
    // -> 이벤트 파라미터가 구조체인 경우 이벤트 호출마다 값 복사가 발생
    // -> 구조체에 값 형식 데이터가 많아질 경우 event Action 대신 커스텀 델리게이트 사용 + in 키워드 사용하여 수정 필요
    public class FloatingTextSpawner : IFloatingTextSpawner
    {
        private readonly Dictionary<FloatingTextEventType, IFloatingTextEventBinder> _binders = new();
        
        private GameObject textPrefab;
        private FloatingTextCatalogSO textCatalogSO;

        private const string textPrefabKey = "FloatingText";
        private const string textCatalogSOKey = "FloatingTextCatalogSO";
        
        private readonly IResourceLoader resourceLoader;
        
        public FloatingTextSpawner(IResourceLoader rLoader)
        {
            AddBinders();
            resourceLoader = rLoader;
            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, InitializeTextPools);
        }

        private void InitializeTextPools()
        {
            if (!resourceLoader.TryLoad(textCatalogSOKey, out textCatalogSO))
            {
                Logg.LogError($"[{nameof(FloatingTextSpawner)}] failed to load text floating text CatalogSO");
                return;
            }

            if (!resourceLoader.TryLoad(textPrefabKey, out textPrefab))
            {
                Logg.LogError($"[{nameof(FloatingTextSpawner)}] failed to load text prefab");
                return;
            }
        }

        private void AddBinders()
        {
            var damageType = FloatingTextEventType.Damage;
            AddBinder<IDamageable, HitResult, HitEvent>(
                damageType,
                (subject, h) => subject.OnDamaged += h,
                (subject, h) => subject.OnDamaged -= h,
                (IDamageable subject, in HitResult data) => ShowFloatingText(damageType, AnchorOf(subject), in data.Damage),
                adapter: ph => new HitEvent((in HitResult x) => ph(in x)) 
            );
            var xpGainType = FloatingTextEventType.GetXp;
            AddBinder<IExperience, float, Action<float>>(
                xpGainType,
                (subject, h) => subject.OnXpGained += h,
                (subject, h) => subject.OnXpGained -= h,
                (IExperience subject, in float v) => ShowFloatingText(xpGainType, AnchorOf(subject), in v),
                adapter: ph => (float v) => ph(in v)
            );
            var healType = FloatingTextEventType.Heal;
            AddBinder<IHealable, float, Action<float>>(
                healType,
                (subject, h) => subject.OnHealed += h,
                (subject, h) => subject.OnHealed -= h,
                (IHealable subject, in float v) => ShowFloatingText(healType, AnchorOf(subject), in v),
                adapter: ph => (float v) => ph(in v)
            );
        }

        private static Transform AnchorOf(object s)
        {
            if (!s.IsNotNull()) return null;
            return (s as Component)?.transform;
        }

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

        private void ShowFloatingText(FloatingTextEventType type, Transform anchor, in float value)
        {
            ShowFloatingText(type, anchor, value.ToString(CultureInfo.InvariantCulture));
        }

        private void ShowFloatingText(FloatingTextEventType type, Transform anchor, in int value)
        {
            ShowFloatingText(type, anchor, value.ToString(CultureInfo.InvariantCulture));
        }

        private void ShowFloatingText(FloatingTextEventType type, Transform anchor, in string str)
        {
            Logg.Log($"[FTSpawner] print {type} ({anchor.name}, {str} using {textPrefab})", Logg.LoggingMode.Completed);
            var s = PoolManager.Instance.GetFromPool<FloatingTextController>(textPrefab, null, anchor.position);
            if (textCatalogSO.TryGetValue(type, out var setting))
            {
                s.SetSetting(setting);
                s.SetText(str);
            }
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