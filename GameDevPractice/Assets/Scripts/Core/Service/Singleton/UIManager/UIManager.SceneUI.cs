using Cysharp.Threading.Tasks;
using TH.Core.Pool;
using TH.Resource;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TH.Core.Service
{
    public partial class UIManager
    {
        private SceneUI sceneUI;

        #region Scene UI Method

        // 현재 씬에 맞는 SceneUI를 비동기로 로드하고 설정
        // 동일한 SceneUI가 이미 있으면 갱신만 수행, 다르면 교체
        // Singleton.InitAfterPreLoad()에서 실행
        private async UniTask SetSceneUIAsync(Scene scene)
        {
            if (sceneCatalogSO == null || sceneUIListSO == null)
            {
                Logg.LogError($"[UIManager] sceneCatalogSO: {sceneCatalogSO}, sceneUIListSO: {sceneUIListSO}");
                return;
            }

            if (!sceneCatalogSO.TryGetSceneEntry(scene, out var currentSceneEntry))
            {
                Logg.LogWarning($"[UIManager] currentSceneEntry is null from (scene: {scene.name})");
                return;
            }

            var targetSceneUIRef = sceneUIListSO.GetSceneUIByScene(currentSceneEntry?.sceneRef);
            if (targetSceneUIRef == null)
            {
                Logg.LogWarning("[UIManager] targetSceneUIRef is null");
                return;
            }
            // 씬 UI 프리펩 로드
            var loadedSceneUI = await resourceLoader.LoadAsync<GameObject>(targetSceneUIRef);
            if (loadedSceneUI == null)
            {
                Logg.LogWarning("[UIManager] loadedSceneUIis null");
                return;
            }

            if (sceneUI != null)
            {
                // 현재 SceneUI와 동일한 경우 변경 없이 갱신만 요청, 종료
                if (loadedSceneUI == sceneUI.Origin)
                {
                    sceneUI.RefreshUI();
                    return;
                }
                // 기존 SceneUI가 있으면 풀에 반환
                else if (uiPools.TryGetValue(sceneUI.GetType(), out var previousPool))
                    previousPool.Release(sceneUI);
                else
                    PoolManager.Instance.ReleaseFromPool(sceneUI);
            }

            // 새로운 씬UI 프리펩 컴포넌트(SceneUI 타입) 체크
            string key = $"{loadedSceneUI.name}.prefab";
            if (!loadedSceneUI.TryGetComponent(out SceneUI sceneUIPrefab))
            {
                Logg.LogWarning("[UIManager] SceneUI component not found on prefab");
                return;
            }
            // 새로운 SceneUI를 풀에서 가져오기 (없으면 오브젝트 풀 생성)
            var sceneUIType = sceneUIPrefab.GetType();
            if (!TryGetOrCreateUIPool(sceneUIType, key, UICanvas.Scene, loadedSceneUI, out var pool))
                return;

            sceneUI = pool.Get() as SceneUI;
            if (sceneUI == null)
            {
                Logg.LogWarning("[UIManager] failed to get SceneUI instance from pool");
                return;
            }

            sceneUI.RefreshUI();
        }

        public bool TryGetSceneUI(out SceneUI ui)
        {
            if (!sceneUI.IsNotNull())
            {
                ui = default;
                return false;
            }

            ui = sceneUI;
            return true;
        }

        #endregion
    }
}
