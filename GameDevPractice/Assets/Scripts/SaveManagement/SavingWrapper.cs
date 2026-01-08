using System;
using Cysharp.Threading.Tasks;
using TH.Core;
using TH.Core.Service;
using TH.SceneManagement;
using UnityEngine;
using TH.Resource;
using TH.Utils;

namespace TH.SaveLoad
{
    public class SavingWrapper : MonoBehaviour
    {
        [SerializeField] private bool loadMainMenuInEditor;

        private const string SceneCatalogKey = "SceneCatalogSO";

        private ISaveSystem saveSystem;
        private IResourceLoader resourceLoader;
        private ISceneLoader sceneLoader;
        private SceneCatalogSO sceneCatalog;
        
        private void Awake()
        {
            saveSystem = ServiceLocator.Get<ISaveSystem>();
            resourceLoader = ServiceLocator.Get<IResourceLoader>();
            sceneLoader = ServiceLocator.Get<ISceneLoader>();

            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, Init);
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
            if (label != Constants.PreLoadLabel) return;
            
            this.Log($"Init() invoked");
            LoadStartupScene().Forget();
        }
        
        private async UniTask LoadStartupScene()
        {
            await UniTask.Yield(); // 1 프레임 지연 (Awake()에서 실행됨으로써 발생 가능한 fader 초기화 순서 오류 방지)
            if (ShouldLoadMainMenu())
            {
                await LoadMainMenuScene();
                return;
            }

            await saveSystem.LoadLastScene();
        }
        
        private bool ShouldLoadMainMenu()
        {
#if UNITY_EDITOR
            return loadMainMenuInEditor;
#else
            return true;
#endif
        }

        private async UniTask LoadMainMenuScene()
        {
            if (resourceLoader == null || sceneLoader == null)
            {
                Logg.LogWarning("[SavingWrapper] missing services. Falling back to LoadLastScene.");
                await saveSystem.LoadLastScene();
                return;
            }

            if (sceneCatalog == null)
                sceneCatalog = await resourceLoader.LoadAsync<SceneCatalogSO>(SceneCatalogKey);
            
            var mainMenuEntry = sceneCatalog.GetMainMenuSceneEntry();
            if (mainMenuEntry.sceneRef == null)
            {
                Logg.LogWarning("[SavingWrapper] main menu scene not configured. Falling back to LoadLastScene.");
                await saveSystem.LoadLastScene();
                return;
            }

            await sceneLoader.LoadSceneAsync(mainMenuEntry.sceneRef);
        }

        private void SaveCall() => Save().Forget();
        private void LoadCall() => Load().Forget();

        public async UniTask Save()
        {
            this.Log($"Save() invoked", Logg.LoggingMode.InProgress);
            await saveSystem.SaveAsync();
        }

        public async UniTask Load()
        {
            this.Log($"Load() invoked", Logg.LoggingMode.InProgress);
            await saveSystem.LoadAsync();
        }

        // public async UniTask Delete()
        // {
        //     await saveSystem.DeleteAsync(Constants.DefaultSaveFile);
        // }
    }
}

