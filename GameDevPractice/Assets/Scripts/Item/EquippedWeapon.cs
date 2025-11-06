using System;
using UnityEngine;
using TH.Resource;

namespace RPG.Item
{
    [RequireComponent(typeof(WeaponTypeHolder))]
    public class EquippedWeapon : MonoBehaviour
    {
        private void Start()
        {
            if (GetComponent<WeaponTypeHolder>() is not { Type: { } weaponType, owner: {} weaponOwner }) return;
            
            if (weaponType.HasProjectile && weaponType.GetProjectilePrefab is { } projectilePrefab)
            {
                if (gameObject.GetOrAddComponent<ProjectileSpawner>() is { } projectileSpawner)
                {
                    // todo: 필요하다면 투사체가 생성(onGet), 생성해제(onRelease)에 필요한 작업 추가
                    projectileSpawner.InitializeProjectileSpawner(weaponOwner, weaponType);
                    projectileSpawner.SetPool(projectilePrefab);
                };
            }
        }
    }
}

