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
        [SerializeField] private float maxUnscaledStep = 1f / 30f;
        [SerializeField] private int minFadeFrames = 10;
        [SerializeField] private float fadeInEndHoldDuration = 0.08f;

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

        public async UniTask FadeIn(float duration, CancellationToken externalToken)
        {
            await FadeAsync(1f, 0f, duration).AttachExternalCancellation(externalToken);

            float holdElapsed = 0f;
            float cappedStep = maxUnscaledStep > 0f ? maxUnscaledStep : 1f / 30f;
            while (holdElapsed < fadeInEndHoldDuration && !externalToken.IsCancellationRequested)
            {
                holdElapsed += Mathf.Min(Time.unscaledDeltaTime, cappedStep);
                await UniTask.Yield(PlayerLoopTiming.Update, externalToken);
            }

            SetActive(false);
        }

        public UniTask FadeOut(float duration, CancellationToken externalToken)
            => FadeAsync(0f, 1f, duration).AttachExternalCancellation(externalToken);

        private async UniTask FadeAsync(float start, float end, float duration)
        {
            FadeCancel();
            var token = RenewToken();
            SetActive(true);

            Shader.SetGlobalFloat(ProgressHash, start);

            float safeDuration = Mathf.Max(duration, 0.0001f);
            float cappedStep = maxUnscaledStep > 0f ? maxUnscaledStep : 1f / 30f;
            int requiredFrames = Mathf.Max(1, minFadeFrames);
            int frameCount = 0;
            float elapsed = 0f;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, cappedStep);
                    frameCount++;

                    float timeT = Mathf.Clamp01(elapsed / safeDuration);
                    float frameT = Mathf.Clamp01((float)frameCount / requiredFrames);
                    float t = Mathf.Min(timeT, frameT);

                    Shader.SetGlobalFloat(ProgressHash, Mathf.Lerp(start, end, t));

                    if (timeT >= 1f && frameCount >= requiredFrames)
                        break;

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    Shader.SetGlobalFloat(ProgressHash, end);
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
                sceneLoader.OnLastSceneChanged -= FadeIn;
            }
        }
    }
}

