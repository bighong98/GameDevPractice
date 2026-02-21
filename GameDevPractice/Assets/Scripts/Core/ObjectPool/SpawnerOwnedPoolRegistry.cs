using System.Collections.Generic;
using UnityEngine.Pool;

namespace TH.Core.Pool
{
    public static class SpawnerOwnedPoolRegistry
    {
        private static readonly Dictionary<IPoolObject, ObjectPool<IPoolObject>> ownerPools = new();

        public static void Attach(IPoolObject pooledObject, ObjectPool<IPoolObject> ownerPool)
        {
            if (pooledObject == null || ownerPool == null)
                return;

            ownerPools[pooledObject] = ownerPool;
        }

        public static void Detach(IPoolObject pooledObject)
        {
            if (pooledObject == null)
                return;

            ownerPools.Remove(pooledObject);
        }

        public static bool TryRelease(IPoolObject pooledObject)
        {
            if (pooledObject == null)
                return false;

            if (!ownerPools.TryGetValue(pooledObject, out var ownerPool) || ownerPool == null)
                return false;

            ownerPool.Release(pooledObject);
            return true;
        }
    }
}
