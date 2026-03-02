using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Core.Pool;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.Pool;

namespace TH.Core.Service
{
    public partial class UIManager
    {
        /// <summary>팝업 연속 오픈 방지를 위한 최소 간격 (초)</summary>
        private const float PopupOpenThreshold = 0.05f;
        /// <summary>마지막 팝업 오픈 시간 (Time.unscaledTime)</summary>
        private float lastPopupOpenTime; 

        /// <summary>팝업 중복 오픈 체크용 딕셔너리</summary>
        private readonly Dictionary<Type, bool> popupDuplicateCheck = new();
        private bool questionPopupGlyphPrewarmed;
        
        #region Popup UI Method

        // 팝업 UI 생성/활성화 메서드
        // 오브젝트 풀링, 중복 팝업 처리 (Allow/Toggle/Replace), 팝업 스택 관리, 일시정지 처리 등 포함
        public T ShowPopupUI<T>(string uiName = null) where T : PopupUI
        {
            var type = typeof(T);

            // 중복 검사가 필요한 팝업인지 확인 및 스택에 동일 타입 팝업이 있는지 검사
            if (ShouldScanDuplicate(type)
                && popupStacks.Count > 0
                && IsPopupInStack<T>(out var inStackPopup))
            {
                // 중복 처리 정책에 따라 분기
                switch (inStackPopup.DuplicatedPopupHandling)
                {
                    case PopupUI.DuplicatedPopupHandle.Replace: // 기존 닫고 새로 열기
                        Logg.Log($"[{GetType().Name}.{nameof(ShowPopupUI)}()] Replace mode: close existing popup and create new one",
                                Logg.LoggingMode.Completed);
                        ClosePopupUI(inStackPopup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true);
                        break;
                    case PopupUI.DuplicatedPopupHandle.Toggle: // 기존 닫기만 수행
                        if (ClosePopupUI(inStackPopup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true))
                        {
                            Logg.Log($"[{GetType().Name}.{nameof(ShowPopupUI)}()] Toggle mode: close existing popup without creating new one",
                                    Logg.LoggingMode.Completed);
                            return null;
                        }
                        break;
                    case PopupUI.DuplicatedPopupHandle.Allow: // 중복 허용
                    default:
                        break;
                }
            }

            // 팝업 인스턴스 가져오기 (풀에서 또는 새로 생성)
            var popup = GetPopupInstance<T>(type, uiName);
            if (popup == null) return null;

            // 팝업 스택에 추가
            popupStacks.Push(popup);

            // 일시정지 필요 시 게임 일시정지
            if (popup.PauseRequired)
                InputManager.Instance.PauseGame();

            // 팝업 열림 시간 기록 (빠른 닫기 방지용)
            lastPopupOpenTime = Time.unscaledTime;

            // UI 액션맵 활성화 (ESC 등의 입력 받기)
            InputManager.Instance.EnableUIActionMap();
            InputManager.Instance.DisableCamActionMap();

            Logg.Log($"[{GetType().Name}.{nameof(ShowPopupUI)}()] new Popup. name: {popup.name} popupStack.Count: {popupStacks.Count}",
                    Logg.LoggingMode.Completed);
            return popup;
        }

        // 공용 UI 호출 메서드(ShowUI)로 팝업 호출 시 사용하는 우회용 메서드 (ShowUI -> ShowPopupUI)
        // 가능하면 팝업은 ShowPopupUI<T>(string) 직접 호출 사용할 것  
        private PopupUI ShowPopupUIByType(Type type, string uiName = null)
        {
            if (type == null || !typeof(PopupUI).IsAssignableFrom(type))
                return null;

            var method = GetType().GetMethod(
                nameof(ShowPopupUI),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null
            );
            if (method == null || !method.IsGenericMethodDefinition)
                return null;

            var generic = method.MakeGenericMethod(type);
            return generic.Invoke(this, new object[] { uiName }) as PopupUI;
        }

        // 특정 팝업 UI 닫기
        // escapableCheck: Escapable 속성 확인 여부
        // ignoreOpenThreshold: 열림 throttling 무시 여부
        // waitForAnimation: 닫힘 애니메이션 대기 여부
        public bool ClosePopupUI(PopupUI popup, bool escapableCheck = true, bool ignoreOpenThreshold = true, bool waitForAnimation = true)
        {
            if (popup == null || popupStacks.Count == 0)
                return false;

            // Escapable 체크: 닫기 불가능한 팝업이면 취소
            if (escapableCheck && !popup.Escapable)
                return false;

            // 빠른 닫기 방지: 열린지 0.05초 이내면 취소
            if (!ignoreOpenThreshold && IsBeforePopupThreshold())
                return false;

            // 스택에서 팝업 제거
            if (!popupStacks.Remove(popup))
            {
                Logg.Log($"[{GetType().Name}.{nameof(ClosePopupUI)}()]: popup not found in stack : {popup.name}", Logg.LoggingMode.Completed);
                return false;
            }

            // 팝업 풀에서 닫기 처리
            if (TryGetUIPool(popup, out var popupPool))
            {
                ClosePopupInternal(popup, popupPool, waitForAnimation);
            }

            // 모든 팝업이 닫혔으면 UI 액션맵 비활성화
            if (popupStacks.Count == 0)
            {
                InputManager.Instance.DisableUIActionMap();
                InputManager.Instance.EnableCamActionMap();
            }

            return true;
        }

        public bool ClosePopupUI(bool loopEnabled = true, bool escapableCheck = true, bool ignoreOpenThreshold = true, bool waitForAnimation = true)
        {
            // Top에서부터 유효한 팝업을 찾을 때까지 반복
            while (popupStacks.TryPeek(out var top))
            {
                // 비활성화된 팝업이면 스택에서 제거만 하고 다음으로 넘어감
                if (!top.gameObject.activeSelf)
                {
                    popupStacks.Pop();
                    if (!loopEnabled)
                        return false;

                    continue;
                }

                // 실제 닫기 로직은 인스턴스 오버로드에 위임
                return ClosePopupUI(top, escapableCheck, ignoreOpenThreshold, waitForAnimation);
            }

            return false;
        }

        // 팝업 닫기 내부 처리 (애니메이션 처리 후 풀 반환)
        private void ClosePopupInternal(PopupUI popup, ObjectPool<IPoolObject> popupPool, bool waitForAnimation)
        {
            if (waitForAnimation)
            {
                // 비동기로 종료 애니메이션 재생 후 정리
                popup.OnPopupClosedAsync().ContinueWith(() =>
                {
                    try { HandleTimePauseAndReleasePopup(popup, popupPool); }
                    catch (Exception e) { Logg.LogError($"[{GetType().Name}] Error during popup closing: {e}"); }
                }).Forget();
            }
            else
            {
                // 즉시 정리 및 풀 반환
                popup.OnPopupClosed();
                HandleTimePauseAndReleasePopup(popup, popupPool);
            }
        }

        // 팝업 인스턴스 가져오기 (오브젝트 풀 사용)
        // 풀이 없으면 새로 생성
        // 중복 정책 캐싱
        private T GetPopupInstance<T>(Type type, string uiName) where T : PopupUI
        {
            string key = uiName ?? $"{type.Name}.prefab";
            if (!TryGetOrCreateUIPool(key, UICanvas.Popup, out var pool))
                return null;

            // 풀에서 인스턴스 가져오기
            var popup = pool.Get() as T;
            if (popup == null) return null;

            // 중복 타입 UI 정책 캐싱 (최초 1회)
            CacheDuplicatePolicy(type, popup);
            return popup;
        }

        // 현재 활성화된 모든 팝업UI 비활성화 (최상단부터 순서대로)
        // escapableCheck 옵션을 무시함 -> 추후 escapable: false 인 팝업은 남겨두고 싶다면 메서드 추가 필요
        public void CloseAllPopupUI()
        {
            while (popupStacks.TryPeek(out var popup))
            {
                ClosePopupUI(popup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true);
            }
        }

        // 일시정지 상태 확인 및 팝업을 풀에 반환
        private void HandleTimePauseAndReleasePopup(PopupUI popup, ObjectPool<IPoolObject> popupPool)
        {
            // 일시정지가 필요한 팝업이 없다면 게임 일시정지 해제
            if (!IsPausedRequired())
            {
                // MonoInputManager.Instance.ResumeGame();
                InputManager.Instance.ResumeGame();
            }

            // 팝업을 풀에 반환 및 Canvas sorting order 감소
            popupPool.Release(popup);
            sortOrders[(int)UICanvas.Popup]--;
        }

        public void ClosePopupUIImmediately<T>(T popup) where T : PopupUI
        {
            Type type = popup.GetType();
            if (TryGetUIPool(popup, out var pool))
            {
                pool.Release(popup);
            }
        }

        public int GetPopupCount() => popupStacks.Count;

        #endregion

        #region PopupUI Helper Method

        // 현재 활성화된 팝업 중 일시정지가 필요한 팝업이 있는지 확인
        private bool IsPausedRequired()
        {
            if (popupStacks.Count == 0) return false;

            // 팝업 중 하나라도 일시정지를 요구하면 true 반환
            foreach (var popup in popupStacks)
            {
                if (popup.PauseRequired) return true;
            }

            return false;
        }

        // 팝업의 중복 처리 정책을 캐싱하여 반복 검사 방지
        private void CacheDuplicatePolicy(Type type, PopupUI popup)
        {
            // Allow가 아니면 중복 검사 필요
            bool needCheck = popup.DuplicatedPopupHandling != PopupUI.DuplicatedPopupHandle.Allow;
            popupDuplicateCheck[type] = needCheck;
        }

        // 해당 타입의 팝업이 중복 검사가 필요한지 확인
        private bool ShouldScanDuplicate(Type type)
        {
            // 이미 한 번 이상 생성해서 정책을 캐싱해둔 경우
            if (popupDuplicateCheck.TryGetValue(type, out var needScan))
            {
                return needScan; // Toggle/Replace -> true, Allow -> false
            }

            // 처음 보는 타입이면 최초 한 번은 검사 (타입 캐싱 위해)
            return true;
        }

        private bool IsPopupInStack<T>(out T inStackPopup) where T : PopupUI
        {
            foreach (var popup in popupStacks)
            {
                if (popup is not T p) continue;

                inStackPopup = p;
                return true;
            }

            inStackPopup = default;
            return false;
        }

        // 팝업이 열린 지 일정 시간(0.05초) 이내인지 확인 (의도치 않은 팝업 동시다발적 비활성화 방지)
        private bool IsBeforePopupThreshold()
        {
            bool rValue = Time.unscaledTime - lastPopupOpenTime < PopupOpenThreshold;
            if (rValue) Logg.Log($"{nameof(IsBeforePopupThreshold)}: ClosePopupUI Guarded");

            return rValue;
        }

        #endregion

        #region Frequently Used UI

        private const string OptionMenuUIKey = "OptionMenuUI";
        private const string InventoryUIKey = "InventoryUI.prefab";
        private const string QuestionPopupUIKey = "QuestionPopupUI.prefab";
        private const string ToastMessageUIKey = "";

        private const string QuestionPopupWarmCharacters = "\uC885\uB8CC \uC804 \uAC8C\uC784\uC744 \uC800\uC7A5\uD558\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?\uBA54\uC778 \uD654\uBA74\uC73C\uB85C \uC774\uB3D9\uD558\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?\uC608\uC544\uB2C8\uC624";

        private void PrepareFrequentlyUsedUIs()
        {
            PrepareFrequentlyUsedUI<OptionMenuUI>(OptionMenuUIKey, UICanvas.Popup);
            PrepareFrequentlyUsedUI<InventoryUI>(InventoryUIKey, UICanvas.Popup);
            PrepareFrequentlyUsedUI<QuestionPopupUI>(QuestionPopupUIKey, UICanvas.Popup);
            PrepareFrequentlyUsedUI<ToastMessageUI>(ToastMessageUIKey, UICanvas.FeedbackOverlay);
        }
        private void PrewarmQuestionPopupUI()
        {
            if (questionPopupGlyphPrewarmed)
                return;

            if (!TryGetOrCreateUIPool(QuestionPopupUIKey, UICanvas.Popup, out var pool))
                return;

            var ui = pool.Get();
            if (ui is QuestionPopupUI questionPopup)
            {
                questionPopup.PrewarmGlyphs(QuestionPopupWarmCharacters);
                questionPopupGlyphPrewarmed = true;
            }

            if (ui is Component comp)
                comp.gameObject.SetActive(false);

            pool.Release(ui);
        }
        private void PrewarmInventoryUI()
        {
            if (!TryGetOrCreateUIPool(InventoryUIKey, UICanvas.Popup, out var pool))
                return;

            var ui = pool.Get();
            if (ui is Component comp)
                comp.gameObject.SetActive(false);

            pool.Release(ui);
        }

        private void PrepareFrequentlyUsedUI<T>(string key, UICanvas canvasType) where T : BaseUI, IPoolObject
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            TryGetOrCreateUIPool(key, canvasType, out _);
        }

        public void ShowOptionMenu()
        {
            ShowPopupUI<OptionMenuUI>(OptionMenuUIKey);
        }

        private void ShowInventoryUI()
        {
            Logg.Log($"[UIManager] Inventory open frame: {Time.frameCount}", Logg.LoggingMode.Completed);
            ShowPopupUI<InventoryUI>(InventoryUIKey);
        }

        public void ShowToastMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (string.IsNullOrWhiteSpace(ToastMessageUIKey))
                return;

            var toast = ShowUI<ToastMessageUI>(ToastMessageUIKey, UICanvas.FeedbackOverlay);
            if (toast == null)
                return;

            toast.Show(message);
        }

        #endregion
    }
}
