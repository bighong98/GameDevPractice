using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Attribute;
using TH.Control.Movement;
using TH.Core.Pool;
using TH.SaveLoad;
using TH.Utils;
using Unity.Serialization.Json;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using TH.Resource;


#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SavableEntity))]
public class EnemySpawner : Spawner<CharacterTypeHolder>, ISavable
{
    [Serializable]
    private sealed class EnemySpawnerSaveData
    {
        public int nextSpawnSequence;
        public List<EnemySnapshot> snapshots = new();
    }

    [Serializable]
    private sealed class EnemySnapshot
    {
        public string spawnKey;
        public List<EnemyPayloadEntry> payload = new();
    }

    [Serializable]
    private sealed class EnemyPayloadEntry
    {
        public string typeName;
        public string jsonPayload;
    }

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

    private readonly Dictionary<IPoolObject, string> enemySpawnKeys = new();
    private readonly Dictionary<string, IPoolObject> enemyBySpawnKey = new();
    private readonly List<IPoolObject> snapshotBuffer = new();
    private readonly Collider[] overlapBuffer = new Collider[32];
    private static readonly SaveTypeResolver SaveTypeResolver = new();

    private bool isSpawnStartPending;
    private EnemySpawnerSaveData pendingRestoreData;
    private CancellationTokenSource spawnLoopCts;
    private int nextSpawnSequence;
    private bool hasStarted;
    private SavableEntity spawnerSavableEntity;
    private string spawnerKeyPrefix;

    private TimeSpan cachedSpawnInterval;
    private float cachedSpawnIntervalSeconds = -1f;

    private const int DefaultMaxCount = 100;
    private const char SpawnKeyDelimiter = ':';
    private int MaxActiveCount => max == 0 ? DefaultMaxCount : max;

    private void Awake()
    {
        TryInitializeSpawnerKeyPrefix();
    }

    protected override async void Start()
    {
        TryInitializeSpawnerKeyPrefix();

        try
        {
            await EnsurePoolInitializedAsync(destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Logg.LogError($"[{nameof(EnemySpawner)}.{nameof(Start)}] failed to initialize pool. {e.Message}");
        }

        UpdateSpawnIntervalCache(force: true);
        hasStarted = true;

        TryApplyPendingRestoreState();

        if (spawnOnStart)
            StartSpawning();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || !hasStarted)
            return;

        if (spawnOnStart)
            StartSpawning();
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    public void StartSpawning()
    {
        if (!Application.isPlaying)
            return;

        StartSpawningAsync(destroyCancellationToken).Forget();
    }

    private async UniTaskVoid StartSpawningAsync(CancellationToken token)
    {
        if (spawnLoopCts != null || isSpawnStartPending)
            return;

        isSpawnStartPending = true;

        try
        {
            if (!await EnsurePoolInitializedAsync(token))
                return;

            TryApplyPendingRestoreState();

            if (!HasPool || spawnLoopCts != null)
                return;

            UpdateSpawnIntervalCache();

            spawnLoopCts = new CancellationTokenSource();
            SpawnLoopAsync(spawnLoopCts.Token).Forget();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Logg.LogError($"[{nameof(EnemySpawner)}.{nameof(StartSpawningAsync)}] failed to start spawning. {e.Message}");
        }
        finally
        {
            isSpawnStartPending = false;
        }
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

    private async UniTask<bool> EnsurePoolInitializedAsync(CancellationToken token = default)
    {
        if (HasPool)
            return true;

        if (!await EnsurePrefabResolvedAsync(token))
        {
            if (prefab == null)
                Logg.LogError($"[{nameof(EnemySpawner)}.{nameof(EnsurePoolInitializedAsync)}] prefab is null");

            return false;
        }

        InitializePool();
        return HasPool;
    }


    public GameObject SpawnEnemyNow()
    {
        if (!Application.isPlaying)
            return null;

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
        if (HasPool)
            return;

        if (prefab == null)
        {
            Logg.LogError($"[{nameof(EnemySpawner)}.{nameof(InitializePool)}] prefab is null");
            return;
        }

        SetPool(prefab, null, OnEnemyGet, OnEnemyRelease, capacity, max);
        if (!HasPool)
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
        if (!HasPool)
            return false;

        return ActiveObjectCount < MaxActiveCount;
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

        AssignSpawnKey(enemy, GetNextSpawnKey());
    }

    private void OnEnemyRelease(IPoolObject enemy)
    {
        if (enemy == null)
            return;

        if (enemySpawnKeys.TryGetValue(enemy, out var spawnKey))
        {
            enemySpawnKeys.Remove(enemy);
            if (!string.IsNullOrEmpty(spawnKey))
                enemyBySpawnKey.Remove(spawnKey);
        }

        if (enemy is Component enemyComponent
            && enemyComponent.TryGetComponent(out SavableEntity savableEntity))
        {
            savableEntity.SetAutoRegisterToRegistry(false);
        }
    }

    object ISavable.CaptureState()
    {
        return CaptureState();
    }

    public object CaptureState()
    {
        var data = new EnemySpawnerSaveData
        {
            nextSpawnSequence = nextSpawnSequence,
            snapshots = new List<EnemySnapshot>()
        };

        CopyActiveObjectsTo(snapshotBuffer);
        foreach (var enemy in snapshotBuffer)
        {
            if (TryCreateSnapshot(enemy, out var snapshot))
            {
                data.snapshots.Add(snapshot);
            }
        }
        snapshotBuffer.Clear();

        return data;
    }

    public bool RestoreState(object state)
    {
        if (state is not EnemySpawnerSaveData data)
            return false;

        if (!HasPool)
        {
            pendingRestoreData = data;

            if (Application.isPlaying)
                TryApplyPendingRestoreStateAsync().Forget();

            return true;
        }

        return ApplyRestoreState(data);
    }

    private bool ApplyRestoreState(EnemySpawnerSaveData data)
    {
        var wasSpawning = spawnLoopCts != null;

        StopSpawning();
        ReleaseAllActiveEnemies();

        if (!HasPool)
            InitializePool();

        if (!HasPool)
            return false;

        var maxSequence = -1;
        if (data.snapshots != null)
        {
            foreach (var snapshot in data.snapshots)
            {
                if (!TrySpawnEnemyFromSnapshot(snapshot))
                    continue;

                if (TryExtractSequence(snapshot.spawnKey, out var sequence) && sequence > maxSequence)
                    maxSequence = sequence;
            }
        }

        nextSpawnSequence = Mathf.Max(data.nextSpawnSequence, maxSequence + 1);

        if (wasSpawning && enabled)
            StartSpawning();

        return true;
    }

    private void TryApplyPendingRestoreState()
    {
        if (pendingRestoreData == null || !HasPool)
            return;

        var restoreData = pendingRestoreData;
        pendingRestoreData = null;

        if (!ApplyRestoreState(restoreData))
            pendingRestoreData = restoreData;
    }

    private async UniTaskVoid TryApplyPendingRestoreStateAsync()
    {
        if (pendingRestoreData == null)
            return;

        if (!await EnsurePoolInitializedAsync(destroyCancellationToken))
            return;

        TryApplyPendingRestoreState();
    }


    public void ResetToDefaultState()
    {
        var wasSpawning = spawnLoopCts != null;

        StopSpawning();
        ReleaseAllActiveEnemies();
        pendingRestoreData = null;
        nextSpawnSequence = 0;

        if (wasSpawning && enabled && spawnOnStart)
            StartSpawning();
    }

    private bool TryCreateSnapshot(IPoolObject enemy, out EnemySnapshot snapshot)
    {
        snapshot = null;
        if (enemy == null || enemy is not Component enemyComponent)
            return false;

        if (!enemySpawnKeys.TryGetValue(enemy, out var spawnKey) || string.IsNullOrEmpty(spawnKey))
            return false;

        if (enemyComponent.TryGetComponent(out TH.Attribute.Health health) && health.IsDead)
            return false;

        if (!enemyComponent.TryGetComponent(out SavableEntity savableEntity))
            return false;

        if (savableEntity.CaptureState() is not Dictionary<string, object> payloadDict)
            return false;

        snapshot = new EnemySnapshot
        {
            spawnKey = spawnKey,
            payload = new List<EnemyPayloadEntry>()
        };

        foreach (var (typeName, payload) in payloadDict)
        {
            if (string.IsNullOrEmpty(typeName) || payload == null)
                continue;

            if (SaveTypeResolver.GetTypeByName(typeName) is not { } payloadType)
                continue;

            try
            {
                var json = JsonSerialization.ToJson(payload, new JsonSerializationParameters
                {
                    SerializedType = payloadType
                });

                snapshot.payload.Add(new EnemyPayloadEntry
                {
                    typeName = typeName,
                    jsonPayload = json
                });
            }
            catch (Exception e)
            {
                this.LogWarning($"Serialize enemy snapshot payload failed ({typeName}) - {e.Message}");
            }
        }

        return true;
    }

    private bool TrySpawnEnemyFromSnapshot(EnemySnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrEmpty(snapshot.spawnKey))
            return false;

        if (enemyBySpawnKey.ContainsKey(snapshot.spawnKey))
            return true;

        var spawnedEnemy = Spawn();
        if (spawnedEnemy == null)
            return false;

        AssignSpawnKey(spawnedEnemy, snapshot.spawnKey);

        if (spawnedEnemy is not Component enemyComponent)
            return true;

        if (!enemyComponent.TryGetComponent(out SavableEntity savableEntity))
            return true;

        var restoredPayload = DeserializeSnapshotPayload(snapshot.payload);
        if (restoredPayload.Count == 0)
            return true;

        return savableEntity.RestoreState((object)restoredPayload);
    }

    private Dictionary<string, object> DeserializeSnapshotPayload(List<EnemyPayloadEntry> payloadEntries)
    {
        var payload = new Dictionary<string, object>();
        if (payloadEntries == null)
            return payload;

        foreach (var entry in payloadEntries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.typeName) || string.IsNullOrEmpty(entry.jsonPayload))
                continue;

            if (SaveTypeResolver.GetTypeByName(entry.typeName) is not { } stateType)
                continue;

            var fromJsonMethod = SaveTypeResolver.GetFromJsonMethod(stateType);
            if (fromJsonMethod == null)
                continue;

            try
            {
                var restored = fromJsonMethod.Invoke(null, new object[]
                {
                    entry.jsonPayload,
                    new JsonSerializationParameters { SerializedType = stateType }
                });

                if (restored != null)
                    payload[entry.typeName] = restored;
            }
            catch (Exception e)
            {
                this.LogWarning($"Deserialize enemy snapshot payload failed ({entry.typeName}) - {e.Message}");
            }
        }

        return payload;
    }

    private void ReleaseAllActiveEnemies()
    {
        ReleaseTrackedActiveObjects();
        ClearActiveObjectTracking();
        snapshotBuffer.Clear();
        enemySpawnKeys.Clear();
        enemyBySpawnKey.Clear();
    }

    private void AssignSpawnKey(IPoolObject enemy, string spawnKey)
    {
        if (enemy == null || string.IsNullOrEmpty(spawnKey))
            return;

        if (enemyBySpawnKey.TryGetValue(spawnKey, out var existingEnemy)
            && existingEnemy != null
            && existingEnemy != enemy)
        {
            enemySpawnKeys.Remove(existingEnemy);
        }

        if (enemySpawnKeys.TryGetValue(enemy, out var previousKey) && !string.IsNullOrEmpty(previousKey))
        {
            enemyBySpawnKey.Remove(previousKey);
        }

        enemySpawnKeys[enemy] = spawnKey;
        enemyBySpawnKey[spawnKey] = enemy;

        if (enemy is not Component enemyComponent)
            return;

        if (!enemyComponent.TryGetComponent(out SavableEntity savableEntity))
            return;

        savableEntity.SetAutoRegisterToRegistry(false);
        savableEntity.SetRuntimeUniqueId(spawnKey);
    }

    private string GetNextSpawnKey()
    {
        TryInitializeSpawnerKeyPrefix();

        if (string.IsNullOrEmpty(spawnerKeyPrefix))
        {
            spawnerKeyPrefix = Guid.NewGuid().ToString();
            if (spawnerSavableEntity != null)
                spawnerSavableEntity.SetRuntimeUniqueId(spawnerKeyPrefix);
        }

        return $"{spawnerKeyPrefix}{SpawnKeyDelimiter}{nextSpawnSequence++}";
    }

    private void TryInitializeSpawnerKeyPrefix()
    {
        if (!string.IsNullOrEmpty(spawnerKeyPrefix))
            return;

        if (spawnerSavableEntity == null && !TryGetComponent(out spawnerSavableEntity))
            return;

        var candidate = spawnerSavableEntity.UniqueIdentifier;
        if (string.IsNullOrEmpty(candidate))
        {
            candidate = Guid.NewGuid().ToString();
            spawnerSavableEntity.SetRuntimeUniqueId(candidate);
        }

        spawnerKeyPrefix = candidate;
    }

    private static bool TryExtractSequence(string spawnKey, out int sequence)
    {
        sequence = -1;
        if (string.IsNullOrEmpty(spawnKey))
            return false;

        var delimiterIndex = spawnKey.LastIndexOf(SpawnKeyDelimiter);
        if (delimiterIndex < 0 || delimiterIndex >= spawnKey.Length - 1)
            return false;

        return int.TryParse(spawnKey[(delimiterIndex + 1)..], out sequence);
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

