using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Core
{
    public class PersistentObjectSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject persistentObjectPrefab;
        private static bool alreadySpawned = false;
        
        private void Awake()
        {
            if (alreadySpawned) return;
            if (persistentObjectPrefab == null) return;
            
            Spawn();
            alreadySpawned = true;
        }

        private void Spawn()
        {
            GameObject po = Instantiate(persistentObjectPrefab);
            DontDestroyOnLoad(po);
        }
    }
}

