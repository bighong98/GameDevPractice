using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class GameSceneManager : Singleton<GameSceneManager>
{
    public Action<bool> notifySceneLoaded;
    private readonly List<Func<UniTask>> cleanupTasks = new List<Func<UniTask>>();
    
    protected override void Awake()
    {
        base.Awake();
        SceneManager.sceneLoaded += ((scene, mode) =>
        {
            notifySceneLoaded?.SafeInvoke(true);
        });
    }
    
    public void LoadScene(Enums.Scene scene) // 즉시 씬 이동 (사용 비권장)
    {
        SceneManager.LoadScene(scene.ToString());
    }

    public async UniTask LoadSceneAsync(Enums.Scene scene) // 비동기 씬 이동 (씬 이동 전 초기화 작업 수행)
    {
        //todo: 씬 타입별로 필요한 작업 처리 로직 추가
        await CleanupAllAsync();
        await SceneManager.LoadSceneAsync(scene.ToString()).ToUniTask();
    }
    public async UniTask LoadSceneAsync(string sceneName)
    {
        await CleanupAllAsync();
        await SceneManager.LoadSceneAsync(sceneName);
    }

    public async UniTask LoadSceneAsync(int sceneIndex)
    {
        await CleanupAllAsync();
        await SceneManager.LoadSceneAsync(sceneIndex);
    }

    public async UniTask QuitGame()
    {
        // await GameManager.Instance.EndApplication();
        await CleanupAllAsync();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_IOS
    return;
#else
    Application.Quit(); // ← 수정된 부분
#endif
    }

    public void RegisterCleanupTask(Func<UniTask> cleanupTask)
    {
        cleanupTasks?.Add(cleanupTask);
    }

    private async UniTask CleanupAllAsync()
    {
        foreach (var task in cleanupTasks)
        {
            if (task == null) continue;    
            await task();
        }
        cleanupTasks.Clear(); // Clean-up Task 종료 후 리스트 비우기
    }

    protected override void OnSceneLoaded(bool isDone)
    {
        
    }
}
