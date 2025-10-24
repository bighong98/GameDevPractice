using System;
using System.Collections.Generic;
using TH.Combat;
using UnityEngine;

namespace TH.Utils
{
    public class FloatingTextSpawner : IFloatingTextSpawner
    {
        private readonly Dictionary<FloatingTextEventType, IEventBinder> _binders;

        public FloatingTextSpawner(/*, 필요시 풀 등 주입 */)
        {
            _binders = new Dictionary<FloatingTextEventType, IEventBinder>
            {
                { FloatingTextEventType.Damage, new DamageableBinder(this /*, 풀/프리팹 등 */) },
            };
        }

        public void Register(object source, FloatingTextEventType eventType)
        {
            if (!_binders.TryGetValue(eventType, out var binder))
                return;

            binder.Bind(source);
        }

        public void UnRegister(object source, FloatingTextEventType eventType)
        {
            if (!_binders.TryGetValue(eventType, out var binder))
                return;

            binder.Unbind(source);
        }

        // 공용 연출 API (바인더들이 호출)
        private void ShowDamageText(Transform anchor, in HitResult hr)
        {
            // 여기에 앞서 만든 DOTween/TMP 연출 호출
            // (풀에서 꺼내기 → 텍스트/색/크기 → 위치 세팅 → 재생)
            Logg.Log($"[{nameof(FloatingTextSpawner)}] Trying to print damage text ({anchor.name}, {hr.Damage})", Logg.LoggingMode.InProgress);
        }
        
        class DamageableBinder : IEventBinder
        {
            private readonly Dictionary<IDamageable, Action<HitResult>> handlers = new();
            private readonly FloatingTextSpawner spawner;

            public DamageableBinder(FloatingTextSpawner spawner) => this.spawner = spawner;

            public void Bind(object source)
            {
                if (source is not IDamageable dmg || handlers.ContainsKey(dmg))
                    return;

                Transform anchor = (dmg as Component)?.transform;
                Action<HitResult> handler = (hr) => spawner.ShowDamageText(anchor, hr);
                handlers[dmg] = handler;
                dmg.OnDamaged += handler;
            }

            public void Unbind(object source)
            {
                if (source is not IDamageable dmg) return;
                if (!handlers.TryGetValue(dmg, out var handler)) return;

                dmg.OnDamaged -= handler;
                handlers.Remove(dmg);
            }
        }
    }
    
    public interface IEventBinder
    {
        void Bind(object o);
        void Unbind(object o);
    }

    
    
    // public class FloatingTextSpawner : IFloatingTextSpawner
    // {
    //     private readonly Dictionary<IDamageable, Action<HitResult>> damageHandlers = new();
    //     public void Register(object sender, FloatingTextEventType eventType)
    //     {
    //         switch (eventType)
    //         {
    //             case FloatingTextEventType.Damage when sender is IDamageable damageable:
    //                 damageHandlers.TryAdd(damageable, DamageHandler);
    //                 damageable.OnDamaged += DamageHandler;
    //                 break;
    //                 void DamageHandler(HitResult hr) => ShowDamageText(damageable, hr);
    //             default:
    //                 break;
    //         }
    //         
    //     }
    //
    //     public void UnRegister(object sender, FloatingTextEventType eventType)
    //     {
    //         switch (eventType)
    //         {
    //             case FloatingTextEventType.Damage when sender is IDamageable damageable:
    //                 if (damageHandlers.TryGetValue(damageable, out var handler))
    //                     damageable.OnDamaged -= handler;
    //                 break;
    //             default:
    //                 break;
    //         }
    //     }
    //
    //     private void ShowDamageText(IDamageable sender, HitResult hitResult)
    //     {
    //         
    //     }
    // }
}

