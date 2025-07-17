using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using RPG.Saving;
using UnityEngine;

namespace RPG.SceneManagement
{
    [RequireComponent(typeof(RPG.Saving.SaveSystem))]
    public class SavingWrapper : MonoBehaviour
    {
        private const string defaultSaveFile = "save";
        private SaveSystem saveSystem;

        [SerializeField] private float fadeInTime = 0.2f;
        private void Awake()
        {
            saveSystem = GetComponent<SaveSystem>();
            LoadLastScene().Forget();
        }
        
        private async UniTask LoadLastScene()
        {
            Fader fader = FindFirstObjectByType<Fader>();
            fader.FadeOutImmediately();
            
            await saveSystem.LoadLastScene(defaultSaveFile);
            fader.FadeIn(fadeInTime).Forget();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                Load().Forget();
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                Save().Forget();
            }

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Delete().Forget();
            }
        }

        public async UniTask Save()
        {
            await saveSystem.SaveAsync(defaultSaveFile);
        }

        public async UniTask Load()
        {
            await saveSystem.LoadAsync(defaultSaveFile);
        }

        public async UniTask Delete()
        {
            await saveSystem.DeleteAsync(defaultSaveFile);
        }
    }
}

