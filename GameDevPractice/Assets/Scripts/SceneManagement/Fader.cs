using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.SceneManagement
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Fader : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private CancellationTokenSource fadeCTS;
        
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
    
            // todo: 캔버스그룹 관련 설정 
            // canvasGroup.ignoreParentGroups = true;
            // canvasGroup.interactable = false;
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
        
        public UniTask FadeIn(float duration = 1f) => FadeAsync(0f, duration);
        public UniTask FadeOut(float duration = 1f) => FadeAsync(1f, duration);

        private async UniTask FadeAsync(float targetAlpha, float duration)
        {
            FadeCancel();
            fadeCTS = new CancellationTokenSource();
            var token = fadeCTS.Token;

            try
            {
                while (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
                {
                    token.ThrowIfCancellationRequested();

                    float delta = Time.deltaTime / duration;
                    if (targetAlpha > canvasGroup.alpha)
                        canvasGroup.alpha = Mathf.Min(canvasGroup.alpha + delta, targetAlpha);
                    else
                        canvasGroup.alpha = Mathf.Max(canvasGroup.alpha - delta, targetAlpha);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                canvasGroup.alpha = targetAlpha; // 정확한 보정
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

