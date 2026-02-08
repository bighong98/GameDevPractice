using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using TH.Attribute;
using TH.Combat;
using TH.Resource;
using TH.UI.Data;
using TH.UI;
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
        private readonly Dictionary<BatchKey, PendingBatch> _pendingBatches = new();
        
        private GameObject textPrefab;
        private FloatingTextCatalogSO textCatalogSO;

        private const string textPrefabKey = "FloatingText";
        private const string textCatalogSOKey = "FloatingTextCatalogSO";
        private const int MaxMergedTexts = 6;
        private static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(0.08f);
        
        private readonly IResourceLoader resourceLoader;
        private RectTransform feedbackCanvasRect;
        
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
            _binders[damageType] = new DamageFloatingTextBinder(
                onSingleDamage: (IDamageable subject, in HitResult data) =>
                    ShowFloatingText(damageType, AnchorOf(subject), in data.Damage),
                onBatchDamage: (IDamageable subject, IReadOnlyList<float> values) =>
                    SpawnBatch(damageType, AnchorOf(subject), values, FloatingTextBatchLayout.Line)
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

        public void SpawnBatch(FloatingTextEventType type, Transform anchor, IReadOnlyCollection<float> values, FloatingTextBatchLayout layout = FloatingTextBatchLayout.Line)
        {
            if (values == null || values.Count == 0)
            {
                Logg.LogWarning($"[FTSpawner] skipped {type} floating text batch because values are empty");
                return;
            }

            int visibleCount = Mathf.Min(values.Count, MaxMergedTexts);
            int overflowCount = values.Count - visibleCount;
            var groupedTexts = new List<string>(visibleCount);

            int index = 0;
            foreach (var value in values)
            {
                if (index >= visibleCount)
                    break;

                groupedTexts.Add(value.ToString(CultureInfo.InvariantCulture));
                index++;
            }

            if (overflowCount > 0)
            {
                string anchorName = anchor != null ? anchor.name : "NullAnchor";
                Logg.LogWarning($"[FTSpawner] merged text overflow for {type} at {anchorName}. hidden count: {overflowCount}");
            }

            SpawnBatch(type, anchor, groupedTexts, layout);
        }

        public void SpawnBatch(FloatingTextEventType type, Transform anchor, IReadOnlyList<string> values, FloatingTextBatchLayout layout = FloatingTextBatchLayout.Line)
        {
            if (anchor == null)
            {
                Logg.LogWarning($"[FTSpawner] skipped {type} floating text batch because anchor is null");
                return;
            }

            if (values == null || values.Count == 0)
            {
                Logg.LogWarning($"[FTSpawner] skipped {type} floating text batch because values are empty");
                return;
            }

            if (textPrefab == null || textCatalogSO == null)
            {
                Logg.LogWarning($"[FTSpawner] skipped {type} floating text batch because prefab or catalog is not ready");
                return;
            }

            if (!textCatalogSO.TryGetValue(type, out var setting))
            {
                Logg.LogWarning($"[FTSpawner] missing setting for floating text type {type}");
                return;
            }

            if (!EnsureFeedbackCanvasReady())
                return;

            int visibleCount = Mathf.Min(values.Count, MaxMergedTexts);
            int overflowCount = values.Count - visibleCount;
            var groupedTexts = new List<string>(visibleCount);
            for (int i = 0; i < visibleCount; i++)
            {
                groupedTexts.Add(values[i] ?? string.Empty);
            }

            if (overflowCount > 0)
            {
                Logg.LogWarning($"[FTSpawner] merged text overflow for {type} at {anchor.name}. hidden count: {overflowCount}");
            }

            Logg.Log($"[FTSpawner] print immediate merged {type} ({groupedTexts.Count})", Logg.LoggingMode.Completed);
            var s = PoolManager.Instance.GetFromPool<FloatingTextController>(textPrefab, feedbackCanvasRect, anchor.position);
            if (s == null)
            {
                Logg.LogWarning($"[FTSpawner] failed to get {nameof(FloatingTextController)} from pool");
                return;
            }

            s.SetWorldAnchor(anchor, anchor.position);
            s.SetSetting(setting);
            s.SetBatchTexts(groupedTexts, layout);
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
            if (anchor == null)
            {
                Logg.LogWarning($"[FTSpawner] skipped {type} floating text because anchor is null");
                return;
            }
            if (textPrefab == null || textCatalogSO == null)
            {
                Logg.LogWarning($"[FTSpawner] skipped {type} floating text because prefab or catalog is not ready");
                return;
            }
            if (!textCatalogSO.TryGetValue(type, out var setting))
            {
                Logg.LogWarning($"[FTSpawner] missing setting for floating text type {type}");
                return;
            }

            if (!EnsureFeedbackCanvasReady())
                return;

            EnqueueFloatingText(type, anchor, setting, str);
        }

        private bool EnsureFeedbackCanvasReady()
        {
            if (feedbackCanvasRect == null)
                feedbackCanvasRect = UIManager.Instance.GetCanvasRect(UICanvas.FeedbackOverlay);
            if (feedbackCanvasRect == null)
            {
                Logg.LogWarning("[FTSpawner] skipped floating text because FeedbackOverlay canvas is not ready");
                return false;
            }

            return true;
        }

        private void EnqueueFloatingText(FloatingTextEventType type, Transform anchor, FloatingTextSO setting, string str)
        {
            var key = new BatchKey(anchor.GetInstanceID(), type);
            if (!_pendingBatches.TryGetValue(key, out var batch))
            {
                batch = new PendingBatch(anchor, setting, anchor.position);
                _pendingBatches.Add(key, batch);
            }
            else
            {
                batch.Anchor = anchor;
                batch.Setting = setting;
                batch.LastWorldPosition = anchor.position;
            }

            if (batch.Texts.Count < MaxMergedTexts)
                batch.Texts.Add(str);
            else
                batch.OverflowCount += 1;

            if (batch.FlushScheduled)
                return;

            batch.FlushScheduled = true;
            FlushBatchDelayedAsync(key).Forget();
        }

        private async UniTaskVoid FlushBatchDelayedAsync(BatchKey key)
        {
            try
            {
                await UniTask.Delay(MergeWindow, DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update);
                FlushBatch(key);
            }
            catch (Exception e)
            {
                Logg.LogWarning($"[FTSpawner] failed to flush floating text batch: {e.Message}");
            }
        }

        private void FlushBatch(BatchKey key)
        {
            if (!_pendingBatches.TryGetValue(key, out var batch))
                return;

            _pendingBatches.Remove(key);
            batch.FlushScheduled = false;

            if (batch.Texts.Count == 0)
                return;

            if (batch.OverflowCount > 0)
            {
                var anchorName = batch.Anchor != null ? batch.Anchor.name : "DestroyedAnchor";
                Logg.LogWarning($"[FTSpawner] merged text overflow for {key.Type} at {anchorName}. hidden count: {batch.OverflowCount}");
            }

            if (textPrefab == null || textCatalogSO == null)
            {
                Logg.LogWarning($"[FTSpawner] skipped {key.Type} floating text because prefab or catalog is not ready");
                return;
            }

            if (!EnsureFeedbackCanvasReady())
                return;

            var anchor = batch.Anchor;
            var spawnPosition = anchor != null ? anchor.position : batch.LastWorldPosition;

            Logg.Log($"[FTSpawner] print merged {key.Type} ({batch.Texts.Count})", Logg.LoggingMode.Completed);
            var s = PoolManager.Instance.GetFromPool<FloatingTextController>(textPrefab, feedbackCanvasRect, spawnPosition);
            if (s == null)
            {
                Logg.LogWarning($"[FTSpawner] failed to get {nameof(FloatingTextController)} from pool");
                return;
            }

            s.SetWorldAnchor(anchor, spawnPosition);
            s.SetSetting(batch.Setting);
            s.SetBatchTexts(batch.Texts, FloatingTextBatchLayout.Spread);
        }

        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly int AnchorId;
            public readonly FloatingTextEventType Type;

            public BatchKey(int anchorId, FloatingTextEventType type)
            {
                AnchorId = anchorId;
                Type = type;
            }

            public bool Equals(BatchKey other)
            {
                return AnchorId == other.AnchorId && Type == other.Type;
            }

            public override bool Equals(object obj)
            {
                return obj is BatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(AnchorId, (int)Type);
            }
        }

        private sealed class PendingBatch
        {
            public Transform Anchor;
            public FloatingTextSO Setting;
            public readonly List<string> Texts;
            public Vector3 LastWorldPosition;
            public int OverflowCount;
            public bool FlushScheduled;

            public PendingBatch(Transform anchor, FloatingTextSO setting, Vector3 lastWorldPosition)
            {
                Anchor = anchor;
                Setting = setting;
                LastWorldPosition = lastWorldPosition;
                OverflowCount = 0;
                FlushScheduled = false;
                Texts = new List<string>(MaxMergedTexts);
            }
        }

        private delegate void DamageSingleEventHandler(IDamageable source, in HitResult payload);
        private delegate void DamageBatchEventHandler(IDamageable source, IReadOnlyList<float> payload);

        private sealed class DamageFloatingTextBinder : IFloatingTextEventBinder
        {
            private readonly DamageSingleEventHandler _onSingleDamage;
            private readonly DamageBatchEventHandler _onBatchDamage;
            private readonly Dictionary<IDamageable, DamageEventHandlers> _handlers = new();

            public DamageFloatingTextBinder(
                DamageSingleEventHandler onSingleDamage,
                DamageBatchEventHandler onBatchDamage)
            {
                _onSingleDamage = onSingleDamage ?? throw new ArgumentNullException(nameof(onSingleDamage));
                _onBatchDamage = onBatchDamage ?? throw new ArgumentNullException(nameof(onBatchDamage));
            }

            public void Bind(object o)
            {
                if (o is not IDamageable source || _handlers.ContainsKey(source))
                    return;

                HitEvent singleHandler = (in HitResult payload) => _onSingleDamage(source, in payload);
                Action<IReadOnlyList<float>> batchHandler = payload => _onBatchDamage(source, payload);

                source.OnDamaged += singleHandler;
                source.OnDamagedBatch += batchHandler;
                _handlers[source] = new DamageEventHandlers(singleHandler, batchHandler);
            }

            public void Unbind(object o)
            {
                if (o is not IDamageable source)
                    return;

                if (!_handlers.TryGetValue(source, out var handlers))
                    return;

                source.OnDamaged -= handlers.Single;
                source.OnDamagedBatch -= handlers.Batch;
                _handlers.Remove(source);
            }

            private readonly struct DamageEventHandlers
            {
                public readonly HitEvent Single;
                public readonly Action<IReadOnlyList<float>> Batch;

                public DamageEventHandlers(HitEvent single, Action<IReadOnlyList<float>> batch)
                {
                    Single = single;
                    Batch = batch;
                }
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
