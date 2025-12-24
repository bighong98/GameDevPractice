using System;
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
    
    public class MoverRefactoring : MonoBehaviour, ISavable
    {
        [SerializeField] private float maxSpeed = 6f;
        [SerializeField] private float speedFraction = 1f;
        
        private NavMeshAgent navMeshAgent;
        private Animator animator;
        
        private void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        public void SetDestination(Vector3 destination)
        {
            navMeshAgent.destination = destination;
        }

        public void Move()
        {
            navMeshAgent.speed = maxSpeed * Mathf.Clamp01(speedFraction);
            navMeshAgent.isStopped = false;
        }

        public void Stop()
        {
            navMeshAgent.isStopped = true;
        }

        public void CancelAction() => Stop();

        public void Moveto(Vector3 destination)
        {
            SetDestination(destination);
            Move();
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

