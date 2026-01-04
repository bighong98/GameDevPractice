using System;
using UnityEngine;
using TH.Resource;

namespace TH.Item
{
    public class EquippedWeapon : WeaponTypeHolder
    {
        [SerializeField] private Transform handle;
        [SerializeField] private Transform model;
        
        public Transform Handle => handle;
        public Transform Model => model;
        
        private void Start()
        {
            if (!Type.HasProjectile 
                || Type.GetProjectilePrefab is not { } projectilePrefab || !projectilePrefab.IsNotNull()) return;
            
            if (gameObject.GetOrAddComponent<ProjectileSpawner>() is { } projectileSpawner)
            {
                // todo: 필요하다면 투사체가 생성(onGet), 생성해제(onRelease)에 필요한 작업 추가
                projectileSpawner.InitializeProjectileSpawner(owner, Type);
                projectileSpawner.SetPool(projectilePrefab);
            };
        }
    }
}

