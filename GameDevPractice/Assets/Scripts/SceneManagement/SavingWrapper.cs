using Cysharp.Threading.Tasks;
using RPG.SceneManagement;
using TH.Core.Service;
using UnityEngine;
using TH.Resource;
using TH.Utils;

namespace TH.SaveLoad
{
    public class SavingWrapper : MonoBehaviour
    {
        private const string defaultSaveFile = "save";
        private ISaveSystem saveSystem;

        [SerializeField] private float fadeInTime = 0.2f;
        private void Awake()
        {
            // saveSystem = GetComponent<SaveSystem>();
            saveSystem = ServiceLocator.Get<ISaveSystem>();
            if (ServiceLocator.Get<IResourceLoader>() is {} resourceLoader)
            {
                if (resourceLoader.IsPreLoadDone())
                    Init(Constants.PreLoadLabel);
                else
                    resourceLoader.NotifyResourceLoad += Init;
            }
        }

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
            
            Fader fader = FindFirstObjectByType<Fader>();
            fader.FadeOutImmediately();
            
            await saveSystem.LoadLastScene(defaultSaveFile);
            fader.FadeIn(fadeInTime).Forget();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                Load().Forget();
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                Save().Forget();
            }

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Delete().Forget();
            }
        }

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

