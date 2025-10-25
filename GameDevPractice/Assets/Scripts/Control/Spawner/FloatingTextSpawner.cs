using System;
using System.Collections.Generic;
using TH.Attribute;
using TH.Combat;
using UnityEngine;

namespace TH.Utils
{   
    
    public class FloatingTextSpawner : IFloatingTextSpawner
    {
        private readonly Dictionary<FloatingTextEventType, IFloatingTextEventBinder> _binders = new();

        public FloatingTextSpawner()
        {
            AddBinders();
        }

        private void AddBinders()
        {
            AddBinder<IDamageable, HitResult>(
                FloatingTextEventType.Damage,
                (subject, action) => subject.OnDamaged += action,
                (subject, action) => subject.OnDamaged -= action,
                (subject, data) => ShowDamageText(AnchorOf(subject), data)
            );

            AddBinder<IExperience, float>(
                FloatingTextEventType.GetXp,
                (subject, action) => subject.OnXpChanged += action,
                (subject, action) => subject.OnXpChanged -= action,
                (subject, data) => ShowExpText(AnchorOf(subject), data)
            );
        }

        private static Transform AnchorOf(object s) => (s as Component)?.transform;

        private void AddBinder<TSource, TPayload>(
            FloatingTextEventType type,
            Action<TSource, Action<TPayload>> subscribe,
            Action<TSource, Action<TPayload>> unsubscribe,
            Action<TSource, TPayload> onEvent)
            where TSource : class
        {
#if UNITY_EDITOR
            if (_binders.ContainsKey(type)) { Logg.Log($"[FloatingText] Binder for {type} is being overwritten.", Logg.LoggingMode.InProgress); }
#endif
            
            _binders[type] = new FloatingTextEventBinder<TSource, TPayload>(
                subscribe, unsubscribe, onEvent);
        }

        public void Register(object source, FloatingTextEventType type)
        {
            if (_binders.TryGetValue(type, out var b)) b.Bind(source);
        }
        public void UnRegister(object source, FloatingTextEventType type)
        {
            if (_binders.TryGetValue(type, out var b)) b.Unbind(source);
        }

        private void ShowDamageText(Transform anchor, in HitResult hr)
        {
            Logg.Log($"[FTSpawner] print damage ({anchor.name}, {hr.Damage})", Logg.LoggingMode.InProgress);
            // 풀에서 꺼내 TMP/DOTween 연출…
        }

        private void ShowExpText(Transform anchor, float value)
        {
            
        }

        private sealed class FloatingTextEventBinder<TSource, TPayload> : IFloatingTextEventBinder where TSource : class
        {
            private readonly Func<object, TSource> _tryCast;
            private readonly Action<TSource, Action<TPayload>> _subscribe;
            private readonly Action<TSource, Action<TPayload>> _unsubscribe;
            private readonly Action<TSource, TPayload> _onEvent;
            
            private readonly Dictionary<TSource, Action<TPayload>> _handlers = new();
            
            public FloatingTextEventBinder(
                Action<TSource, Action<TPayload>> subscribe,
                Action<TSource, Action<TPayload>> unsubscribe,
                Action<TSource, TPayload> onEvent)
            {
                _tryCast = o => o is TSource s ? s : default;
                _subscribe = subscribe;
                _unsubscribe = unsubscribe;
                _onEvent = onEvent;
            }
            
            public void Bind(object o)
            {
                var src = _tryCast(o);
                if (src == null || _handlers.ContainsKey(src)) return;

                Action<TPayload> h = payload => _onEvent(src, payload);
                _handlers[src] = h;
                _subscribe(src, h);
            }

            public void Unbind(object o)
            {
                var src = _tryCast(o);
                if (src == null) return;
                if (!_handlers.TryGetValue(src, out var h)) return;

                _unsubscribe(src, h);
                _handlers.Remove(src);
            }
        }
    }
    
    public interface IFloatingTextEventBinder
    {
        void Bind(object o);
        void Unbind(object o);
    }
}

