using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
// 추후 싱글톤을 경유해서 접근할 필요가 있는 오브젝트 관리 목적의 매니저
// 현재는 사용X
public class ObjectManager : Singleton<ObjectManager>
{
    protected override void Awake()
    {
        base.Awake();
        if (IsInvalidInstance()) return;
        Init();
    }

    protected override void OnSceneLoaded(bool isDone)
    {
        if (!isDone) return;
        Init();
    }

    private void Init()
    {
        ResourceManager.Instance.SubscribePreLoad(InitAfterLoad);
        GameSceneManager.Instance.RegisterCleanupTask(async () =>
        {
            await Clear();  
        });
    }

    private void InitAfterLoad(bool isDone)
    {
        if (!isDone) return;
        //todo: 리소스 로드 후 처리할 작업 추가
    }

    private UniTask Clear()
    {
        //todo: 씬 로드 전 정리할 작업 추가
        return UniTask.CompletedTask;
    }
}
