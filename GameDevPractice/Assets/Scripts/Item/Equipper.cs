using System;
using System.Collections.Generic;
using RPG.Combat;
using UnityEngine;
using UnityEngine.Pool;

// 무기 장착/장착해제 시 무기 오브젝트 생성/생성해제(오브젝트 풀 기반)
// 풀링된 장착무기 오브젝트의 참조를 추적

namespace RPG.Item
{
    [RequireComponent(typeof(Fighter))]
    public class Equipper : MonoBehaviour
    {
        [SerializeField] private Fighter fighter; // serialize for debug
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;

        private bool isInit = false;
        private WeaponTypeHolder currentWeapon;
        private readonly Dictionary<WeaponTypeSO, ObjectPool<WeaponTypeHolder>> weaponPools = new();
        
        private void Awake()
        {
            if (Util.FindChild(gameObject, "Root", recursive: true) is not { } root)
            {
                Util.Log($"failed to find root for hand: {gameObject.name}");
                return;
            }
            
            if (rightHandTransform == null && 
                Util.FindChildContainName<Transform>(root, "hand_r", true, false) is {} rResult)
            {
                var rightGo = (new GameObject("weapon_r")).transform;
                rightGo.SetParent(rResult, worldPositionStays: false);
                rightHandTransform = rightGo;
            }

            if (leftHandTransform == null && 
                Util.FindChildContainName<Transform>(root, "hand_l", true, false) is {} lResult)
            {
                var leftGo = (new GameObject("weapon_l")).transform;
                leftGo.SetParent(lResult, worldPositionStays: false);
                leftHandTransform = leftGo;
            }
        }

        private void Start()
        {
            if (fighter == null) fighter = GetComponent<Fighter>();
            if (fighter == null || leftHandTransform == null || rightHandTransform == null)
            {
                Util.Log($"[{name}.{typeof(Equipper)}] failed to initialize");
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

            if (currentWeapon is { type: { } currWeaponType }) // 기존에 사용 중인 무기가 있었다면
            {
                if (currWeaponType == weaponType) return; // 현재 장착중인 무기와 동일한 무기라면 즉시 실행 중지 (중복 무기 생성 방지)
                
                DeSpawnWeapon(); // 다른 무기라면 기존 무기 비활성화
            }

            if (SpawnWeapon(weaponType)) // 새로 장착한 무기 생성(활성화)에 성공했다면
            {
                animator.runtimeAnimatorController = weaponType.weaponAnimatorOverride; // 해당 무기의 애니메이션 오버라이드 적용
            }
        }

        private void DeSpawnWeapon()
        {
            if (currentWeapon != null && weaponPools.TryGetValue(currentWeapon.type, out var pool))
            {
                pool.Release(currentWeapon);
            }
        }

        private bool SpawnWeapon(WeaponTypeSO weaponType)
        {
            if (!isInit) return false;
            
            if (!weaponPools.TryGetValue(weaponType, out var weaponPool))
            {
                weaponPool = PoolingManager.Instance.GetPool<WeaponTypeHolder>(
                    weaponType.EquippedPrefab, 
                    GetHandGrip(weaponType), 
                    registerPool: false);
                weaponPools[weaponType] = weaponPool; // 풀 딕셔너리에 신규 풀 등록
            }

            if (weaponPool is { } pool && pool.Get() is { } result)
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
