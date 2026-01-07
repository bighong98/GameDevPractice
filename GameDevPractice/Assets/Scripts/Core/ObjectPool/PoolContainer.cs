using UnityEngine;
using System;
using System.Collections.Generic;
using TH.Utils;

namespace TH.Core.Pool
{
    [Serializable]
    public class PoolContainer
    {
        [SerializeField] private Transform TopParent;
        private readonly Dictionary<Type, Transform> TypeContainerDictionary = new Dictionary<Type, Transform>();
        private readonly Dictionary<GameObject, Transform> PoolContainerDictionary = new Dictionary<GameObject, Transform>();

        public void Init(Transform parent)
        {
            TopParent = parent;
        }

public Transform GetPoolContainer(GameObject prefab, Type type)
        {
            // 기존 컨테이너가 있고 유효하면 반환
            if (PoolContainerDictionary.TryGetValue(prefab, out var existingPoolContainer))
            {
                // 컨테이너가 파괴되었으면 딕셔너리에서 제거하고 새로 생성
                if (existingPoolContainer == null)
                {
                    PoolContainerDictionary.Remove(prefab);
                }
                else
                {
                    return existingPoolContainer;
                }
            }
            
            GameObject newPoolContainer = new GameObject($"{prefab.name}");
            newPoolContainer.transform.SetParent(GetTypePoolContainer(type));
            return PoolContainerDictionary[prefab] = newPoolContainer.transform;
        }
        
private Transform GetTypePoolContainer(Type type)
        {
            // 기존 타입 컨테이너가 있고 유효하면 반환
            if (TypeContainerDictionary.TryGetValue(type, out var existingTypeContainer))
            {
                // 컨테이너가 파괴되었으면 딕셔너리에서 제거하고 새로 생성
                if (existingTypeContainer == null)
                {
                    TypeContainerDictionary.Remove(type);
                }
                else
                {
                    return existingTypeContainer;
                }
            }
            
            GameObject newTypeContainer = new GameObject($"{type.Name}s");
            newTypeContainer.transform.SetParent(TopParent);
            return TypeContainerDictionary[type] = newTypeContainer.transform;
        }
        
        public Transform GetPoolContainer<T>(GameObject prefab) where T : Component, IPoolObject
        {
            if (PoolContainerDictionary.TryGetValue(prefab, out var existingPoolContainer))
            {
                return existingPoolContainer;
            }

            GameObject newPoolContainer = new GameObject($"{prefab.name}");
            newPoolContainer.transform.SetParent(GetTypePoolContainer<T>());
            return PoolContainerDictionary[prefab] = newPoolContainer.transform;
        }
        private Transform GetTypePoolContainer<T>() where T : Component, IPoolObject
        {
            if (TypeContainerDictionary.TryGetValue(typeof(T), out var existingTypeContainer))
            {
                return existingTypeContainer;
            }
            
            GameObject newTypeContainer = new GameObject($"{typeof(T).Name}s");
            newTypeContainer.transform.SetParent(TopParent);
            return TypeContainerDictionary[typeof(T)] = newTypeContainer.transform;
        }

        public void ReleaseAllPooledObjects()
        {
            Logg.Log($"[{GetType().Name}] Releasing all pooled objects", Logg.LoggingMode.Completed);
            foreach (var poolContainer in PoolContainerDictionary.Values)
            {
                if (poolContainer.childCount == 0) continue; // 풀 컨테이너에 오브젝트 풀이 없으면 스킵
                
                for (int i = 0; i < poolContainer.childCount; i++)
                {
                    var child = poolContainer.GetChild(i);
                    Logg.Log($"[{GetType().Name}] Releasing {child.gameObject.name}", Logg.LoggingMode.Completed);
                    if (child.gameObject.activeSelf && child.GetComponent<IPoolObject>() is { Origin: not null } pooledObject)
                    {
                        pooledObject.ReleaseSelf();
                    }
                }
            }
        }

        public void Clear()
        {
            TypeContainerDictionary.Clear();
            PoolContainerDictionary.Clear();
        }
    }
}


