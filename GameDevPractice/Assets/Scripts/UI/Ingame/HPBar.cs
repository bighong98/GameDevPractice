using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using RPG.Attribute;

// HP Bar Controller using UI Component Image, Slider
namespace RPG.UI
{
    public class HPBar : BaseUI, IPoolObject
    {
        private Transform target;
        private RectTransform rect;
        
        private Slider main;
        private Slider sub;

        private bool easing;
        
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
                Util.Log($"[{nameof(HPBar)}] failed to initialize");
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
                SlowlyChangeFill(sub, from, to).Forget(); // sub 천천히 변경
            }
            else // case 체력이 늘어남
            {
                SlowlyChangeFill(main, from, to, 0.2f).Forget(); // main 천천히 변경
            }
            
        }

        
        private async UniTask SlowlyChangeFill(Slider slider, float from, float to, float speed = 0.1f)
        {
            if (easing)
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }
            ClarifyToken();
            
            float curr = from;
            easing = true;
            while (!token.IsCancellationRequested && !Mathf.Approximately(curr, to))
            {
                try
                {
                    await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, token).SuppressCancellationThrow();
                    curr = Mathf.MoveTowards(curr, to, speed * Time.deltaTime);
                    slider.value = curr;
                }
                catch (Exception e)
                {
                    Util.LogError($"[{nameof(HPBar)}] error occurred while {nameof(SlowlyChangeFill)}(). {e}");
                }
            }
            
            slider.value = to;
            easing = false;
        }

        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        private CancellationToken token;
        private void ClarifyToken()
        {
            if (!token.CanBeCanceled || token.IsCancellationRequested)
            {
                tokenSource = new CancellationTokenSource();
                token = tokenSource.Token;
            }
        }
        
        public void SetOwner(Health owner)
        {
            owner.OnHealthRatioChanged += this.OnHealthRatioChanged;
            owner.OnMaxHealthChanged += this.OnMaxHealthChanged;
            target = owner.transform;
        }

        private void OnHealthRatioChanged(float ratio)
        {
            Util.Log($"{target.gameObject.name}: health ratio is changed. {ratio}");
            SetFill(ratio);
        }

        private void OnMaxHealthChanged(float amount)
        {
            Util.Log($"{target.gameObject.name}: max health is changed. {amount}");
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
            
            Util.Log($"not in screen. Hide HPBar");
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
            if (!tokenSource.IsCancellationRequested)
                tokenSource.Cancel();
        }

        public void OnDestroyFromPool()
        {
            if (!tokenSource.IsCancellationRequested)
            {
                tokenSource.Cancel();
            }
            tokenSource.Dispose();
        }

        public void ReleaseSelf()
        {
            if (gameObject.activeSelf)
            {
                PoolingManager.Instance.ReleaseFromPool(this);
            }
        }
    }
}

