using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;
using TH.SceneManagement;

namespace TH.UI
{
    public class ScreenTransitionUI : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        
        private CancellationTokenSource fadeCTS;
        
        private const string UICanvasSettingSOKey =  "UICanvasSettingSO";
        private static readonly int AlphaHash = Shader.PropertyToID("_FSS_Alpha");
        private static readonly int ProgressHash = Shader.PropertyToID("_FSS_Progress");

        private void Awake()
        {
            if (canvas == null) TryGetComponent(out canvas);
            if (ServiceLocator.Get<ISceneLoader>() is { } sceneLoader)
            {
                sceneLoader.OnBeforeSceneChanged += FadeOut;
                sceneLoader.OnLastSceneChanged += FadeIn;
            }
        }

        public void SetActive(bool toggle)
        {
            gameObject.SetActive(toggle);
            Shader.SetGlobalFloat(AlphaHash, toggle ? 1f : 0f);
        }

        private void FadeCancel()
        {
            if (fadeCTS == null) return;
            
            fadeCTS.Cancel();
            fadeCTS.Dispose();
            fadeCTS = null;
        }
        
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        
        public UniTask FadeIn(CancellationToken externalToken)
        {
            Logg.Log($"[{GetType().Name}] FadeIn()", Logg.LoggingMode.Completed);
            return FadeIn(fadeInDuration, externalToken);
        }

        public UniTask FadeOut(CancellationToken externalToken)
        {
            Logg.Log($"[{GetType().Name}] FadeOut()", Logg.LoggingMode.Completed);
            return FadeOut(fadeOutDuration, externalToken);
        }

        public UniTask FadeIn(float duration, CancellationToken externalToken) 
            => FadeAsync(1f, 0f, duration)
                .AttachExternalCancellation(externalToken)
                .ContinueWith(() => { SetActive(false); });
        public UniTask FadeOut(float duration, CancellationToken externalToken) 
            => FadeAsync(0f, 1f, duration).AttachExternalCancellation(externalToken);
        
        private async UniTask FadeAsync(float start, float end, float duration)
        {
            FadeCancel();
            var token = RenewToken();
            SetActive(true);

            Shader.SetGlobalFloat(ProgressHash, start);
            
            float elapsed = 0f;

            try
            {
                while (elapsed < duration && !token.IsCancellationRequested)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    Shader.SetGlobalFloat(ProgressHash, Mathf.Lerp(start, end, t));
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    Shader.SetGlobalFloat(ProgressHash, end);; // 최종 값 보정
            }
        }


        private CancellationToken RenewToken()
        {
            fadeCTS = new CancellationTokenSource();
            this.GetCancellationTokenOnDestroy().Register(() => fadeCTS.Cancel());
            
            return fadeCTS.Token;
        }

        private void OnDestroy()
        {
            if (Util.IsQuitting) return;
            if (ServiceLocator.Get<ISceneLoader>() is { } sceneLoader)
            {
                sceneLoader.OnBeforeSceneChanged -= FadeOut;
                sceneLoader.OnAfterSceneChanged -= FadeIn;
            }
        }
    }
}

