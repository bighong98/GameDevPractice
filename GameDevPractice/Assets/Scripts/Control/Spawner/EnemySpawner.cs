using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Attribute;
using TH.Control.Movement;
using TH.Core.Pool;
using TH.Resource;
using TH.Utils;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class EnemySpawner : Spawner<CharacterTypeHolder>
{
    [Header("Enemy")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Pool")]
    [SerializeField, Min(0)] private int capacity = 10;
    [SerializeField, Min(0)] private int max = 30;

    [Header("Spawn")]
    [SerializeField, Min(0.05f)] private float spawnInterval = 1.5f;
    [SerializeField, Min(0f)] private float spawnRadius = 3f;
    [SerializeField, Min(0.05f)] private float overlapCheckRadius = 0.6f;
    [SerializeField] private LayerMask overlapMask = ~0;
    [SerializeField, Min(0.05f)] private float navMeshSampleDistance = 1.5f;
    [SerializeField, Min(1)] private int maxSpawnAttempts = 8;
    [SerializeField] private bool spawnOnStart = true;

#if UNITY_EDITOR
    [Header("Gizmo (Editor Only)")]
    [SerializeField] private bool drawGizmoAlways;
#endif

    private readonly HashSet<IPoolObject> activeEnemies = new();
    private readonly Collider[] overlapBuffer = new Collider[32];

    private CancellationTokenSource spawnLoopCts;
    private bool isInitialized;

    private TimeSpan cachedSpawnInterval;
    private float cachedSpawnIntervalSeconds = -1f;

    private const int DefaultMaxCount = 100;
    private int MaxActiveCount => max == 0 ? DefaultMaxCount : max;

    protected override void Start()
    {
        InitializePool();
        UpdateSpawnIntervalCache(force: true);

        if (spawnOnStart)
            StartSpawning();
    }

    private void OnEnable()
    {
        if (spawnOnStart && isInitialized)
            StartSpawning();
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    private void OnValidate()
    {
        UpdateSpawnIntervalCache(force: true);
    }

    public void StartSpawning()
    {
        if (!isInitialized)
            InitializePool();

        if (!isInitialized || spawnLoopCts != null)
            return;

        UpdateSpawnIntervalCache();

        spawnLoopCts = new CancellationTokenSource();
        SpawnLoopAsync(spawnLoopCts.Token).Forget();
    }

    public void StopSpawning()
    {
        if (spawnLoopCts == null)
            return;

        if (!spawnLoopCts.IsCancellationRequested)
            spawnLoopCts.Cancel();

        spawnLoopCts.Dispose();
        spawnLoopCts = null;
    }

    public GameObject SpawnEnemyNow()
    {
        if (!CanSpawn())
            return null;

        if (!TryGetSpawnPosition(out var spawnPosition))
            return null;

        var spawnedEnemy = Spawn();
        if (spawnedEnemy == null)
            return null;

        PlaceEnemyAtSpawnPoint(spawnedEnemy, spawnPosition);
        return spawnedEnemy.gameObject;
    }

    private void PlaceEnemyAtSpawnPoint(Component enemyComponent, Vector3 spawnPosition)
    {
        if (enemyComponent.TryGetComponent(out NavMeshAgent navMeshAgent))
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;

            if (!navMeshAgent.Warp(spawnPosition))
                enemyComponent.transform.position = spawnPosition;
        }
        else
        {
            enemyComponent.transform.position = spawnPosition;
        }

        if (enemyComponent.TryGetComponent(out Mover mover))
            mover.ResetMovementState();
    }

    private async UniTaskVoid SpawnLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && enabled)
        {
            SpawnEnemyNow();
            UpdateSpawnIntervalCache();

            await UniTask.Delay(cachedSpawnInterval, DelayType.DeltaTime,
                PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        }
    }

    private void InitializePool()
    {
        if (isInitialized)
            return;

        if (enemyPrefab == null)
        {
            Logg.LogError($"[{nameof(EnemySpawner)}.{nameof(InitializePool)}] enemyPrefab is null");
            return;
        }

        SetPool(enemyPrefab, null, OnEnemyGet, OnEnemyRelease, capacity, max);
        isInitialized = pool != null;

        if (!isInitialized)
            Logg.LogError($"[{nameof(EnemySpawner)}.{nameof(InitializePool)}] failed to create pool. Ensure prefab has an IPoolObject component.");
    }

    private void UpdateSpawnIntervalCache(bool force = false)
    {
        var interval = Mathf.Max(0.05f, spawnInterval);

        if (!force && Mathf.Approximately(interval, cachedSpawnIntervalSeconds))
            return;

        cachedSpawnIntervalSeconds = interval;
        cachedSpawnInterval = TimeSpan.FromSeconds(interval);
    }

    private bool CanSpawn()
    {
        if (!isInitialized)
            return false;

        return activeEnemies.Count < MaxActiveCount;
    }

    private bool TryGetSpawnPosition(out Vector3 spawnPosition)
    {
        var center = transform.position;

        for (var i = 0; i < maxSpawnAttempts; i++)
        {
            var offset = Random.insideUnitCircle * spawnRadius;
            var candidate = center + new Vector3(offset.x, 0f, offset.y);

            if (!TryGetWalkablePosition(candidate, out var walkablePosition))
                continue;

            if (!HasCharacterOverlap(walkablePosition))
            {
                spawnPosition = walkablePosition;
                return true;
            }
        }

        spawnPosition = default;
        return false;
    }

    private bool TryGetWalkablePosition(Vector3 candidate, out Vector3 walkablePosition)
    {
        if (NavMesh.SamplePosition(candidate, out var navMeshHit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            walkablePosition = navMeshHit.position;
            return true;
        }

        walkablePosition = default;
        return false;
    }

    private bool HasCharacterOverlap(Vector3 position)
    {
        var hitCount = Physics.OverlapSphereNonAlloc(position, overlapCheckRadius, overlapBuffer, overlapMask,
            QueryTriggerInteraction.Ignore);

        for (var i = 0; i < hitCount; i++)
        {
            var collider = overlapBuffer[i];
            if (collider == null)
                continue;

            if (collider.transform.IsChildOf(transform))
                continue;

            if (collider.GetComponentInParent<Health>() != null)
                return true;
        }

        return false;
    }

    private void OnEnemyGet(IPoolObject enemy)
    {
        if (enemy == null)
            return;

        activeEnemies.Add(enemy);
    }

    private void OnEnemyRelease(IPoolObject enemy)
    {
        if (enemy == null)
            return;

        activeEnemies.Remove(enemy);
    }

#if UNITY_EDITOR
    private static readonly Color SpawnAreaGizmoColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    private static readonly Color OverlapCheckGizmoColor = new Color(0.2f, 0.8f, 1f, 0.95f);

    private void OnDrawGizmos()
    {
        if (!drawGizmoAlways)
            return;

        DrawSpawnGizmos(0.45f);
    }

    private void OnDrawGizmosSelected()
    {
        DrawSpawnGizmos(1f);
    }

    private void DrawSpawnGizmos(float alpha)
    {
        var center = transform.position;

        var spawnColor = WithAlpha(SpawnAreaGizmoColor, alpha);
        Handles.color = WithAlpha(spawnColor, 0.25f);
        Handles.DrawSolidDisc(center, Vector3.up, spawnRadius);
        Handles.color = spawnColor;
        Handles.DrawWireDisc(center, Vector3.up, spawnRadius);

        Gizmos.color = WithAlpha(OverlapCheckGizmoColor, alpha);
        Gizmos.DrawWireSphere(center, overlapCheckRadius);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(color.a * alpha);
        return color;
    }
#endif
}

