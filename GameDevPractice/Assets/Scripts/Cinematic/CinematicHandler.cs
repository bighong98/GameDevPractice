using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace TH.Cinematic
{
    [RequireComponent(typeof(PlayableDirector))]
    public class CinematicHandler : MonoBehaviour
    {
        private GameObject player;
        private PlayableDirector pd;
        
        private void Awake()
        {
            player = GameObject.FindWithTag("Player");
            pd = GetComponent<PlayableDirector>();
        }

        private void OnEnable()
        {
            if (player == null) return;

            pd.played += OnPDStarted;
            pd.stopped += OnPDStopped;
        }

        private void OnDisable()
        {
            if (player == null) return;
            
            pd.played -= OnPDStarted;
            pd.stopped -= OnPDStopped;
        }

        private void OnPDStarted(PlayableDirector pd)
        {
            Debug.Log("OnPDStarted called");
            PausePlayer();
        }
        
        private void OnPDStopped(PlayableDirector pd)
        {
            Debug.Log("OnPDStopped called");
            ResumePlayer();
        }

        private void PausePlayer()
        {
            // player.GetComponent<TH.Core.CharacterActionScheduler>()?.CancelCurrentAction();
            player.GetComponent<TH.Control.PlayerController>().enabled = false;
        }

        private void ResumePlayer()
        {
            player.GetComponent<TH.Control.PlayerController>().enabled = true;
        }
    }
}
