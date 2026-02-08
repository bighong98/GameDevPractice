using System;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.SaveLoad;
using TH.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Movement
{
    [Serializable]
    public struct MoverSaveData
    {
        public SerializableVector3 position;
        public SerializableVector3 rotation;
    }
    
    public class Mover : MonoBehaviour, ISavable, IMover
    {
        [SerializeField] private GameStatSO moveSpeedStatSO;
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 6f;
        [SerializeField] private float speedFraction = 1f;
        
        private NavMeshAgent navMeshAgent;
        private Health health;
        private IStatHolder statHolder;
        private IGameStat moveSpeedStat;
        
        public event Action OnDestinationSet;   
        public event Action OnArrived;
        
        private Vector3 currentDestination = Vector3.zero;
        private const float distanceTolerance = 2.0f;

        private void Awake()
        {
            TryGetComponent(out navMeshAgent);
            TryGetComponent(out health);
            TryGetComponent(out statHolder);

            if (moveSpeedStatSO.IsNull() || moveSpeedStatSO.LegacyId == default)
                this.LogWarning($"[{gameObject.name}.{GetType().Name}] invalid moveSpeedStatSO", context: this);
        }

        private void OnEnable()
        {
            SyncMoveSpeedStat();
        }

        private void OnDisable()
        {
            UnSyncMoveSpeedStat();
        }

        private void Update()
        {
            if (health.IsDead) return;
            if (currentDestination == Vector3.zero) return;
            
            float distanceToWaypoint = Vector3.SqrMagnitude(transform.position - currentDestination);
            if (distanceToWaypoint < distanceTolerance)
            {
                OnArrived?.Invoke();
            }
        }

        public void SetDestination(Vector3 destination, bool notify = true)
        {
            if (destination == Vector3.zero) return;
            
            currentDestination = destination;
            if (notify) OnDestinationSet?.Invoke();
        }

        public void Move(MoveType moveType = MoveType.Run)
        {
            if (currentDestination == Vector3.zero) return;

            navMeshAgent.destination = currentDestination;
            navMeshAgent.speed = (moveType == MoveType.Run ? runSpeed : walkSpeed) * Mathf.Clamp01(speedFraction);
            navMeshAgent.isStopped = false;
        }

        public void Stop()
        {
            navMeshAgent.isStopped = true;
        }

        public void ResetMovementState()
        {
            currentDestination = Vector3.zero;

            if (navMeshAgent == null) return;

            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
        }


        public void CancelAction() => Stop();

        public void MoveTo(Vector3 destination, MoveType moveType = MoveType.Run, bool notify = true)
        {
            SetDestination(destination, notify);
            Move(moveType);
        }
        
        private void SyncMoveSpeedStat()
        {
            if (statHolder.IsNull()) return;
            if (moveSpeedStatSO.IsNull() || moveSpeedStatSO.LegacyId == default) return;

            if (statHolder.BindEvent(moveSpeedStatSO, OnMoveSpeedStatDirty) is { } bindResult)
            {
                moveSpeedStat = bindResult;
                OnMoveSpeedStatDirty();
            }
        }

        private void UnSyncMoveSpeedStat()
        {
            if (statHolder.IsNull() || moveSpeedStatSO.IsNull()) return;

            statHolder.UnBindEvent(moveSpeedStatSO, OnMoveSpeedStatDirty);
            moveSpeedStat = null;
        }

        private void OnMoveSpeedStatDirty()
        {
            if (statHolder == null) return;
            if (moveSpeedStat == null && !statHolder.TryGetStat(moveSpeedStatSO, out moveSpeedStat))
            {
                this.LogWarning($"OnMoveSpeedStatDirty() - failed to get move speed stat from statholder. moveSpeedStatSO: {moveSpeedStatSO}", context: this);
                return;
            }

            walkSpeed = moveSpeedStat.Value;
            runSpeed = moveSpeedStat.Value * 2f;
        }

        #region ISavable
        
        public object CaptureState()
        {
            MoverSaveData data = new MoverSaveData
            {
                position = new SerializableVector3(transform.position),
                rotation = new SerializableVector3(transform.eulerAngles)
            };

            return data;
        }

        public bool RestoreState(object state)
        {
            if (state is not MoverSaveData data) return false;
            if (!TryGetComponent(out navMeshAgent)) return false;
            
            // NavMeshAgent의 transform 간섭 차단 방지
            navMeshAgent.enabled = false; 
            
            this.Log($"({gameObject.name}) - set position: {data.position.ToVector()}, set rotation: {data.rotation.ToVector()}", Logg.LoggingMode.Completed);
            transform.position = data.position.ToVector();
            transform.eulerAngles = data.rotation.ToVector();
            
            navMeshAgent.enabled = true;
            return true;
        }

        #endregion
    }
}

