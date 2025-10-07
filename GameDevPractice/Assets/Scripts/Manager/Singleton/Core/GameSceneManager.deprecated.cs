using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.Deprecated
{
    public class GameSceneManager : Singleton<GameSceneManager>
    {
        // public Action<bool> notifySceneLoaded;
        private Action<bool> initializationTasks;
        private readonly Queue<Func<UniTask>> cleanupTasks = new();
        
        private bool currentSceneLoaded;

        protected override void Awake()
        {
            base.Awake();
            if (IsInvalidInstance()) return;
            
            SceneManager.sceneLoaded += ((scene, mode) =>
            {
                // notifySceneLoaded?.SafeInvoke(true);
                Util.SetMainCameraForUtilClass();
                initializationTasks?.SafeInvoke(true);
                currentSceneLoaded = true;
            });
        }

        protected override void Start()
        {
            base.Start();
            if (IsInvalidInstance()) return;
            if (currentSceneLoaded) return;
            
            Util.SetMainCameraForUtilClass();
            initializationTasks?.SafeInvoke(true);
            currentSceneLoaded = true;
        }

        #region Initialization

        protected override void InitOnce() { }

        protected override void InitOnceAfterPreLoad(bool isLoadCompleted) { }

        protected override void Init() { }

        protected override void InitAfterPreLoad(bool isLoadCompleted) { }

        protected override UniTask Clear()
        {
            base.Clear();
            
            currentSceneLoaded = false; // 플래그 초기화
            return UniTask.CompletedTask;
        }
        
        #endregion

        #region Load Scene
        
        private async UniTask TaskBeforeLoadSceneAsync()
        {
            // currentSceneLoaded = false; // 플래그 초기화
            await CleanupAllAsync();
        }

        public async UniTask LoadSceneAsync(Enums.Scene scene, bool reload = false) // 비동기 씬 이동 (씬 이동 전 초기화 작업 수행)
        {
            await LoadSceneAsync((int)scene, reload);
        }

        public async UniTask LoadSceneAsync(int sceneIndex, bool reload = false)
        {
            if (!reload && SceneManager.GetActiveScene().buildIndex == sceneIndex)
            {
                return; // reload 목적이 아니라면, 동일 씬으로의 이동x
            }
            
            await TaskBeforeLoadSceneAsync();
            await SceneManager.LoadSceneAsync(sceneIndex).ToUniTask();
        }

        #endregion
        
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

        public void RegisterInitializationTask(Action<bool> task)
        {
            if (currentSceneLoaded)
            {
                // Util.Log($"[{nameof(GameSceneManager)}] RegisterInitializationTask: trying to run task", Util.LoggingMode.Completed);
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

        private async UniTask CleanupAllAsync()
        {
            while (cleanupTasks.Count < 0)
            {
                if (cleanupTasks.Dequeue() is not { } task) continue;
                
                try { await task(); }
                catch (Exception e) { Debug.LogError($"Exception occured while Clean-up task. {e}"); }
            }
        }
    }
}

