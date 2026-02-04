using System;
using System.Collections.Generic;
using TH.Combat;
using UnityEngine;
using UnityEngine.Pool;
using TH.Core.Pool;
using TH.Utils;
using TH.Resource;
using TH.Attribute.Stat;
using TH.Core.Service;

// 무기 장착/장착해제 시 무기 오브젝트 생성/생성해제(오브젝트 풀 기반)
// 풀링된 장착무기 오브젝트의 참조를 추적

namespace TH.Item
{
    public class Equipper : MonoBehaviour
    {
        [SerializeField] private AvatarAnchorProvider avatarAnchorProvider;
        [SerializeField] private Transform bodyRootTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;

        [SerializeField] private bool ignoreLocalPosition = false;
        
        private IFighter fighter;
        private IStatHolder statHolder;
        private Animator animator;

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
            FindAvatarAnchors();

            TryGetComponent(out fighter);
            TryGetComponent(out animator);
            TryGetComponent(out statHolder);
        }

        private void FindAvatarAnchors()
        {
            if (avatarAnchorProvider == null)
                avatarAnchorProvider = Util.FindChild<AvatarAnchorProvider>(gameObject, recursive: false);
            if (avatarAnchorProvider != null)
            {
                avatarAnchorProvider.EnsureInitialized();
                bodyRootTransform = avatarAnchorProvider.BodyRootTransform;
                rightHandTransform = avatarAnchorProvider.RightHandTransform;
                leftHandTransform = avatarAnchorProvider.LeftHandTransform;
                return;
            }

            if (bodyRootTransform == null)
                bodyRootTransform = Util.FindChild<Transform>(gameObject, DefaultRootName, recursive: true);
            if (bodyRootTransform == null)
            {
                this.LogError($"failed to find avatar root");
                return;
            }

            var root = bodyRootTransform.gameObject;
            
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
        }

        private void Start()
        {
            if (fighter == null || leftHandTransform == null || rightHandTransform == null)
            {
                Logg.LogError($"[{gameObject.name}] {nameof(Equipper)} failed to initialize");
                return;
            }
    
            isInit = true;
            
            fighter.OnEquipWeapon += this.HandleOnEquipWeapon;
            if (fighter is { IsEquippingWeapon: true, GetEquippedWeaponInfo: {} weapon })
            {
                this.HandleOnEquipWeapon(weapon);
            }
        }

        // private void OnEquipWeapon(WeaponTypeSO weaponType, Animator animator)
        private void HandleOnEquipWeapon(WeaponTypeSO weaponType)
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

            if (currentWeapon.IsNotNull())
            {
                // 현재 장착중인 무기와 동일한 무기라면 중복 (장착해제 -> 장착) 방지
                if (currentWeapon.Type == weaponType) return;
                // 기존 무기 디스폰
                DeSpawnWeapon();
            }

            if (!SpawnWeapon(weaponType))
            {
                Logg.LogError($"[{gameObject.name}.Equipper] Failed to spawn weapon: {weaponType?.name ?? "null"}");
                return;
            }

            OverrideWeaponAnimator(weaponType);
        }

        private void OverrideWeaponAnimator(WeaponTypeSO weaponType)
        {
            if (weaponType.IsNull())
            {
                this.LogWarning($"{nameof(OverrideWeaponAnimator)} - invalid weaponType data", context: this);
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

        private readonly Dictionary<WeaponTypeHolder, List<(Transform, Vector3, Quaternion)>> _cachedLocalTransforms = new();
        
        
        private bool SpawnWeapon(WeaponTypeSO weaponType)
        {
            if (!isInit) return false;

            if (!weaponPools.TryGetValue(weaponType, out var weaponPool))
            {
                weaponPool = PoolManager.Instance.GetPool(
                    weaponType.EquippedPrefab,
                    GetHandGrip(weaponType),
                    registerPool: false);

                weaponPools[weaponType] = weaponPool;
            }

            if (weaponPool is not { } pool || pool.Get() is not EquippedWeapon result)
                return false;

            result.owner = fighter;
            EnsureWeaponProjectileSpawner(weaponType, result);

            currentWeapon = result;

            if (!ignoreLocalPosition) return true;

            var root = result.transform;

            // 1) 자식 로컬 트랜스폼 캐싱
            var cached = new List<(Transform, Vector3, Quaternion)>();
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t == root) continue;
                cached.Add((t, t.localPosition, t.localRotation));
            }
            _cachedLocalTransforms[result] = cached;

            // 2) handle/modeling은 보정에 쓰이므로 "제로잉 대상에서 제외"
            var modeling = result.Model;
            var handle = result.Handle;

            // 3) 루트는 건드리지 않고, modeling의 localPosition/localRotation만 조정해서
            //    handle이 grip(=root의 부모) 기준으로 (0, identity)에 오도록 보정
            if (!modeling || !handle) return true;

            // handle의 "modeling 로컬 기준" 위치/회전 (현재 포즈 1회 계산)
            var handlePosInModeling = modeling.InverseTransformPoint(handle.position);
            var handleRotInModeling = Quaternion.Inverse(modeling.rotation) * handle.rotation;

            // 목표:
            // (modelingLocalRot * handleRotInModeling) == identity
            // (modelingLocalPos + modelingLocalRot * handlePosInModeling) == zero
            var modelingLocalRot = Quaternion.Inverse(handleRotInModeling);
            modeling.localRotation = modelingLocalRot;
            modeling.localPosition = -(modelingLocalRot * handlePosInModeling);

            result.transform.localRotation = Quaternion.Euler(90f, 0f, -180f);

            return true;
        }

        private void EnsureWeaponProjectileSpawner(WeaponTypeSO weaponType, EquippedWeapon result)
        {
            this.Log($"EnsureWeaponProjectileSpawner() invoked weaponType: {weaponType}, equippedWeaponInstance: {result}", Logg.LoggingMode.Completed);
            if (weaponType.IsNull()) return; 
            if (weaponType is not { HasProjectile: true, GetProjectilePrefab: {} projectilePrefab}) return;
            if (projectilePrefab.IsNull()) return;
            
            AttackSource attackSource;
            if (statHolder == null)
            {
                this.LogWarning($"{nameof(EnsureWeaponProjectileSpawner)} - no IStatHolder found, fallback to 1 damage", context: this);
                attackSource = new AttackSource(fighter, 1f, weaponType.DamageType);
            }
            else
            {
                var statSO = weaponType.AttackSourceStatSO;
                if (statSO.IsNull())
                {
                    this.LogWarning($"{nameof(EnsureWeaponProjectileSpawner)} - invalid AttackSourceStatSO, falling back to AD", context: this);
                    statSO = GameStats.AD;
                }

                if (statSO.IsNotNull() && statHolder.TryGetStat(statSO, out var attackSourceStat))
                {
                    attackSource = new AttackSource(fighter, attackSourceStat, weaponType.DamageType);
                }
                else
                {
                    this.LogWarning($"{nameof(EnsureWeaponProjectileSpawner)} - failed to get stat by {statSO}, fallback to 1 damage", context: this);
                    attackSource = new AttackSource(fighter, 1f, weaponType.DamageType);
                }
            }

            var projectileSpawner = result.gameObject.GetOrAddComponent<ProjectileSpawner>();
            projectileSpawner.InitializeProjectileSpawner(fighter, weaponType, attackSource);
            if (projectileSpawner.pool == null)
            {
                projectileSpawner.SetPool(projectilePrefab);
            }
        }

        private void DeSpawnWeapon()
        {
            if (currentWeapon == null) return;

            // 캐시가 있으면(= ignoreLocalPosition 케이스였으면) 원복
            if (_cachedLocalTransforms.TryGetValue(currentWeapon, out var cached))
            {
                foreach (var (t, pos, rot) in cached)
                {
                    if (!t) continue;
                    t.SetLocalPositionAndRotation(pos, rot);
                }
                _cachedLocalTransforms.Remove(currentWeapon);
            }

            if (weaponPools.TryGetValue(currentWeapon.Type, out var pool))
            {
                pool.Release(currentWeapon);
            }

            currentWeapon = null;
        }




        private Transform GetHandGrip(WeaponTypeSO weapon)
        {
            return weapon.GripHand switch
            {
                WeaponTypeSO.Hand.Right => rightHandTransform,
                WeaponTypeSO.Hand.Left => leftHandTransform,
                _ => rightHandTransform
            };
        }
    }
}
