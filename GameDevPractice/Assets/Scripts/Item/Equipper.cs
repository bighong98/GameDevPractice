using System;
using System.Collections.Generic;
using TH.Combat;
using UnityEngine;
using UnityEngine.Pool;
using TH.Core.Pool;
using TH.Utils;
using TH.Resource;
using TH.Core.Service;

// 무기 장착/장착해제 시 무기 오브젝트 생성/생성해제(오브젝트 풀 기반)
// 풀링된 장착무기 오브젝트의 참조를 추적

namespace TH.Item
{
    public class Equipper : MonoBehaviour
    {
        private IEquipmentHolder equipHolder;
        private IFighter fighter; // serialize for debug
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;

        private bool isInit = false;
        private WeaponTypeHolder currentWeapon;
        private readonly Dictionary<WeaponTypeSO, ObjectPool<IPoolObject>> weaponPools = new();

        private const string DefaultRootName = "Root";
        private const string DefaultRightHandContainerName = "hand_r";
        private const string DefaultLeftHandContainerName = "hand_l";
        private const string DefaultRightWeaponContainerName = "weapon_r";
        private const string DefaultLeftWeaponContainerName = "weapon_l";
        
        private void Awake()
        {
            if (Util.FindChild(gameObject, DefaultRootName, recursive: true) is not { } root)
            {
                Logg.Log($"failed to find root for hand: {gameObject.name}");
                return;
            }
            
            if (rightHandTransform == null && 
                Util.FindChildContainName<Transform>(root, DefaultRightHandContainerName, true, false) is {} rResult)
            {
                var rightGo = (new GameObject(DefaultRightWeaponContainerName)).transform;
                rightGo.SetParent(rResult, worldPositionStays: false);
                rightHandTransform = rightGo;
            }

            if (leftHandTransform == null && 
                Util.FindChildContainName<Transform>(root, DefaultLeftHandContainerName, true, false) is {} lResult)
            {
                var leftGo = (new GameObject(DefaultLeftWeaponContainerName)).transform;
                leftGo.SetParent(lResult, worldPositionStays: false);
                leftHandTransform = leftGo;
            }

            TryGetComponent(out equipHolder);
            TryGetComponent(out fighter);
        }

        private void Start()
        {
            if (fighter == null || leftHandTransform == null || rightHandTransform == null)
            {
                Logg.LogError($"[{gameObject.name}] {nameof(Equipper)} failed to initialize");
                return;
            }
    
            isInit = true;
            
            fighter.OnEquipWeapon += this.OnEquipWeapon;
            if (fighter is { IsEquippingWeapon: true, GetWeaponEquipperInfo: ({ } weapon, { } anim) })
            {
                this.OnEquipWeapon(weapon, anim);
            }
        }

        private void OnEquipWeapon(WeaponTypeSO weaponType, Animator animator)
        {
            if (!isInit)
            {
                Logg.LogWarning($"[{gameObject.name}.Equipper] OnEquipWeapon called before initialization");
                return;
            }
            
            if (animator == null)
            {
                Logg.LogError($"[{gameObject.name}.Equipper] Animator is null");
                return;
            }

            if (currentWeapon?.Type is { } currWeaponType)
            {
                if (currWeaponType == weaponType)
                {
                    return; // 현재 장착중인 무기와 동일한 무기라면 중복 방지
                }
                
                DeSpawnWeapon();
            }

            if (!SpawnWeapon(weaponType))
            {
                Logg.LogError($"[{gameObject.name}.Equipper] Failed to spawn weapon: {weaponType?.name ?? "null"}");
                return;
            }
            
            if (weaponType.weaponAnimatorOverride is { } newWeaponAnimatorOverride)
            {
                animator.runtimeAnimatorController = newWeaponAnimatorOverride;
            }
            else if (animator.runtimeAnimatorController is AnimatorOverrideController overrideController)
            {
                animator.runtimeAnimatorController = overrideController.runtimeAnimatorController;
            }
        }

        private void DeSpawnWeapon()
        {
            if (currentWeapon != null && weaponPools.TryGetValue(currentWeapon.Type, out var pool))
            {
                pool.Release(currentWeapon);
            }
        }

        private bool SpawnWeapon(WeaponTypeSO weaponType)
        {
            if (!isInit) return false;
            
            if (!weaponPools.TryGetValue(weaponType, out var weaponPool))
            {
                weaponPool = PoolManager.Instance.GetPool(
                    weaponType.EquippedPrefab, 
                    GetHandGrip(weaponType), 
                    registerPool: false);
                weaponPools[weaponType] = weaponPool; // 풀 딕셔너리에 신규 풀 등록
            }
            
            if (weaponPool is { } pool && pool.Get() is WeaponTypeHolder result)
            {
                result.owner = fighter;
                currentWeapon = result;
                return true;
            }

            return false;
        }

        private Transform GetHandGrip(WeaponTypeSO weapon)
        {
            return weapon.GetGripHand switch
            {
                WeaponTypeSO.Hand.Right => rightHandTransform,
                WeaponTypeSO.Hand.Left => leftHandTransform,
                _ => rightHandTransform
            };
        }
    }
}
