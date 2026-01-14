using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.SaveLoad;
using TH.Resource;

using UnityEngine;
using TH.Utils;

namespace TH.UI
{
    [RequireComponent(typeof(MainMenuUI))]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private MainMenuUI view;
        [SerializeField] private AudioClip testBgm;

        // 외부 서비스 (ServiceLocator로 주입)
        private ISaveSystem saveSystem;
        private ISaveFileHandler saveFileHandler;
        
        // SceneCatalog 비동기 로드 관리
        private const string SceneCatalogKey = "SceneCatalogSO";
        private readonly UniTaskCompletionSource<SceneCatalogSO> catalogResolveTCS = new();
        private UniTask<SceneCatalogSO> catalogResolved;
        private SceneCatalogSO sceneCatalog;

        private void Awake()
        {
            saveSystem = ServiceLocator.Get<ISaveSystem>();
            saveFileHandler = ServiceLocator.Get<ISaveFileHandler>();

            if (view == null)
                TryGetComponent(out view);
        }

        private void Start()
        {
            catalogResolved = catalogResolveTCS.Task.Preserve();
            LoadSceneCatalogAsync().Forget();

            if (testBgm != null)
            {
                SoundManager.Instance.Play(Enums.AudioType.Bgm, testBgm);
            }
        }

        private void OnEnable()
        {
            if (view == null)
                return;

            view.NewGameRequested += HandleNewGameRequested;
            view.ContinueRequested += HandleContinueRequested;
            view.LoadRequested += HandleLoadRequested;
            view.OptionRequested += HandleOptionRequested;
            view.QuitRequested += HandleQuitRequested;
            view.LoadSlotSelected += HandleLoadSlotSelected;

            RefreshSaveState();
        }

        private void OnDisable()
        {
            if (view == null)
                return;

            view.NewGameRequested -= HandleNewGameRequested;
            view.ContinueRequested -= HandleContinueRequested;
            view.LoadRequested -= HandleLoadRequested;
            view.OptionRequested -= HandleOptionRequested;
            view.QuitRequested -= HandleQuitRequested;
            view.LoadSlotSelected -= HandleLoadSlotSelected;
        }

        #region Initialization

        private async UniTask LoadSceneCatalogAsync()
        {
            try
            {
                sceneCatalog = await ServiceLocator.Get<IResourceLoader>().LoadAsync<SceneCatalogSO>(SceneCatalogKey);
                catalogResolveTCS.TrySetResult(sceneCatalog);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MainMenuController] SceneCatalog 로드 실패: {e.Message}");
                catalogResolveTCS.TrySetException(e);
            }
        }
        
        private async UniTask WaitForCatalog()
        {
            if (sceneCatalog != null) return;
            await catalogResolved;
        }

        #endregion

        private void RefreshSaveState()
        {
            if (saveFileHandler == null || view == null)
                return;

            saveFileHandler.RefreshSaveFileList();
            bool hasSave = saveFileHandler.SaveFiles.Count > 0;
            view.SetContinueButtonEnabled(hasSave);
            view.SetLoadButtonEnabled(hasSave);
        }

        #region Handle UI Events

        private void HandleNewGameRequested()
        {
            this.Log($"HandleNewGameRequested invoked", Logg.LoggingMode.InProgress);
            StartNewGameAsync().Forget();
        }

        private void HandleContinueRequested()
        {
            ContinueGameAsync().Forget();
        }

        private void HandleLoadRequested()
        {
            if (saveFileHandler == null || view == null)
                return;

            saveFileHandler.RefreshSaveFileList();
            var slots = BuildSlotViewData(saveFileHandler.SaveFiles);
            view.ShowLoadSlots(slots);
        }

        private void HandleLoadSlotSelected(string saveFile)
        {
            if (string.IsNullOrEmpty(saveFile))
                return;

            LoadFromSlotAsync(saveFile).Forget();
        }

        private void HandleOptionRequested()
        {
            UIManager.Instance.ShowOptionMenu();
        }

        private void HandleQuitRequested()
        {
            QuitGameAsync().Forget();
        }

        #endregion
        
        #region Core 

        private async UniTask StartNewGameAsync()
        {
            if (saveSystem == null || saveFileHandler == null)
                return;
            this.Log($"StartNewGameAsync invoked", Logg.LoggingMode.InProgress);
            saveFileHandler.RefreshSaveFileList();
            
            // SceneCatalog 로드 완료 대기
            await WaitForCatalog();
            if (sceneCatalog == null)
            {
                Debug.LogError("[MainMenuController] SceneCatalogSO 로드 실패");
                return;
            }
            
            var defaultSceneEntry = sceneCatalog.GetDefaultSceneEntry();
            string saveFile = saveFileHandler.CreateEmptySaveFile(defaultSceneEntry);
            await saveSystem.LoadLastScene(saveFile);
        }

        private async UniTask ContinueGameAsync()
        {
            if (saveSystem == null || saveFileHandler == null)
                return;

            saveFileHandler.RefreshSaveFileList();
            string saveFile = GetMostRecentSaveFileName();
            if (string.IsNullOrEmpty(saveFile))
            {
                await StartNewGameAsync();
                return;
            }

            await saveSystem.LoadLastScene(saveFile);
        }

        private async UniTask LoadFromSlotAsync(string saveFile)
        {
            if (saveSystem == null)
                return;

            await saveSystem.LoadLastScene(saveFile);
        }

        private async UniTask QuitGameAsync()
        {
            await GameSceneManager.Instance.QuitGame();
        }

        #endregion
        
        #region Helper Methods

        private string GetMostRecentSaveFileName()
        {
            var recent = saveFileHandler.GetMostRecentSaveFile();
            return recent.HasValue ? recent.Value.FileName : null;
        }

        private static List<MainMenuUI.SaveSlotViewData> BuildSlotViewData(IReadOnlyList<SaveFileInfo> saveFiles)
        {
            var slots = new List<MainMenuUI.SaveSlotViewData>(saveFiles.Count);
            foreach (var saveInfo in saveFiles)
            {
                string label = $"{saveInfo.FileName}  {saveInfo.SaveDate:yyyy/MM/dd HH:mm}";
                slots.Add(new MainMenuUI.SaveSlotViewData(saveInfo.FileName, label));
                Logg.Log($"[MainMenuController] BuildSlotViewData - slot({label})", Logg.LoggingMode.Completed);
            }
            return slots;
        }

        #endregion

        
    }
}
