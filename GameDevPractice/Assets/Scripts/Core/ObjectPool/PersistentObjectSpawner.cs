using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TH.Utils;

namespace TH.Core
{
    public class PersistentObjectSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject persistentObjectPrefab;
        private static bool _alreadySpawned = false;
        private CancellationToken token;
        
        private void Awake()
        {
            if (_alreadySpawned)
            {
                Destroy(gameObject);
                return;
            }
            if (persistentObjectPrefab == null) return;

            token = destroyCancellationToken;
            // Spawn().Forget();
        }

        private async void Start()
        {
            try {await Spawn();}
            catch (Exception e) { Logg.LogError(e); }
        }

        private async UniTask Spawn()
        {
            GameObject[] result = new GameObject[] { };
            try
            {
                result = await InstantiateAsync(persistentObjectPrefab).ToUniTask(cancellationToken: token);
                if (result?.GetValue(0) is GameObject po)
                {
                    DontDestroyOnLoad(po);
                    _alreadySpawned = true;
                }
            }
            catch (Exception e)
            {
                Logg.LogWarning($"[{nameof(PersistentObjectSpawner)}] spawning persistent objects stopped. {e}");
                _alreadySpawned = false;
                if (result is { Length: > 0 })
                {
                    foreach (var g in result)
                        Destroy(g);
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    Destroy(gameObject);
            } // 최종적으로 스포너(자기자신) 파괴
        }
    }
}

