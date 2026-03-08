using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Combat.Drop;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "NPCTypeSO", menuName = "Scriptable Objects/Type/Character/NPCTypeSO")]
    public class NpcTypeSO : CharacterTypeSO, IHasEnemyDropTable
    {
        [Header("Drop")]
        [SerializeField] private AssetReferenceEnemyDropTableSO enemyDropTableReference;

        [System.NonSerialized] private EnemyDropTableSO enemyDropTable;
        [System.NonSerialized] private bool isDropTableInitialized;

        public EnemyDropTableSO EnemyDropTable => enemyDropTable;

        public override async UniTask InitializeAsync(CancellationToken token = default)
        {
            await base.InitializeAsync(token);

            if (isDropTableInitialized)
                return;

            if (enemyDropTableReference != null && enemyDropTableReference.RuntimeKeyIsValid())
            {
                var loadedDropTable = await GetStateFromAssetReference<EnemyDropTableSO>(enemyDropTableReference, token);
                if (loadedDropTable != null)
                    enemyDropTable = loadedDropTable;
            }

            isDropTableInitialized = true;
        }
    }
}

