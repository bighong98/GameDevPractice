using System.Collections.Generic;
using TH.Combat;
using TH.Core.Pool;
using UnityEngine;
using UnityEngine.Pool;
using TH.Resource;

namespace TH.Item
{
    public class WeaponSpawner : IWeaponSpawner
    {
        public Transform RightHand { get; private set; }
        public Transform LeftHand { get; private set; }
        public GameObject EquippedWeapon { get; private set; }

        private WeaponTypeHolder currentWeapon;
        private static readonly Dictionary<WeaponTypeSO, ObjectPool<IPoolObject>> weaponPools = new();
        
        public void SetHandRoot(WeaponTypeSO.Hand hand, Transform trs)
        {
            if (trs == null) return;
            switch (hand)
            {
                case WeaponTypeSO.Hand.Right:
                    RightHand = trs;
                    break;
                case WeaponTypeSO.Hand.Left:
                    LeftHand = trs;
                    break;
                default: break;
            }
        }

        public void SpawnWeapon(WeaponTypeSO data)
        {
            if (!weaponPools.TryGetValue(data, out var weaponPool))
            {
                weaponPool = PoolManager.Instance.GetPool(
                    data.EquippedPrefab, 
                    GetHandGrip(data), 
                    registerPool: false);
                weaponPools[data] = weaponPool; // 풀 딕셔너리에 신규 풀 등록
            }

            if (weaponPool is { } pool && pool.Get() is WeaponTypeHolder spawned)
            {
                currentWeapon = spawned;
                EquippedWeapon = spawned.gameObject;
            }
        }

        public void DespawnWeapon(WeaponTypeSO data)
        {
            throw new System.NotImplementedException();
        }

        public void DespawnWeapon()
        {
            if (currentWeapon == null) return;
            if (!weaponPools.TryGetValue(currentWeapon.Type, out var pool)) return;
            
            pool.Release(currentWeapon);
        }

        private Transform GetHandGrip(WeaponTypeSO weapon)
        {
            return weapon.GetGripHand switch
            {
                WeaponTypeSO.Hand.Right => RightHand,
                WeaponTypeSO.Hand.Left => LeftHand,
                _ => RightHand
            };
        }
    }
}

