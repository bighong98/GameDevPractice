using System;
using Cysharp.Threading.Tasks;
using TH.Core;
using TH.SceneManagement;
using TH.Core.Service;
using UnityEngine;
using TH.Resource;
using TH.UI;
using TH.Utils;

namespace TH.SaveLoad
{
    public class SavingWrapper : MonoBehaviour
    {
        private const string defaultSaveFile = "save";
        private ISaveSystem saveSystem;
        
        private void Awake()
        {
            saveSystem = ServiceLocator.Get<ISaveSystem>();
            
            if (ServiceLocator.Get<IResourceLoader>() is {} resourceLoader)
            {
                resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, Init);
            }
        }

        private void OnEnable()
        {
            InputManager.Instance.OnSaveCalled += SaveCall;
            InputManager.Instance.OnLoadCalled += LoadCall;
        }
        
        private void OnDisable()
        {
            InputManager.Instance.OnSaveCalled -= SaveCall;
            InputManager.Instance.OnLoadCalled -= LoadCall;
        }

        private void Init() => Init(Constants.PreLoadLabel);

        private void Init(string label)
        {
            if (label == Constants.PreLoadLabel)
            {
                Logg.Log($"[SavingWrapper] Init() invoked");
                LoadLastScene().Forget();
            }
        }
        
        private async UniTask LoadLastScene()
        {
            await UniTask.Yield(); // 1 프레임 지연 (Awake()에서 실행됨으로써 발생 가능한 fader 초기화 순서 오류 방지)
            await saveSystem.LoadLastScene(defaultSaveFile);
        }
        
        private void SaveCall() => Save().Forget();
        private void LoadCall() => Load().Forget();

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

