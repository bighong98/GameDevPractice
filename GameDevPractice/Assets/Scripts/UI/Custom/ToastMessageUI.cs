using DG.Tweening;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Utils;
using TMPro;
using UnityEngine;

namespace TH.UI
{
    public sealed class ToastMessageUI : BaseUI, IPoolObject
    {
        [Header("References")]
        [SerializeField] private TMP_Text messageText;

        [Header("Timing")]
        [SerializeField] private float showDuration = 1.5f;
        [SerializeField] private float fadeDuration = 0.5f;

        private Sequence fadeSequence;

        public GameObject Origin { get; set; }

        protected override void Awake()
        {
            base.Awake();
            CacheComponents();
        }

        public void Show(string message)
        {
            if (messageText == null)
                return;

            messageText.text = message;
            StartFadeSequence();
        }

        public void OnCreateFromPool()
        {
            CacheComponents();
        }

        public void OnGetFromPool()
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;

            KillFadeSequence();
        }

        public void OnReleaseFromPool()
        {
            KillFadeSequence();
        }

        public void OnDestroyFromPool()
        {
            KillFadeSequence();
        }

        public void ReleaseSelf()
        {
            if (Util.IsQuitting || !gameObject.activeSelf)
                return;

            UIManager.Instance.ReleaseUI(this);
        }

        private void CacheComponents()
        {
            if (messageText == null)
                messageText = GetComponentInChildren<TMP_Text>(true);

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void StartFadeSequence()
        {
            if (canvasGroup == null)
                return;

            KillFadeSequence();
            canvasGroup.alpha = 1f;

            fadeSequence = DOTween.Sequence()
                .AppendInterval(showDuration)
                .Append(canvasGroup.DOFade(0f, fadeDuration))
                .SetUpdate(true)
                .OnComplete(ReleaseSelf);
        }

        private void KillFadeSequence()
        {
            if (fadeSequence == null)
                return;

            if (fadeSequence.IsActive())
                fadeSequence.Kill(false);

            fadeSequence = null;
        }
    }
}
