using System;
using Cysharp.Threading.Tasks;
using TH.Core;
using TH.Core.Service;
using TH.SceneManagement;
using UnityEngine;
using TH.Resource;
using TH.Utils;
#if UNITY_EDITOR
using TH.Core.Data;
#endif

namespace TH.SaveLoad
{
    public class SavingWrapper : MonoBehaviour
    {
        private const string SceneCatalogKey = "SceneCatalogSO";
#if UNITY_EDITOR
        private const string GameBootSettingResourceKey = "GameBootSetting";
#endif

        private ISaveSystem saveSystem;
        private IResourceLoader resourceLoader;
        private ISceneLoader sceneLoader;
        private SceneCatalogSO sceneCatalog;
#if UNITY_EDITOR
        private GameBootSetting gameBootSetting;
#endif
        private bool disableSaveLoadInEditor;
        
        private void Awake()
        {
#if UNITY_EDITOR
            gameBootSetting = Resources.Load<GameBootSetting>(GameBootSettingResourceKey);

            if (gameBootSetting == null)
                Logg.LogWarning($"[SavingWrapper] Missing Resources/{GameBootSettingResourceKey}.asset. Editor boot setting will default to LoadLastScene.");

            disableSaveLoadInEditor = ShouldDisableSaveLoadInEditor();
            if (disableSaveLoadInEditor)
            {
                this.Log("[SavingWrapper] Save/Load initialization skipped by GameBootSetting.");
                return;
            }
#endif

            saveSystem = ServiceLocator.Get<ISaveSystem>();
            resourceLoader = ServiceLocator.Get<IResourceLoader>();
            sceneLoader = ServiceLocator.Get<ISceneLoader>();

            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, Init);
        }

        private void OnEnable()
        {
            if (disableSaveLoadInEditor) return;

            InputManager.Instance.OnSaveCalled += SaveCall;
            InputManager.Instance.OnLoadCalled += LoadCall;
        }
        
        private void OnDisable()
        {
            if (disableSaveLoadInEditor) return;

            InputManager.Instance.OnSaveCalled -= SaveCall;
            InputManager.Instance.OnLoadCalled -= LoadCall;
        }

        private void Init()
        {
            if (disableSaveLoadInEditor) return;
            Init(Constants.PreLoadLabel);
        }


        private void Init(string label)
        {
            if (disableSaveLoadInEditor) return;
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
            return gameBootSetting != null && gameBootSetting.LoadMainMenuInEditor;
#else
            return true;
#endif
        }

        private bool ShouldDisableSaveLoadInEditor()
        {
#if UNITY_EDITOR
            return gameBootSetting != null && gameBootSetting.DisableSaveLoadInEditor;
#else
            return false;
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
            if (disableSaveLoadInEditor)
            {
                this.Log("Save() skipped by GameBootSetting", Logg.LoggingMode.InProgress);
                return;
            }

            if (saveSystem == null)
            {
                Logg.LogWarning("[SavingWrapper] ISaveSystem is not available. Save() skipped.");
                return;
            }

            this.Log($"Save() invoked", Logg.LoggingMode.InProgress);
            await saveSystem.SaveAsync();
        }

        public async UniTask Load()
        {
            if (disableSaveLoadInEditor)
            {
                this.Log("Load() skipped by GameBootSetting", Logg.LoggingMode.InProgress);
                return;
            }

            if (saveSystem == null)
            {
                Logg.LogWarning("[SavingWrapper] ISaveSystem is not available. Load() skipped.");
                return;
            }

            this.Log($"Load() invoked", Logg.LoggingMode.InProgress);
            await saveSystem.LoadAsync();
        }

        // public async UniTask Delete()
        // {
        //     await saveSystem.DeleteAsync(Constants.DefaultSaveFile);
        // }
    }
}

