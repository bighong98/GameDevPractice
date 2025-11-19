using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using UnityEngine;
using UnityEngine.SceneManagement;
using TH.Core;
using TH.Utils;

namespace TH.SceneManagement
{
    public class GameSceneManager : Singleton<GameSceneManager>
    {
        private Action<bool> initializationTasks;
        private readonly Queue<Func<UniTask>> cleanupTasks = new();
    
        private bool currentSceneLoaded;

        protected override void Awake()
        {
            base.Awake();
            if (IsInvalidInstance()) return;
            sceneLoader = ServiceLocator.Get<ISceneLoader>();
            
            SceneManager.sceneLoaded += ((scene, mode) =>
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

        // public async UniTask LoadSceneAsync(Enums.Scene scene, bool reload = false) // 비동기 씬 이동 (씬 이동 전 초기화 작업 수행)
        // {
        //     await LoadSceneAsync((int)scene, reload);
        // }

        // public async UniTask LoadSceneAsync(int sceneIndex, bool reload = false)
        // {
        //     if (!reload && SceneManager.GetActiveScene().buildIndex == sceneIndex)
        //     {
        //         return; // reload 목적이 아니라면, 동일 씬으로의 이동x
        //     }
        //
        //     await TaskBeforeLoadScene();
        //     await sceneLoader.LoadSceneAsync("");
        // }
        //
        // public async UniTask LoadSceneAsync(string key, bool reload = false)
        // {
        //     if (!reload && SceneManager.GetActiveScene().name == key)
        //     {
        //         return; // reload 목적이 아니라면, 동일 씬으로의 이동x
        //     }
        //
        //     await TaskBeforeLoadScene();
        //     await sceneLoader.LoadSceneAsync(key);
        // }
        
        public async UniTask LoadSceneAsync(object key, bool reload = false)
        {
            if (!reload && (key is string strKey && SceneManager.GetActiveScene().name == strKey)
                || (key is AssetReferenceScene sceneRef && SceneManager.GetActiveScene().name == sceneRef.SceneName))
            {
                return; // reload 목적이 아니라면, 동일 씬으로의 이동x
            }
        
            await TaskBeforeLoadScene();
            await sceneLoader.LoadSceneAsync(key);
        }

        #endregion

        // 씬 로딩(필수 리소스 + 씬 리소스 + 씬 로드 + 씬 활성화) 진행도 전달받기
        public IProgressSubscription GetSceneLoadProgress(Action<float> onProgress)
        {
            return sceneLoader.SubscribeProgress(onProgress);
        }
        
        private async UniTask TaskBeforeLoadScene()
        {
            await CleanupAllAsync();
        }
        
        public void RegisterInitializationTask(Action<bool> task)
        {
            if (currentSceneLoaded)
            {
                Logg.Log($"[{nameof(GameSceneManager)}] RegisterInitializationTask: trying to run task", Logg.LoggingMode.Completed);
                task?.Invoke(true);
            }
            else initializationTasks += task;
        }

        public void UnRegisterInitializationTask(Action<bool> task)
        {
            initializationTasks -= task;
        }
        
        public void RegisterCleanupTask(Func<UniTask> cleanupTask)
        {
            cleanupTasks.Enqueue(async() =>
            {
                // if (Util.IsQuitting) return;
                await cleanupTask();
            });
        }

        #region End

        public async UniTask QuitGame()
        {
            // await GameManager.Instance.EndApplication();
            await CleanupAllAsync();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_IOS
    return;
#else
    Application.Quit();
#endif
        }
        
        protected override UniTask Clear()
        {
            base.Clear();

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

