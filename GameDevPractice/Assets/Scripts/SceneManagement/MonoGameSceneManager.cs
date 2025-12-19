using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using TH.Core;
using TH.Utils;

namespace TH.SceneManagement
{
    public class MonoGameSceneManager : MonoSingleton<MonoGameSceneManager>
    {
        private Action<bool> initializationTasks;
        private readonly Queue<Func<UniTask>> cleanupTasks = new();
    
        private bool currentSceneLoaded;

        protected override void Awake()
        {
            base.Awake();
            if (IsInvalidInstance()) return;
            
            SceneManager.sceneLoaded += ((_, _) =>
            {
                initializationTasks?.SafeInvoke(true);
                currentSceneLoaded = true;
            });
        }

        protected override void Start()
        {
            base.Start();
            if (IsInvalidInstance()) return;
            if (currentSceneLoaded) return;
            
            initializationTasks?.SafeInvoke(true);
            currentSceneLoaded = true;
        }

        #region LoadScene
        
        public async UniTask LoadSceneAsync(object key, bool reload = false)
        {
            if (!reload && key is string strKey && SceneManager.GetActiveScene().name == strKey
                || (key is AssetReferenceScene sceneRef && SceneManager.GetActiveScene().name == sceneRef.SceneName))
            {
                return; // reload 목적이 아니라면, 동일 씬으로의 이동x
            }
        
            await TaskBeforeLoadScene();
            await sceneLoader.LoadSceneAsync(key);
        }

        #endregion
        
        private async UniTask TaskBeforeLoadScene()
        {
            await CleanupAllAsync();
        }

        #region End

        public async UniTask QuitGame()
        {
            await CleanupAllAsync();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_IOS
    return;
#else
    Application.Quit();
#endif
        }
        
        protected override UniTask Clear(CancellationToken externalToken)
        {
            base.Clear(externalToken);
            if (externalToken.IsCancellationRequested) return UniTask.CompletedTask;

            currentSceneLoaded = false;
            return UniTask.CompletedTask;
        }
        
        private async UniTask CleanupAllAsync()
        {
            while (cleanupTasks.Count < 0)
            {
                if (cleanupTasks.Dequeue() is not { } task) continue;
            
                try { await task(); }
                catch (Exception e) { Logg.LogError($"Exception occured while Clean-up task. {e}"); }
            }
        }

        #endregion
        
    }
}

