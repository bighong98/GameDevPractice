using System;
using System.Collections;
using System.Collections.Generic;
using RPG.Core;
using RPG.Saving;
using RPG.Attribute;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.Movement
{
    [Serializable]
    public struct MoverSaveData
    {
        public SerializableVector3 position;
        public SerializableVector3 rotation;
    }
    
    public class Mover : MonoBehaviour, IAction, ISavable
    {
        [SerializeField] private float maxSpeed = 6f;
        
        private NavMeshAgent navMeshAgent;
        private Animator animator;
        private Health health;
        
        private static readonly int ForwardSpeed = Animator.StringToHash("forwardSpeed");
        
        public ActoinScheduler ActionScheduler { get; private set; }

        private void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            health = GetComponent<Health>();
            
            ActionScheduler = GetComponent<ActoinScheduler>();
        }

        private void Update()
        {
            navMeshAgent.enabled = !health.IsDead;
            UpdateAnimator();
        }

        public void StartMoveAction(Vector3 dest, float speedFraction = 1f)
        {
            ActionScheduler.StartAction(this);
            Moveto(dest, speedFraction);
        }
        
        public void Moveto(Vector3 dest, float speedFraction = 1f)
        {
            navMeshAgent.destination = dest;
            navMeshAgent.speed = maxSpeed * Mathf.Clamp01(speedFraction);
            navMeshAgent.isStopped = false;
        }

        public void Cancel()
        {
            navMeshAgent.isStopped = true;
        }

        private void UpdateAnimator()
        {
            Vector3 velocity = navMeshAgent.velocity;
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float speed = localVelocity.z;
            animator.SetFloat(ForwardSpeed, speed);
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
            
            var nav = GetComponent<NavMeshAgent>();
            nav.enabled = false; // NavMeshAgent의 transform 간섭 차단 방지
            
            transform.position = data.position.ToVector();
            transform.eulerAngles = data.rotation.ToVector();
            
            nav.enabled = true;
            return true;
        }

        #endregion
        
    }
}
