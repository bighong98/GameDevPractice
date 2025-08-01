using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class GameSceneManager : Singleton<GameSceneManager>
{
    // public Action<bool> notifySceneLoaded;
    private Action<bool> initializationTasks;
    private readonly Queue<Func<UniTask>> cleanupTasks = new();
    
    private bool currentSceneLoaded;
    private int lastScene = -1;
    protected override void Awake()
    {
        base.Awake();
        if (IsInvalidInstance()) return;
        
        SceneManager.sceneLoaded += ((scene, mode) =>
        {
            // notifySceneLoaded?.SafeInvoke(true);
            initializationTasks?.SafeInvoke(true);
            currentSceneLoaded = true;
        });
    }

    #region Initialization

    protected override void InitOnce() { }

    protected override void InitOnceAfterPreLoad(bool isLoadCompleted) { }

    protected override void Init() { }

    protected override void InitAfterPreLoad(bool isLoadCompleted) { }

    protected override UniTask Clear()
    {
        return base.Clear();
    }
    
    #endregion

    #region Load Scene
    
    private async UniTask TaskBeforeLoadSceneAsync()
    {
        lastScene = SceneManager.GetActiveScene().buildIndex;
        currentSceneLoaded = false; // 플래그 초기화
        await CleanupAllAsync();
    }

    public async UniTask LoadSceneAsync(Enums.Scene scene, bool reload = false) // 비동기 씬 이동 (씬 이동 전 초기화 작업 수행)
    {
        await TaskBeforeLoadSceneAsync();
        await SceneManager.LoadSceneAsync(scene.ToString()).ToUniTask();
    }
    public async UniTask LoadSceneAsync(string sceneName)
    {
        await TaskBeforeLoadSceneAsync();
        await SceneManager.LoadSceneAsync(sceneName).ToUniTask();
    }

    public async UniTask LoadSceneAsync(int sceneIndex)
    {
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
        if (currentSceneLoaded) task?.Invoke(true);
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
            await cleanupTask();
        });
    }

    private async UniTask CleanupAllAsync()
    {
        while (cleanupTasks.Count < 0)
        {
            if (cleanupTasks.Dequeue() is not { } task) continue;
            
            try { await task(); }
            catch (Exception e) { Util.LogError($"Exception occured while Clean-up task. {e}"); }
        }
    }
}
