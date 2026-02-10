using System;
using System.Collections;
using System.Collections.Generic;
using TH.Attribute;
using TH.Combat;
using TH.Control;
using TH.Core.Service;
using UnityEngine;

namespace TH.Utils
{
    // Play mode probe component for validating floating text output paths.
    public sealed class FloatingTextPlayModeProbe : MonoBehaviour
    {
#if UNITY_EDITOR
        private enum ProbeDispatchMode
        {
            DamageableBatchHitResult,
            DamageableRepeatedSingle,
            DirectSpawnerBatch,
        }

        [Header("Target")]
        [SerializeField] private Transform playerAnchor;
        [SerializeField] private Component damageableTarget;

        [Header("Dispatch")]
        [SerializeField] private ProbeDispatchMode dispatchMode = ProbeDispatchMode.DamageableBatchHitResult;
        [SerializeField] private bool logDispatchDetails;

        [Header("Direct Spawner Mode")]
        [SerializeField] private FloatingTextEventType eventType = FloatingTextEventType.Damage;
        [SerializeField] private FloatingTextBatchLayout batchLayout = FloatingTextBatchLayout.Line;

        [Header("Emit")]
        [SerializeField, Min(1)] private int emitsPerInterval = 1;
        [SerializeField] private bool spreadEmitsAcrossFrames;
        [SerializeField] private float emitInterval = 0.5f;
        [SerializeField] private Vector2 damageRange = new Vector2(5f, 20f);
        [SerializeField] private Vector2Int hitCountRange = new Vector2Int(2, 4);
        [SerializeField] private bool useRandomHitCount = true;
        [SerializeField, Min(1)] private int fixedHitCount = 3;
        [SerializeField] private bool roundDamageToInt = true;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Control")]
        [SerializeField] private bool autoStartOnEnable = true;
        [SerializeField] private int initialAttackInstanceId = 1;

        private IFloatingTextSpawner floatingTextSpawner;
        private IPlayerHolder playerHolder;
        private Coroutine emitRoutine;
        private int nextAttackInstanceId;

        private void Awake()
        {
            nextAttackInstanceId = Mathf.Max(1, initialAttackInstanceId);
        }

        private void OnEnable()
        {
            if (autoStartOnEnable)
                StartProbe();
        }

        private void OnDisable()
        {
            StopProbe();
        }

        [ContextMenu("Start FloatingText Probe")]
        public void StartProbe()
        {
            if (emitRoutine != null)
                return;

            emitRoutine = StartCoroutine(ProbeRoutine());
        }

        [ContextMenu("Stop FloatingText Probe")]
        public void StopProbe()
        {
            if (emitRoutine == null)
                return;

            StopCoroutine(emitRoutine);
            emitRoutine = null;
        }

        [ContextMenu("Apply High Load Preset")]
        private void ApplyHighLoadPreset()
        {
            dispatchMode = ProbeDispatchMode.DamageableBatchHitResult;
            useRandomHitCount = false;
            fixedHitCount = 8;
            emitInterval = 0.02f;
            emitsPerInterval = 4;
            spreadEmitsAcrossFrames = false;
            logDispatchDetails = false;
        }

        [ContextMenu("Apply Extreme Load Preset")]
        private void ApplyExtremeLoadPreset()
        {
            dispatchMode = ProbeDispatchMode.DirectSpawnerBatch;
            batchLayout = FloatingTextBatchLayout.Spread;
            eventType = FloatingTextEventType.Damage;
            useRandomHitCount = false;
            fixedHitCount = 12;
            emitInterval = 0f;
            emitsPerInterval = 8;
            spreadEmitsAcrossFrames = false;
            logDispatchDetails = false;
        }



        private IEnumerator ProbeRoutine()
        {
            while (enabled)
            {
                if (!TryResolveServices())
                {
                    yield return null;
                    continue;
                }

                var anchor = ResolvePlayerAnchor();
                if (anchor == null)
                {
                    yield return null;
                    continue;
                }

                int emitCount = Mathf.Max(1, emitsPerInterval);
                for (int i = 0; i < emitCount; i++)
                {
                    EmitOnce(anchor);

                    if (spreadEmitsAcrossFrames && i < emitCount - 1)
                        yield return null;
                }

                float wait = Mathf.Max(0f, emitInterval);
                if (wait <= 0f)
                {
                    yield return null;
                }
                else if (useUnscaledTime)
                {
                    yield return new WaitForSecondsRealtime(wait);
                }
                else
                {
                    yield return new WaitForSeconds(wait);
                }
            }
        }

        private bool TryResolveServices()
        {
            if (dispatchMode == ProbeDispatchMode.DirectSpawnerBatch && floatingTextSpawner == null)
            {
                floatingTextSpawner = TryGetService<IFloatingTextSpawner>();
                if (floatingTextSpawner == null)
                    return false;
            }

            if (playerHolder == null)
                playerHolder = TryGetService<IPlayerHolder>();

            return true;
        }

        private Transform ResolvePlayerAnchor()
        {
            if (playerAnchor != null)
                return playerAnchor;

            if (playerHolder != null)
            {
                if (playerHolder.GetPlayerInstance is Component c)
                    return c.transform;

                if (playerHolder.GetPlayerInstance is GameObject go)
                    return go.transform;
            }

            var playerByTag = GameObject.FindGameObjectWithTag("Player");
            if (playerByTag != null)
                return playerByTag.transform;

            var playerController = FindAnyObjectByType<PlayerController>();
            return playerController != null ? playerController.transform : null;
        }

        private IDamageable ResolveDamageable(Transform anchor)
        {
            if (damageableTarget is IDamageable assigned)
                return assigned;

            if (anchor != null)
            {
                if (anchor.TryGetComponent<IDamageable>(out var onAnchor))
                    return onAnchor;
                if (anchor.TryGetComponent<Health>(out var healthOnAnchor))
                    return healthOnAnchor;
            }

            if (playerHolder?.GetPlayerInstance is Component c)
            {
                if (c.TryGetComponent<IDamageable>(out var fromHolder))
                    return fromHolder;
            }

            return FindAnyObjectByType<Health>();
        }

        private void EmitOnce(Transform anchor)
        {
            int hitCount = ResolveHitCount();
            var damages = BuildDamageValues(hitCount);

            switch (dispatchMode)
            {
                case ProbeDispatchMode.DirectSpawnerBatch:
                    EmitByDirectSpawner(anchor, damages);
                    break;
                case ProbeDispatchMode.DamageableRepeatedSingle:
                    EmitByRepeatedSingles(anchor, damages);
                    break;
                default:
                    EmitByBatchHitResult(anchor, damages);
                    break;
            }
        }

        private int ResolveHitCount()
        {
            if (useRandomHitCount)
            {
                int minHit = Mathf.Max(1, Mathf.Min(hitCountRange.x, hitCountRange.y));
                int maxHit = Mathf.Max(minHit, Mathf.Max(hitCountRange.x, hitCountRange.y));
                return UnityEngine.Random.Range(minHit, maxHit + 1);
            }

            return Mathf.Max(1, fixedHitCount);
        }

        private List<float> BuildDamageValues(int hitCount)
        {
            float minDamage = Mathf.Min(damageRange.x, damageRange.y);
            float maxDamage = Mathf.Max(damageRange.x, damageRange.y);
            var damages = new List<float>(hitCount);

            for (int i = 0; i < hitCount; i++)
            {
                float damage = UnityEngine.Random.Range(minDamage, maxDamage);
                damages.Add(roundDamageToInt ? Mathf.Round(damage) : damage);
            }

            return damages;
        }

        private void EmitByDirectSpawner(Transform anchor, IReadOnlyList<float> damages)
        {
            if (floatingTextSpawner == null)
                return;

            floatingTextSpawner.SpawnBatch(eventType, anchor, damages, batchLayout);
            LogDispatch($"DirectSpawnerBatch count={damages.Count}, layout={batchLayout}, eventType={eventType}");
        }

        private void EmitByBatchHitResult(Transform anchor, IReadOnlyList<float> damages)
        {
            var damageable = ResolveDamageable(anchor);
            if (damageable == null)
                return;

            int attackInstanceId = TakeNextAttackInstanceId();
            float totalDamage = 0f;
            for (int i = 0; i < damages.Count; i++)
                totalDamage += damages[i];

            var hitResult = new HitResult(default, totalDamage, attackInstanceId, damages);
            damageable.TakeDamage(hitResult);
            LogDispatch($"DamageableBatchHitResult count={damages.Count}, attackId={attackInstanceId}, total={totalDamage:0.##}");
        }

        private void EmitByRepeatedSingles(Transform anchor, IReadOnlyList<float> damages)
        {
            var damageable = ResolveDamageable(anchor);
            if (damageable == null)
                return;

            int attackInstanceId = TakeNextAttackInstanceId();
            for (int i = 0; i < damages.Count; i++)
            {
                var hitResult = new HitResult(default, damages[i], attackInstanceId, null);
                damageable.TakeDamage(hitResult);
            }

            LogDispatch($"DamageableRepeatedSingle count={damages.Count}, attackId={attackInstanceId}");
        }

        private int TakeNextAttackInstanceId()
        {
            int current = nextAttackInstanceId;
            nextAttackInstanceId = current == int.MaxValue ? 1 : current + 1;
            return current;
        }

        private void LogDispatch(string message)
        {
            if (!logDispatchDetails)
                return;

            Logg.Log($"[FloatingTextProbe] {message}", Logg.LoggingMode.InProgress);
        }

        private static T TryGetService<T>() where T : class
        {
            try
            {
                return ServiceLocator.Get<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }
#endif
    }
}
