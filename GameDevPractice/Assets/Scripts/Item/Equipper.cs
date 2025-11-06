using System;
using System.Collections.Generic;
using RPG.Combat;
using UnityEngine;
using UnityEngine.Pool;
using TH.Core.Pool;
using TH.Item;
using TH.Utils;
using TH.Resource;

// 무기 장착/장착해제 시 무기 오브젝트 생성/생성해제(오브젝트 풀 기반)
// 풀링된 장착무기 오브젝트의 참조를 추적

namespace RPG.Item
{
    [RequireComponent(typeof(Fighter))]
    [RequireComponent(typeof(IEquipmentHolder))]
    public class Equipper : MonoBehaviour
    {
        private IEquipmentHolder equipHolder;
        [SerializeField] private Fighter fighter; // serialize for debug
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;

        private bool isInit = false;
        private WeaponTypeHolder currentWeapon;
        // private readonly Dictionary<WeaponTypeSO, ObjectPool<WeaponTypeHolder>> weaponPools = new();
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

            TryGetComponent<IEquipmentHolder>(out equipHolder);
            if (fighter == null) TryGetComponent<Fighter>(out fighter);
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
            if (!isInit) return;
            if (animator == null) return;

            if (currentWeapon is { Type: { } currWeaponType }) // 기존에 사용 중인 무기가 있었다면
            {
                if (currWeaponType == weaponType) return; // 현재 장착중인 무기와 동일한 무기라면 즉시 실행 중지 (중복 무기 생성 방지)
                
                DeSpawnWeapon(); // 다른 무기라면 기존 무기 비활성화
            }

            if (!SpawnWeapon(weaponType)) return; // 새로 장착한 무기 생성(활성화)에 실패했다면 즉시 실행 중지
            
            if (weaponType.weaponAnimatorOverride is {} newWeaponAnimatorOverride) // 새로 장착한 무기의 weaponAnimatorOverride가 비어있지 않다면 (!= null)
            {
                animator.runtimeAnimatorController = newWeaponAnimatorOverride; // 해당 무기의 애니메이션 오버라이드 적용
            }
            else if (animator.runtimeAnimatorController is AnimatorOverrideController { } overrideController) 
                // 새로 장착한 무기의 weaponAnimatorOverride가 비어있다면 (== null)
                // 또한 현재 runtimeAnimatorController가 override 된 적이 있다면    
            {
                // AnimatorOverrideController.runtimeAnimatorController : 원본 AnimatorController의 참조를 가지고 있음
                // 해당 참조를 통해 Override 되기 전으로 원복
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
                // weaponPool = PoolingManager.Instance.GetPool<WeaponTypeHolder>(
                //     weaponType.EquippedPrefab, 
                //     GetHandGrip(weaponType), 
                //     registerPool: false);
                weaponPool = PoolManager.Instance.GetPool(
                    weaponType.EquippedPrefab, 
                    GetHandGrip(weaponType), 
                    registerPool: false);
                weaponPools[weaponType] = weaponPool; // 풀 딕셔너리에 신규 풀 등록
            }

            // if (weaponPool is { } pool && pool.Get() is { } result)
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
