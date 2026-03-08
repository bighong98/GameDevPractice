using TH.Control;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Combat.Drop
{
    [DisallowMultipleComponent]
    public sealed class EnemyDropOnKill : MonoBehaviour, ITypeDependent
    {
        [Header("Drop Table")]
        [SerializeField] private EnemyDropTableSO dropTable;
        private EnemyDropTableSO injectedDropTable;

        [Header("Spawn")]
        [SerializeField] private Vector3 spawnOffset = Vector3.up * 0.5f;
        [SerializeField, Min(0f)] private float scatterRadius = 0.75f;
        [SerializeField, Min(0f)] private float scatterHorizontalForce = 1.5f;
        [SerializeField, Min(0f)] private float scatterUpForce = 1.25f;
        [SerializeField] private bool applyScatterForce = true;

        private EnemyDropTableSO ActiveDropTable => injectedDropTable != null ? injectedDropTable : dropTable;

        public void ReceiveType(ScriptableObject typeInfo)
        {
            injectedDropTable = (typeInfo as IHasEnemyDropTable)?.EnemyDropTable;
        }

        public bool TryDrop()
        {
            var table = ActiveDropTable;
            if (table == null || !table.HasEntries)
                return false;

            var droppedAny = false;
            var entries = table.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (TryDropEntry(entries[i]))
                    droppedAny = true;
            }

            return droppedAny;
        }

private bool TryDropEntry(EnemyDropEntry entry)
        {
            if (entry == null || !entry.TryGetItem(out var item) || item == null || item.Prefab == null)
                return false;

            if (entry.chance <= 0f)
                return false;

            if (entry.chance < 1f && Random.value > entry.chance)
                return false;

            var amount = RollAmount(entry);
            var spawnPosition = GetSpawnPosition();
            var spawned = PoolManager.Instance.GetFromPool<ItemTypeHolder>(item.Prefab, null, spawnPosition);
            if (spawned == null)
            {
                this.LogWarning($"TryDropEntry() - failed to spawn drop item for '{item.name}'");
                return false;
            }

            if (!TryAssignRuntimeItemType(spawned, item))
                return false;

            spawned.SetAmount(amount);

            if (!spawned.TryGetComponent<IDropItem>(out var dropItem))
            {
                this.LogWarning($"TryDropEntry() - spawned object does not implement IDropItem. item: '{item.name}'");
                return false;
            }

            if (applyScatterForce)
                dropItem.ApplyScatterForce(scatterHorizontalForce, scatterUpForce);

            return true;
        }

private bool TryAssignRuntimeItemType(ItemTypeHolder spawned, ItemTypeSO expectedItem)
        {
            if (spawned == null || expectedItem == null)
                return false;

            if (spawned is IRuntimeTypeInjectable<ItemTypeSO> runtimeInjectable && runtimeInjectable.AllowRuntimeTypeInjection)
            {
                if (runtimeInjectable.TryForceInjectType(expectedItem, notifyDependents: true))
                    return true;

                this.LogWarning($"TryAssignRuntimeItemType() - runtime injection failed. expected: '{expectedItem.name}', spawned: '{spawned.name}'");
                return false;
            }

            if (object.ReferenceEquals(spawned.Type, expectedItem))
                return true;

            this.LogWarning($"TryAssignRuntimeItemType() - runtime injection is not available and type mismatched. expected: '{expectedItem.name}', current: '{spawned.Type?.name}'");
            return false;
        }


        private int RollAmount(EnemyDropEntry entry)
        {
            var min = Mathf.Max(1, entry.minAmount);
            var max = Mathf.Max(min, entry.maxAmount);
            return Random.Range(min, max + 1);
        }

        private Vector3 GetSpawnPosition()
        {
            var random = Random.insideUnitCircle * scatterRadius;
            return transform.position + spawnOffset + new Vector3(random.x, 0f, random.y);
        }
    }
}
