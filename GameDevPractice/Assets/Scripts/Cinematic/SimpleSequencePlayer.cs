using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace TH.Cinematic
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleSequencePlayer : MonoBehaviour
    {
        [SerializeField] private bool playOnce;
        private bool alreadyPlayed = false;

        private void Awake()
        {
            var coll = GetComponent<Collider>();
            coll.isTrigger = true;

            var rigid = GetComponent<Rigidbody>();
            rigid.isKinematic = true;
            rigid.useGravity = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (playOnce && alreadyPlayed) return;
                alreadyPlayed = true;
                GetComponent<PlayableDirector>()?.Play();
            }
        }

    }
}
