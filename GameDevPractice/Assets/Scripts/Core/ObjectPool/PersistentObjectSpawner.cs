using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.Core
{
    public class PersistentObjectSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject persistentObjectPrefab;
        private static bool _alreadySpawned = false;
        private CancellationToken token;
        
        private void Awake()
        {
            if (_alreadySpawned) return;
            if (persistentObjectPrefab == null) return;

            token = destroyCancellationToken;
            Spawn().Forget();
        }

        private async UniTask Spawn()
        {
            try
            {
                var result = await InstantiateAsync(persistentObjectPrefab).ToUniTask(cancellationToken: token);
                if (result?.GetValue(0) is GameObject po)
                {
                    DontDestroyOnLoad(po);
                    _alreadySpawned = true;
                }
            }
            catch (Exception e) { Util.LogError($"[{nameof(PersistentObjectSpawner)}] error occurred while spawning persistent objects. {e}"); }
            finally { Destroy(gameObject); } // 최종적으로 스포너(자기자신) 파괴
        }
    }
}

