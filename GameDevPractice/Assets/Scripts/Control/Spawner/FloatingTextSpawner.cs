using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
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
    // 전투/성장 이벤트를 UI 플로팅 텍스트로 변환해 풀링 오브젝트로 출력하는 스포너 구현체
    // 소스별 이벤트 바인딩, 프레임 단위 머지 배치, 캔버스 좌표계 연계를 단일 진입점으로 통합
    // 구조체 payload 전달 비용 완화를 위해 커스텀 delegate + in 전달 패턴 사용
    public class FloatingTextSpawner : IFloatingTextSpawner
    {
        // 이벤트 타입별 바인더 테이블
        private readonly Dictionary<FloatingTextEventType, IFloatingTextEventBinder> _binders = new();
        // 앵커 + 이벤트 타입별 대기 배치 테이블
        private readonly Dictionary<BatchKey, PendingBatch> _pendingBatches = new();
        
        // 풀 매니저에서 꺼낼 플로팅 텍스트 프리팹
        private GameObject textPrefab;
        // 이벤트 타입 -> 텍스트 설정 매핑 카탈로그
        private FloatingTextCatalogSO textCatalogSO;

        // 리소스 키 플로팅 텍스트 프리팹
        private const string textPrefabKey = "FloatingText";
        // 리소스 키 플로팅 텍스트 카탈로그 SO
        private const string textCatalogSOKey = "FloatingTextCatalogSO";
        // 배치당 최대 노출 텍스트 개수
        private const int MaxMergedTexts = 10;
        // 머지 배치 윈도우 지연 시간
        private static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(0.08f);
        
        // 프리로드 이후 프리팹/SO 조회용 로더
        private readonly IResourceLoader resourceLoader;
        // 실제 플로팅 텍스트 생성 부모 캔버스 RectTransform
        private RectTransform feedbackCanvasRect;
        // 비동기 배치 flush 취소 토큰 소스
        private readonly CancellationTokenSource flushCts = new CancellationTokenSource();
        
        // 생성자 바인더 초기화 + 프리로드 완료 콜백 등록
        public FloatingTextSpawner(IResourceLoader rLoader)
        {
            AddBinders();
            resourceLoader = rLoader;
            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, InitializeTextPools);
        }

        // 프리팹/카탈로그 로딩 초기화 루틴
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

        // 지원 이벤트 타입별 기본 바인더 등록 루틴
        private void AddBinders()
        {
            var damageType = FloatingTextEventType.Damage;
            AddBinder<IDamageable, HitResult, HitEvent>(
                damageType,
                (subject, h) => subject.OnDamaged += h,
                (subject, h) => subject.OnDamaged -= h,
                onEvent: HandleDamageEvent,
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

        // 피해 이벤트 payload를 단건/다건 조건에 따라 배치 출력 루틴으로 전달
        private void HandleDamageEvent(IDamageable subject, in HitResult data)
        {
            var anchor = AnchorOf(subject);
            if (anchor == null)
            {
                Logg.LogWarning("[FTSpawner] skipped damage floating text because anchor is null");
                return;
            }

            if (data.HasBatchDamages || data.AttackInstanceId > 0)
            {
                if (textPrefab == null || textCatalogSO == null)
                {
                    Logg.LogWarning("[FTSpawner] skipped damage floating text because prefab or catalog is not ready");
                    return;
                }

                if (!textCatalogSO.TryGetValue(FloatingTextEventType.Damage, out var setting))
                {
                    Logg.LogWarning("[FTSpawner] missing setting for floating text type Damage");
                    return;
                }

                if (!EnsureFeedbackCanvasReady())
                    return;

                if (data.HasBatchDamages)
                {
                    for (int i = 0; i < data.HitDamages.Count; i++)
                    {
                        EnqueueFloatingText(
                            FloatingTextEventType.Damage,
                            anchor,
                            setting,
                            data.HitDamages[i].ToString(CultureInfo.InvariantCulture),
                            data.AttackInstanceId);
                    }
                }
                else
                {
                    EnqueueFloatingText(
                        FloatingTextEventType.Damage,
                        anchor,
                        setting,
                        data.Damage.ToString(CultureInfo.InvariantCulture),
                        data.AttackInstanceId);
                }
                return;
            }

            ShowFloatingText(FloatingTextEventType.Damage, anchor, in data.Damage);
        }

        // 이벤트 소스 객체에서 월드 앵커 Transform 추출 헬퍼
        private static Transform AnchorOf(object s)
        {
            if (!s.IsNotNull()) return null;
            return (s as Component)?.transform;
        }

        // 제네릭 이벤트 바인더 생성 후 타입 테이블 등록
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

        // 소스 객체 + 이벤트 타입 바인딩 등록
        public void Register(object source, FloatingTextEventType type)
        {
            if (_binders.TryGetValue(type, out var b)) b.Bind(source);
        }
        // 소스 객체 + 이벤트 타입 바인딩 해제
        public void UnRegister(object source, FloatingTextEventType type)
        {
            if (_binders.TryGetValue(type, out var b)) b.Unbind(source);
        }
        
        // 특정 소스에 연결된 모든 이벤트 바인딩 해제
        public void UnregisterAll(object source)
        {
            foreach (var b in _binders.Values) b.Unbind(source);
        }

        // float 목록 배치를 문자열 목록으로 변환 후 공통 출력 경로 호출
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

        // 문자열 목록 배치 즉시 출력 경로
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

        // float payload 단건 출력 오버로드
        private void ShowFloatingText(FloatingTextEventType type, Transform anchor, in float value)
        {
            ShowFloatingText(type, anchor, value.ToString(CultureInfo.InvariantCulture));
        }

        // int payload 단건 출력 오버로드
        private void ShowFloatingText(FloatingTextEventType type, Transform anchor, in int value)
        {
            ShowFloatingText(type, anchor, value.ToString(CultureInfo.InvariantCulture));
        }

        // 문자열 payload 단건 출력 공통 루틴
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

        // 피드백 오버레이 캔버스 참조 확보 루틴
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

        // 앵커/타입 키 기준 대기 배치 큐 적재 + flush 예약
        private void EnqueueFloatingText(
            FloatingTextEventType type,
            Transform anchor,
            FloatingTextSO setting,
            string str,
            int attackInstanceId = 0)
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
            {
                batch.Texts.Add(str);
                batch.AttackInstanceIds.Add(attackInstanceId);
            }
            else
            {
                batch.OverflowCount += 1;
            }

            if (batch.FlushScheduled)
                return;

            batch.FlushScheduled = true;
            FlushBatchDelayedAsync(key).Forget();
        }

        // 머지 윈도우 이후 배치 flush 트리거 비동기 루틴
        private async UniTaskVoid FlushBatchDelayedAsync(BatchKey key)
        {
            try
            {
                var canceled = await UniTask
                    .Delay(MergeWindow, DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, flushCts.Token)
                    .SuppressCancellationThrow();

                if (canceled)
                    return;

                FlushBatch(key);
            }
            catch (Exception e)
            {
                Logg.LogWarning($"[FTSpawner] failed to flush floating text batch: {e.Message}");
            }
        }

        // 대기 배치 확정 출력 + 풀 오브젝트 세팅 루틴
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
            s.SetBatchTexts(batch.Texts, batch.AttackInstanceIds);
        }

        // 대기 배치 딕셔너리 키 구조체 앵커 인스턴스 ID + 이벤트 타입 조합
        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            // 앵커 transform instance id
            public readonly int AnchorId;
            // 이벤트 타입 키 값
            public readonly FloatingTextEventType Type;

            // 키 생성자
            public BatchKey(int anchorId, FloatingTextEventType type)
            {
                AnchorId = anchorId;
                Type = type;
            }

            // 값 동일성 비교 구현
            public bool Equals(BatchKey other)
            {
                return AnchorId == other.AnchorId
                       && Type == other.Type;
            }

            // object 기반 동일성 비교 오버라이드
            public override bool Equals(object obj)
            {
                return obj is BatchKey other && Equals(other);
            }

            // 딕셔너리 해시코드 오버라이드
            public override int GetHashCode()
            {
                return HashCode.Combine(AnchorId, (int)Type);
            }
        }

        // 머지 윈도우 동안 누적되는 배치 임시 버퍼
        private sealed class PendingBatch
        {
            // 최신 앵커 참조
            public Transform Anchor;
            // 출력 설정 데이터
            public FloatingTextSO Setting;
            // 누적 문자열 텍스트 목록
            public readonly List<string> Texts;
            // 텍스트 항목별 공격 인스턴스 ID 목록
            public readonly List<int> AttackInstanceIds;
            // 앵커 소실 대비 마지막 월드 좌표
            public Vector3 LastWorldPosition;
            // 노출 한도 초과 숨김 개수
            public int OverflowCount;
            // flush 예약 상태 플래그
            public bool FlushScheduled;

            // 배치 버퍼 생성자
            public PendingBatch(Transform anchor, FloatingTextSO setting, Vector3 lastWorldPosition)
            {
                Anchor = anchor;
                Setting = setting;
                LastWorldPosition = lastWorldPosition;
                OverflowCount = 0;
                FlushScheduled = false;
                Texts = new List<string>(MaxMergedTexts);
                AttackInstanceIds = new List<int>(MaxMergedTexts);
            }
        }

        // 타입 안전 이벤트 바인딩/해제 처리를 담당하는 제네릭 바인더 구현
        private sealed class FloatingTextEventBinder<TSource, TPayload, TEvent> : IFloatingTextEventBinder 
            where TSource : class
            where TEvent : Delegate
        {
            // object -> TSource 캐스팅 함수
            private readonly Func<object, TSource> _tryCast;
            // 외부 이벤트 구독 함수
            private readonly Action<TSource, TEvent> _subscribe;
            // 외부 이벤트 해제 함수
            private readonly Action<TSource, TEvent> _unsubscribe;
            // payload 수신 시 실행할 처리 함수
            private readonly OnEventHandler<TSource, TPayload> _onEvent;
            // payload 핸들러를 이벤트 delegate로 변환하는 어댑터
            private readonly Func<PayloadHandler<TPayload>, TEvent> _adapter;
            
            // 소스별 생성된 delegate 핸들 저장소
            private readonly Dictionary<TSource, TEvent> _handlers = new();
            
            // 바인더 생성자 인자 검증 + 핸들러 저장
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
            
            // 소스 객체 이벤트 구독 등록 중복 등록 방지 포함
            public void Bind(object o)
            {
                var src = _tryCast(o);
                if (src == null || _handlers.ContainsKey(src)) return;
                
                PayloadHandler<TPayload> ph = (in TPayload payload) => _onEvent(src, in payload);
                TEvent ev = _adapter(ph);

                _handlers[src] = ev;
                _subscribe(src, ev);
            }

            // 소스 객체 이벤트 구독 해제 + 핸들 테이블 정리
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
    
    // in 전달 payload 수신 delegate
    public delegate void PayloadHandler<TPayload>(in TPayload payload);
    // 소스 + payload 이벤트 처리 delegate
    public delegate void OnEventHandler<in TSource, TPayload>(TSource source, in TPayload payload);

    // 공통 바인더 동작 인터페이스
    public interface IFloatingTextEventBinder
    {
        // 이벤트 바인딩 등록
        void Bind(object o);
        // 이벤트 바인딩 해제
        void Unbind(object o);
    }
}
