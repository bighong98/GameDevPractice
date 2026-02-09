using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TH.Attribute;
using TH.Core.Pool;
using TH.Core.Service;
using TH.UI.Service;
using TH.Utils;

// HP Bar Controller using UI Component Image, Slider
namespace TH.UI
{
    public class HPBar : BaseUI, IPoolObject, ICullingTargetView, IHUDCullingBindable, ISharedCanvasUI
    {
        private static readonly Dictionary<Health, HPBar> ActiveByOwner = new();

        [SerializeField]private Transform target; // HPBar가 추적하는 대상 // serialize for debug
        private RectTransform rect; // 자기 자신의 RectTransform
        
        private Slider main; // 실제 체력바
        private Slider sub; // 체력이 줄어들었을 때 효과 처리용 바

        private bool easing; // 천천히 움직이는 bar 애니메이션 실행중인지 여부
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

        private CancellationToken destroyToken;
        private Health owner;
        private bool isReleasing;

        private HUDCullingSystem.CullingHandle cullingHandle;
        private Func<ICullingTargetView, HUDCullingSystem.CullingHandle> registerCulling;
        private Action<HUDCullingSystem.CullingHandle> unregisterCulling;
        
        protected override void Awake()
        {
            base.Awake();
            
            rect = GetComponent<RectTransform>();
            BindObject(typeof(GameObjects));
            main = GetObject((int)GameObjects.Main).GetComponent<Slider>();
            sub = GetObject((int)GameObjects.Sub).GetComponent<Slider>();

            if (main == null || sub == null)
            {
                Logg.LogError($"[{nameof(HPBar)}] failed to initialize");
                ReleaseSelf();
            }

            destroyToken = destroyCancellationToken;
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
            if (easing) StopBarAnimation();
            ClarifyToken();

            float curr = from;
            easing = true;
            while (!barAnimToken.IsCancellationRequested && !Mathf.Approximately(curr, to))
            {
                try
                {
                    var canceled = await UniTask.NextFrame(PlayerLoopTiming.LastUpdate, barAnimToken).SuppressCancellationThrow();
                    if (canceled) break; // Token에 의한 

                    curr = Mathf.MoveTowards(curr, to, speed * Time.deltaTime);
                    slider.value = curr;
                }
                catch (MissingReferenceException) { break; } // slider 참조를 잃어버려서 발생한 예외는 무시
                catch (Exception e)
                {
                    Logg.LogError($"[{nameof(HPBar)}] error occurred while {nameof(ChangeFillSlowly)}(). {e}");
                    break;
                }
            }

            if (!destroyToken.IsCancellationRequested && slider.IsNotNull())
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
                barAnimCTS = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
                barAnimToken = barAnimCTS.Token;
            }
        }

        private readonly TimeSpan deathDelaySpan = TimeSpan.FromSeconds(DelayHideByDeath);
        private async UniTaskVoid HideAfterSecond(float duration, bool keepHiding)
        {
            try
            {
                await UniTask
                    .Delay(deathDelaySpan, DelayType.Realtime, PlayerLoopTiming.PreLateUpdate,
                        destroyToken).SuppressCancellationThrow();
            }
            catch (Exception e) { Logg.LogError($"[{nameof(HPBar)}] unexpected error occurred while {nameof(HideAfterSecond)}. {e}"); }
            Hide(keepHiding);
        }
        
        public void SetOwner(Health owner)
        {
            AttachOwner(owner);
        }

        private void ResetOwner()
        {
            if (target == null)
            {
                AttachOwner(null);
                return;
            }

            if (target.GetComponent<Health>() is { } owner)
                AttachOwner(owner);
            else
                AttachOwner(null);
        }

        private void OnHealthRatioChanged(float ratio)
        {
            Logg.Log($"{target.gameObject.name}: health ratio is changed. {ratio}");
            SetFill(ratio);
        }

        private void OnMaxHealthChanged(float amount)
        {
            Logg.Log($"{target.gameObject.name}: HealthBar: max health is changed. {amount}", Logg.LoggingMode.Completed);
        }

        private void OnOwnerDied()
        {
            if (gameObject is not { activeSelf: true }) return;
            Logg.Log($"[{target.name}.{nameof(HPBar)}] {nameof(OnOwnerDied)}() invoked", Logg.LoggingMode.Completed);
            HideAfterSecond(DelayHideByDeath, true).Forget();
        }

        private void OnOwnerRevived()
        {
        }

        private void Show()
        {
            SetDisplayerActive(true);
        }

        private void Hide(bool keepHiding = false)
        {
            if (!this) return;
            if (isReleasing) return;
            ReleaseSelf();
        }

        public void ConfigureCulling(Func<ICullingTargetView, HUDCullingSystem.CullingHandle> register,
            Action<HUDCullingSystem.CullingHandle> unregister)
        {
            registerCulling = register;
            unregisterCulling = unregister;
        }

        private void TryRegisterCulling()
        {
            if (registerCulling == null || target == null || cullingHandle.IsValid) return;
            cullingHandle = registerCulling(this);
        }

        private void UnregisterCulling()
        {
            if (unregisterCulling == null || !cullingHandle.IsValid) return;
            isReleasing = true;
            unregisterCulling(cullingHandle);
            cullingHandle = default;
            isReleasing = false;
        }

        Transform ICullingTargetView.Target => target;

        void ICullingTargetView.SetVisible(bool visible)
        {
            if (visible) Show();
            else SetDisplayerActive(false);
        }

        void ICullingTargetView.SetScreenPosition(Vector2 screenPos)
        {
            if (rect == null) return;
            rect.position = screenPos;
        }
        
        public GameObject Origin { get; set; }

        public void OnCreateFromPool()
        {
            
        }

        public void OnGetFromPool()
        {
            SetFill(1f);
            if (owner == null)
            {
                ResetOwner();
                return;
            }
            TryRegisterCulling();
        }

        public void OnReleaseFromPool()
        {
            ClearPoolingState(disposeTokenSource: false);
        }

        public void OnDestroyFromPool()
        {
            ClearPoolingState(disposeTokenSource: true);
        }

        public void ReleaseSelf()
        {
            if (isReleasing) return;
            if (!gameObject.activeSelf) return;
            isReleasing = true;
            UIManager.Instance.ReleaseUI(this);
            isReleasing = false;
        }

        private void SetDisplayerActive(bool active)
        {
            var displayer = GetObject((int)GameObjects.Displayer);
            if (!displayer) return;
            if (displayer.activeSelf == active) return;

            displayer.SetActive(active);
        }

        public static HPBar Acquire(Health owner, GameObject prefab)
        {
            if (owner == null || prefab == null) return null;
            if (ActiveByOwner.TryGetValue(owner, out var existing) && existing != null)
                return existing;

            var ui = UIManager.Instance.GetUIFromPool<HPBar>(prefab, UICanvas.HUD);
            if (ui == null) return null;
            ui.SetOwner(owner);
            return ui;
        }

        public static void Release(Health owner)
        {
            if (owner == null) return;
            if (!ActiveByOwner.TryGetValue(owner, out var ui) || ui == null) return;

            UIManager.Instance.ReleaseUI(ui);
            ActiveByOwner.Remove(owner);
        }

        private void ClearOwnerMap()
        {
            DetachOwner();

            if (owner == null)
            {
                RemoveMapByValue();
                return;
            }

            ActiveByOwner.Remove(owner);
            owner = null;
            target = null;
        }

        private void DetachOwner()
        {
            if (!owner) return;
            owner.OnHealthRatioChanged -= this.OnHealthRatioChanged;
            owner.OnMaxHealthChanged -= this.OnMaxHealthChanged;
            owner.OnDead -= this.OnOwnerDied;
            owner.OnRevived -= this.OnOwnerRevived;
        }

        private void RemoveMapByValue()
        {
            Health keyToRemove = null;
            foreach (var kvp in ActiveByOwner)
            {
                if (ReferenceEquals(kvp.Value, this))
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }

            if (keyToRemove != null)
                ActiveByOwner.Remove(keyToRemove);
        }

        private void AttachOwner(Health newOwner)
        {
            if (ReferenceEquals(owner, newOwner))
            {
                if (owner != null)
                    target = owner.transform;
                TryRegisterCulling();
                return;
            }

            UnregisterCulling();
            DetachOwner();
            if (owner != null)
                ActiveByOwner.Remove(owner);

            owner = newOwner;
            if (owner == null)
            {
                target = null;
                return;
            }

            ActiveByOwner[owner] = this;
            owner.OnHealthRatioChanged += this.OnHealthRatioChanged;
            owner.OnMaxHealthChanged += this.OnMaxHealthChanged;
            owner.OnDead += this.OnOwnerDied;
            owner.OnRevived += this.OnOwnerRevived;
            target = owner.transform;
            TryRegisterCulling();
        }

        private void ClearPoolingState(bool disposeTokenSource)
        {
            UnregisterCulling();
            ClearOwnerMap();
            if (!barAnimCTS.IsCancellationRequested)
                barAnimCTS.Cancel();
            if (disposeTokenSource)
                barAnimCTS.Dispose();
        }
    }
}

