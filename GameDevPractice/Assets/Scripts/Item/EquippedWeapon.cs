using System;
using UnityEngine;
using TH.Resource;
using TH.Utils;

namespace TH.Item
{
    public class EquippedWeapon : WeaponTypeHolder
    {
        [SerializeField] private Transform handle;
        [SerializeField] private Transform model;
        
        public Transform Handle => handle;
        public Transform Model => model;
        
        #region deprecated

        /// <summary> Equipper.cs에서 EnsureWeaponProjectileSpawner() 대신 실행
        /// 추후 투사체 오브젝트 풀(ProjectileSpawner) 초기화에 필요한 데이터나 기능이 추가된다면,
        /// EquippedWeapon.cs에서 EnsureWeaponProjectileSpawner()를 비롯한 초기화 로직을 구현하고
        /// Equipper.cs에서 인터페이스 기반으로 호출만 하는 형식으로 변경 필요
        /// </summary>

        // protected override async void Start()
        // {
        //     try
        //     {
        //         await InitializeTypeAsync();

        //         EnsureWeaponProjectileSpawner();
        //     }
        //     catch (Exception e)
        //     {
        //         this.LogError($"{e}", this);
        //     }
        // }

        // private void EnsureWeaponProjectileSpawner()
        // {
        //     if (!Type.HasProjectile || 
        //         Type.GetProjectilePrefab is not { } projectilePrefab || 
        //         !projectilePrefab.IsNotNull()) return ;

        //     if (!isActiveAndEnabled || owner.IsNull()) return ;
        //     if (owner.GetWeaponEquipperInfo.weapon != Type) return ;

        //     var projectileSpawner = gameObject.GetOrAddComponent<ProjectileSpawner>();
        //     // todo: 필요하다면 투사체가 생성(onGet), 생성해제(onRelease)에 필요한 작업 추가
        //     projectileSpawner.InitializeProjectileSpawner(owner, Type);
        //     if (projectileSpawner.pool == null)
        //     {
        //         projectileSpawner.SetPool(projectilePrefab);
        //     }

        //     return ;
        // }

        #endregion
    }
}

