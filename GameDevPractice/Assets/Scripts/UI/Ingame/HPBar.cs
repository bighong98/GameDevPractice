using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using RPG.Attribute;
using TH.Core.Pool;

// HP Bar Controller using UI Component Image, Slider
namespace RPG.UI
{
    public class HPBar : BaseUI, IPoolObject
    {
        private Transform target; // HPBar가 추적하는 대상
        private RectTransform rect; // 자기 자신의 RectTransform
        
        private Slider main; // 실제 체력바
        private Slider sub; // 체력이 줄어들었을 때 효과 처리용 바

        private bool easing; // 천천히 움직이는 bar 애니메이션 실행중인지 여부
        private bool isHiding; // 현재 시각적으로 비활성화중인지 여부
        
        private const float FillingUpSpeed = 1.0f; // 체력이 회복되었을 때 체력바 움직임 애니메이션 속도
        private const float FallingDownSpeed = 0.5f; // 체력이 떨어졌을 때 체력바 움직임 애니메이션 속도
        private const float DelayHideByDeath = 1.0f; // Bar의 주인이 사망처리시 비활성화까지의 지연시간 (사망 후 {DelayHideByDeath}초 뒤 사라짐) // 현재 사용x
        
        #region Enums

        enum GameObjects
        {
            Displayer,
            Sub,
            Main,
        }

        #endregion

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            BindObject(typeof(GameObjects));
            main = GetObject((int)GameObjects.Main).GetComponent<Slider>();
            sub = GetObject((int)GameObjects.Sub).GetComponent<Slider>();

            if (main == null || sub == null)
            {
                Util.Log($"[{nameof(HPBar)}] failed to initialize", Util.LoggingMode.Completed);
                ReleaseSelf();
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (Util.IsInsideScreen(target.position, out var result))
            {
                rect.position = result;
                Show();
            }
            else
            {
                Util.Log($"not in screen. Hide HPBar", Util.LoggingMode.Completed);
                Hide();
            }
        }

        private void SetFill(float ratio)
        {
            var to = Mathf.Clamp01(ratio);
            var from = Mathf.Clamp01(main.value);

            if (to < from) // case : 체력이 줄어듦
            {
                main.value = to; // main 즉시 변경
                ChangeFillSlowly(sub, from, to, FallingDownSpeed).Forget(); // sub 천천히 변경
            }
            else // case 체력이 늘어남
            {
                ChangeFillSlowly(main, from, to, FillingUpSpeed).Forget(); // main 천천히 변경
            }
        }

        private void ChangeFillImmediately(float ratio)
        {
            if (easing) StopBarAnimation(); // 기존 바 애니메이션 중지
            
            var value = Mathf.Clamp01(ratio);
            main.value = value;
            sub.value = value;
        }
        
        private async UniTask ChangeFillSlowly(Slider slider, float from, float to, float speed)
        {
            if (easing) StopBarAnimation(); // 기존 바 애니메이션 중지
            ClarifyToken();
            
            float curr = from;
            easing = true;
            while (!barAnimToken.IsCancellationRequested && !Mathf.Approximately(curr, to))
            {
                try
                {
                    await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, barAnimToken).SuppressCancellationThrow();
                    curr = Mathf.MoveTowards(curr, to, speed * Time.deltaTime);
                    slider.value = curr;
                }
                catch (Exception e)
                {
                    Util.LogError($"[{nameof(HPBar)}] error occurred while {nameof(ChangeFillSlowly)}(). {e}");
                }
            }
            
            slider.value = to;
            easing = false;
        }

        private CancellationTokenSource barAnimCTS = new CancellationTokenSource();
        private CancellationToken barAnimToken;

        private void StopBarAnimation()
        {
            barAnimCTS.Cancel();
            barAnimCTS.Dispose();

            easing = false;
        }
        private void ClarifyToken()
        {
            if (!barAnimToken.CanBeCanceled || barAnimToken.IsCancellationRequested)
            {
                barAnimCTS = new CancellationTokenSource();
                barAnimToken = barAnimCTS.Token;
            }
        }

        private readonly TimeSpan deathDelaySpan = TimeSpan.FromSeconds(DelayHideByDeath);
        private async UniTaskVoid HideAfterSecond(float duration)
        {
            try
            {
                await UniTask
                    .Delay(deathDelaySpan, DelayType.Realtime, PlayerLoopTiming.PreLateUpdate,
                        this.destroyCancellationToken).SuppressCancellationThrow();
                Hide();
            }
            catch (Exception e)
            {
                Util.LogError($"[{nameof(HPBar)}] unexpected error occurred while {nameof(HideAfterSecond)}. {e}");
            }
        }
        
        public void SetOwner(Health owner)
        {
            owner.OnHealthRatioChanged += this.OnHealthRatioChanged;
            owner.OnMaxHealthChanged += this.OnMaxHealthChanged;
            owner.OnDead += this.OnOwnerDied;
            target = owner.transform;
        }

        private void OnHealthRatioChanged(float ratio)
        {
            // Util.Log($"{target.gameObject.name}: health ratio is changed. {ratio}");
            SetFill(ratio);
        }

        private void OnMaxHealthChanged(float amount)
        {
            Util.Log($"{target.gameObject.name}: max health is changed. {amount}");
        }

        private void OnOwnerDied()
        {
            if (gameObject is not { activeSelf: true }) return;
            HideAfterSecond(DelayHideByDeath).Forget();
        }

        private void Show()
        {
            if (GetObject((int)GameObjects.Displayer) is not { activeSelf: false } displayer) 
                return;
            
            displayer.SetActive(true);
        }

        private void Hide()
        {
            if (GetObject((int)GameObjects.Displayer) is not { activeSelf: true } displayer) 
                return;
            
            displayer.SetActive(false);
        }
        
        public GameObject Origin { get; set; }

        public void OnCreateFromPool()
        {
            
        }

        public void OnGetFromPool()
        {
            
        }

        public void OnReleaseFromPool()
        {
            if (!barAnimCTS.IsCancellationRequested)
                barAnimCTS.Cancel();
        }

        public void OnDestroyFromPool()
        {
            if (!barAnimCTS.IsCancellationRequested)
            {
                barAnimCTS.Cancel();
            }
            barAnimCTS.Dispose();
        }

        public void ReleaseSelf()
        {
            if (gameObject.activeSelf)
            {
                // PoolingManager.Instance.ReleaseFromPool(this);
                PoolManager.Instance.ReleaseFromPool(this);
            }
        }
    }
}

