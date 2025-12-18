using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TH.SceneManagement
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Fader : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        
        private CancellationTokenSource fadeCTS;
        
        private const string UICanvasSettingSOKey =  "UICanvasSettingSO";
        
        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
            if (canvas == null)
                canvas = gameObject.GetOrAddComponent<Canvas>();

            SceneManager.sceneLoaded += (_, _) => gameObject.SetActive(false);
            if (ServiceLocator.Get<ISceneLoader>() is { } sceneLoader)
            {
                sceneLoader.OnBeforeSceneChanged += FadeOut;
                sceneLoader.OnAfterSceneChanged += FadeIn;
            }
        }

        public void FadeOutImmediately()
        {
            FadeCancel();
            canvasGroup.alpha = 1f;
        }

        public void FadeInImmediately()
        {
            FadeCancel();
            canvasGroup.alpha = 0f;
        }

        private void FadeCancel()
        {
            if (fadeCTS == null) return;
            
            fadeCTS.Cancel();
            fadeCTS.Dispose();
            fadeCTS = null;
        }
        
        private const float FadeInDuration = 0.5f;
        private const float FadeOutDuration = 0.5f;
        
        public UniTask FadeIn(CancellationToken externalToken)
        {
            Logg.Log($"[{GetType().Name}] FadeIn()", Logg.LoggingMode.Completed);
            return FadeIn(FadeInDuration, externalToken);
        }

        public UniTask FadeOut(CancellationToken externalToken)
        {
            Logg.Log($"[{GetType().Name}] FadeOut()", Logg.LoggingMode.Completed);
            return FadeOut(FadeOutDuration, externalToken);
        }

        public UniTask FadeIn(float duration, CancellationToken externalToken) => FadeAsync(0f, duration).AttachExternalCancellation(externalToken);
        public UniTask FadeOut(float duration, CancellationToken externalToken) => FadeAsync(1f, duration).AttachExternalCancellation(externalToken);

        private async UniTask FadeAsync(float targetAlpha, float duration)
        {
            FadeCancel();
            var token = RenewToken();
            gameObject.SetActive(true);
            try
            {
                while (!Mathf.Approximately(canvasGroup.alpha, targetAlpha)
                       && !token.IsCancellationRequested)
                {
                    float delta = Time.unscaledDeltaTime / duration;
                    if (targetAlpha > canvasGroup.alpha)
                        canvasGroup.alpha = Mathf.Min(canvasGroup.alpha + delta, targetAlpha);
                    else
                        canvasGroup.alpha = Mathf.Max(canvasGroup.alpha - delta, targetAlpha);
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    canvasGroup.alpha = targetAlpha; // 정확한 보정
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

        #region Deprecated

        // public async UniTask FadeOut(float duration = 1f)
        // {
        //     FadeCancel();
        //     fadeCTS = new CancellationTokenSource();
        //     var token = fadeCTS.Token;
        //
        //     try
        //     {
        //         while (canvasGroup.alpha < 1f)
        //         {
        //             token.ThrowIfCancellationRequested();
        //             
        //             canvasGroup.alpha += Time.deltaTime / duration;
        //             await UniTask.Yield(PlayerLoopTiming.Update, token);
        //         }
        //     }
        //     finally
        //     {
        //         canvasGroup.alpha = 1f;
        //     }
        // }
        //
        // public async UniTask FadeIn(float duration = 1f)
        // {
        //     FadeCancel();
        //     fadeCTS = new CancellationTokenSource();
        //     var token = fadeCTS.Token;
        //     
        //     try
        //     {
        //         while (canvasGroup.alpha > 0)
        //         {
        //             token.ThrowIfCancellationRequested();
        //             
        //             canvasGroup.alpha -= Time.deltaTime / duration;
        //             await UniTask.Yield(PlayerLoopTiming.Update, token);
        //         }
        //     }
        //     finally
        //     {
        //         canvasGroup.alpha = 0;
        //     }
        // }

        #endregion
        
    }
}

