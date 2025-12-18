using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.SceneManagement;
using UnityEngine;

public class CoreDuplicationHandler : MonoBehaviour
{
    [SerializeField] private List<GameObject> targets;
    private CancellationToken token;
    
    private void Awake()
    {
        if (ServiceLocator.Get<ISceneLoader>() is { } sceneLoader)
        {
            sceneLoader.OnBeforeSceneChanged += DisablePossiblyDuplicate;
        }

        token = destroyCancellationToken;
    }

    private UniTask DisablePossiblyDuplicate(CancellationToken externalToken)
    {
        if (token.IsCancellationRequested || externalToken.IsCancellationRequested
            || targets == null || targets.Count == 0) return  UniTask.CompletedTask;

        foreach (var target in targets)
        {
            if (!target.IsAlive() || !target.activeSelf) continue;
            target.SetActive(false);
        }
        
        return UniTask.CompletedTask;
    }
}
