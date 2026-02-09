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
        [SerializeField] private AvatarAnchorProvider avatarAnchorProvider;
        [SerializeField] private Transform bodyRootTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;

        [SerializeField] private bool ignoreLocalPosition = false;
        private IFighter fighter;
        private EquipmentHolder equipHolder;
        private ISkillController skillController;

        private bool isInit = false;

        private WeaponTypeSO currentWeaponType;
        private WeaponTypeHolder currentRightWeapon;
        private WeaponTypeHolder currentLeftWeapon;
        private readonly Dictionary<(WeaponTypeSO, WeaponTypeSO.Hand), ObjectPool<IPoolObject>> weaponPools = new();

        private const string DefaultRootName = "Root";
        private const string DefaultRightHandContainerName = "hand_r";
        private const string DefaultLeftHandContainerName = "hand_l";
        private const string DefaultRightWeaponContainerName = "weapon_r";
        private const string DefaultLeftWeaponContainerName = "weapon_l";
        
        private void Awake()
        {
            FindAvatarAnchors();

            TryGetComponent(out fighter);
            TryGetComponent(out equipHolder);
            TryGetComponent(out skillController);
        }

        private void Start()
        {
            if (fighter == null || equipHolder == null || leftHandTransform == null || rightHandTransform == null)
            {
                Logg.LogError($"[{gameObject.name}] {nameof(Equipper)} failed to initialize");
                return;
            }
    
            isInit = true;
            
            equipHolder.OnEquipWeapon += this.HandleOnEquipWeapon;
            if (equipHolder is { IsEquippingWeapon: true, GetEquippedWeaponInfo: {} weapon })
            {
                this.HandleOnEquipWeapon(weapon);
            }
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

        private void HandleOnEquipWeapon(WeaponTypeSO weaponType)
        {
            if (!isInit)
            {
                Logg.LogWarning($"[{gameObject.name}.Equipper] OnEquipWeapon called before initialization");
                return;
            }

            if (weaponType.IsNull())
            {
                Logg.LogError($"[{gameObject.name}.Equipper] WeaponType is null");
                return;
            }

            if (currentWeaponType == weaponType &&
                (currentRightWeapon.IsNotNull() || currentLeftWeapon.IsNotNull()))
            {
                return;
            }

            if (currentRightWeapon.IsNotNull() || currentLeftWeapon.IsNotNull())
            {
                DeSpawnWeapon();
            }

            if (!SpawnWeapon(weaponType))
            {
                Logg.LogError($"[{gameObject.name}.Equipper] Failed to spawn weapon: {weaponType?.name ?? "null"}");
                return;
            }

        }

        private readonly Dictionary<WeaponTypeHolder, List<(Transform, Vector3, Quaternion)>> _cachedLocalTransforms = new();
        
        
        private bool SpawnWeapon(WeaponTypeSO weaponType)
        {
            if (!isInit || weaponType.IsNull()) return false;

            bool SpawnOnHand(WeaponTypeSO.Hand hand, ref WeaponTypeHolder slot)
            {
                var key = (weaponType, hand);
                if (!weaponPools.TryGetValue(key, out var weaponPool))
                {
                    var prefab = (hand == WeaponTypeSO.Hand.Left)
                        ? weaponType.EquippedPrefabLeft
                        : weaponType.EquippedPrefab;

                    if (prefab.IsNull()) return false;

                    weaponPool = PoolManager.Instance.GetPool(
                        prefab,
                        GetHandGrip(hand),
                        registerPool: false);

                    weaponPools[key] = weaponPool;
                }

                if (weaponPool is not { } pool || pool.Get() is not EquippedWeapon result)
                    return false;

                result.owner = fighter;
                EnsureWeaponProjectileSpawner(weaponType, result);

                slot = result;

                if (ignoreLocalPosition)
                    ApplyIgnoreLocalPosition(result);

                return true;
            }

            bool spawned;
            switch (weaponType.GripHand)
            {
                case WeaponTypeSO.Hand.Right:
                    spawned = SpawnOnHand(WeaponTypeSO.Hand.Right, ref currentRightWeapon);
                    break;
                case WeaponTypeSO.Hand.Left:
                    spawned = SpawnOnHand(WeaponTypeSO.Hand.Left, ref currentLeftWeapon);
                    break;
                case WeaponTypeSO.Hand.Both:
                    if (!SpawnOnHand(WeaponTypeSO.Hand.Right, ref currentRightWeapon))
                        return false;

                    if (!SpawnOnHand(WeaponTypeSO.Hand.Left, ref currentLeftWeapon))
                    {
                        DeSpawnWeapon();
                        return false;
                    }

                    spawned = true;
                    break;
                default:
                    spawned = false;
                    break;
            }

            if (spawned)
                currentWeaponType = weaponType;

            return spawned;

            void ApplyIgnoreLocalPosition(EquippedWeapon result)
            {
                var root = result.transform;

                var cached = new List<(Transform, Vector3, Quaternion)>();
                foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t == root) continue;
                    cached.Add((t, t.localPosition, t.localRotation));
                }
                _cachedLocalTransforms[result] = cached;

                var modeling = result.Model;
                var handle = result.Handle;

                if (!modeling || !handle) return;

                var handlePosInModeling = modeling.InverseTransformPoint(handle.position);
                var handleRotInModeling = Quaternion.Inverse(modeling.rotation) * handle.rotation;

                var modelingLocalRot = Quaternion.Inverse(handleRotInModeling);
                modeling.localRotation = modelingLocalRot;
                modeling.localPosition = -(modelingLocalRot * handlePosInModeling);

                result.transform.localRotation = Quaternion.Euler(90f, 0f, -180f);
            }
        }

        private void DeSpawnWeapon()
        {
            void ReleaseOnHand(WeaponTypeSO.Hand hand, ref WeaponTypeHolder weapon)
            {
                if (weapon == null) return;

                if (_cachedLocalTransforms.TryGetValue(weapon, out var cached))
                {
                    foreach (var (t, pos, rot) in cached)
                    {
                        if (!t) continue;
                        t.SetLocalPositionAndRotation(pos, rot);
                    }
                    _cachedLocalTransforms.Remove(weapon);
                }

                if (weaponPools.TryGetValue((weapon.Type, hand), out var pool))
                {
                    pool.Release(weapon);
                }

                weapon = null;
            }

            ReleaseOnHand(WeaponTypeSO.Hand.Right, ref currentRightWeapon);
            ReleaseOnHand(WeaponTypeSO.Hand.Left, ref currentLeftWeapon);
            currentWeaponType = null;
        }

        private void EnsureWeaponProjectileSpawner(WeaponTypeSO weaponType, EquippedWeapon result)
        {
            this.Log($"EnsureWeaponProjectileSpawner() invoked weaponType: {weaponType}, equippedWeaponInstance: {result}", Logg.LoggingMode.Completed);
            if (weaponType.IsNull() || result.IsNull()) return;

            var skill = weaponType.DefaultSkill;
            if (skill.IsNull() || !skill.HasProjectile || skill.ProjectilePrefab.IsNull()) return;
            if (skillController.IsNull()) return;
            if (!skillController.TryBuildPreviewAttackSource(fighter, out var attackSource)) return;

            var projectileSpawner = result.gameObject.GetOrAddComponent<ProjectileSpawner>();
            projectileSpawner.InitializeProjectileSpawner(fighter, skill, attackSource);
            if (projectileSpawner.pool == null)
            {
                projectileSpawner.SetPool(skill.ProjectilePrefab);
            }
        }


        private Transform GetHandGrip(WeaponTypeSO.Hand hand)
        {
            return hand switch
            {
                WeaponTypeSO.Hand.Right => rightHandTransform,
                WeaponTypeSO.Hand.Left => leftHandTransform,
                _ => rightHandTransform
            };
        }
    }
}
