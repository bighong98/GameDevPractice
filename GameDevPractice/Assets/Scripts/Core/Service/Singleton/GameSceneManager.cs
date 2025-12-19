using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.SceneManagement;
using TH.Utils;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace TH.Core.Service
{
    [Preserve]
    public class GameSceneManager : Singleton<GameSceneManager>, ISingleton
    {
        private readonly Queue<Func<CancellationToken, UniTask>> beforeSceneLoadTasks = new();
        private readonly Queue<Func<CancellationToken, UniTask>> afterSceneLoadTasks = new();
        
        private readonly ISceneLoader sceneLoader;
        
        private GameSceneManager()
        {
            sceneLoader = ServiceLocator.Get<ISceneLoader>();
        }
        
        public async UniTask LoadSceneAsync(object key, bool reload = false)
        {
            if (!reload && key is string strKey && SceneManager.GetActiveScene().name == strKey
                || (key is AssetReferenceScene sceneRef && SceneManager.GetActiveScene().name == sceneRef.SceneName))
            {
                return; // reload 목적이 아니라면, 동일 씬으로의 이동x
            }
            await sceneLoader.LoadSceneAsync(key);
        }

        public void RegisterBeforeSceneLoadTask(Func<CancellationToken, UniTask> beforeSceneLoadTask)
        {
            beforeSceneLoadTasks.Enqueue(beforeSceneLoadTask);
        }

        public void RegisterAfterSceneLoadTask(Func<CancellationToken, UniTask> afterSceneLoadTask)
        {
            afterSceneLoadTasks.Enqueue(afterSceneLoadTask);
        }
        
        #region ISingleton

        public async UniTask BeforeSceneLoad(CancellationToken externalToken)
        {
            while (afterSceneLoadTasks.Count < 0)
            {
                if (!externalToken.IsCancellationRequested) return;
                if (afterSceneLoadTasks.Dequeue() is not { } task) continue;
                // 이미 await에 들어간 task는 externalToken에 의해 중단되지 않음에 주의할 것
                try { await task(externalToken); }
                catch (Exception e) { Logg.LogError($"Exception occured while BeforeSceneLoad. {e}"); }
            }
        }

        public async UniTask AfterSceneLoad(CancellationToken externalToken)
        {
            while (beforeSceneLoadTasks.Count < 0)
            {
                if (!externalToken.IsCancellationRequested) return;
                if (beforeSceneLoadTasks.Dequeue() is not { } task) continue;
                // 이미 await에 들어간 task는 externalToken에 의해 자동으로 중단되지 않음에 주의할 것
                // -> 개별 task에 토큰 기반 중단 로직 필수
                try { await task(externalToken).AttachExternalCancellation(externalToken); }
                catch (Exception e) { Logg.LogError($"Exception occured while AfterSceneLoad. {e}"); }
            }
        }

        #endregion
        
        public async UniTask QuitGame()
        {
            await BeforeSceneLoad(CancellationToken.None);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_IOS
    return;
#else
    Application.Quit();
#endif
        }
    }
}

