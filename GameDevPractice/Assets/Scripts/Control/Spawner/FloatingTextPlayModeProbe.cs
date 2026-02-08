using System;
using System.Collections;
using TH.Combat;
using TH.Control;
using TH.Core.Service;
using UnityEngine;

namespace TH.Utils
{
    // Play mode probe component for validating floating damage text output.
    public sealed class FloatingTextPlayModeProbe : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Target")]
        [SerializeField] private Transform playerAnchor;

        [Header("Emit")]
        [SerializeField] private FloatingTextEventType eventType = FloatingTextEventType.Damage;
        [SerializeField] private FloatingTextBatchLayout batchLayout = FloatingTextBatchLayout.Line;
        [SerializeField] private float emitInterval = 0.5f;
        [SerializeField] private Vector2 damageRange = new Vector2(5f, 20f);
        [SerializeField] private Vector2Int hitCountRange = new Vector2Int(2, 4);
        [SerializeField] private bool useRandomHitCount = true;
        [SerializeField, Min(1)] private int fixedHitCount = 3;
        [SerializeField] private bool roundDamageToInt = true;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Control")]
        [SerializeField] private bool autoStartOnEnable = true;

        private IFloatingTextSpawner floatingTextSpawner;
        private IPlayerHolder playerHolder;
        private Coroutine emitRoutine;

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

                EmitBatchOnce(anchor);

                float wait = Mathf.Max(0.01f, emitInterval);
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(wait);
                else
                    yield return new WaitForSeconds(wait);
            }
        }

        private bool TryResolveServices()
        {
            if (floatingTextSpawner == null)
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

        private void EmitBatchOnce(Transform anchor)
        {
            int hitCount;
            if (useRandomHitCount)
            {
                int minHit = Mathf.Max(1, Mathf.Min(hitCountRange.x, hitCountRange.y));
                int maxHit = Mathf.Max(minHit, Mathf.Max(hitCountRange.x, hitCountRange.y));
                hitCount = UnityEngine.Random.Range(minHit, maxHit + 1);
            }
            else
            {
                hitCount = Mathf.Max(1, fixedHitCount);
            }

            float minDamage = Mathf.Min(damageRange.x, damageRange.y);
            float maxDamage = Mathf.Max(damageRange.x, damageRange.y);

            var values = new System.Collections.Generic.List<string>(hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                float damage = UnityEngine.Random.Range(minDamage, maxDamage);
                if (roundDamageToInt)
                    values.Add(Mathf.RoundToInt(damage).ToString());
                else
                    values.Add(damage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            }

            floatingTextSpawner.SpawnBatch(eventType, anchor, values, batchLayout);
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
