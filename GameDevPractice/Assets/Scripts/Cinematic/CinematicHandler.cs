using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace RPG.Cinematic
{
    public class CinematicHandler : MonoBehaviour
    {
        private GameObject player;

        private void Start()
        {
            player = GameObject.FindWithTag("Player");
            var pd = GetComponent<PlayableDirector>();
            if (pd == null) return;

            pd.played += OnPDStarted;
            pd.stopped += OnPDStopped;
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
            player.GetComponent<RPG.Core.ActoinScheduler>()?.CancelCurrentAction();
            player.GetComponent<RPG.Control.PlayerController>().enabled = false;
        }

        private void ResumePlayer()
        {
            player.GetComponent<RPG.Control.PlayerController>().enabled = true;
        }
    }
}
