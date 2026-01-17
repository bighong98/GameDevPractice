using TH.Utils;
using UnityEngine;

namespace TH.Core.Service
{
    public partial class UIManager
    {
        #region Input Event

        private void ConnectInputEvents()
        {
            InputManager.Instance.OnEscaped -= OnEscapeCalled;
            InputManager.Instance.OnEscaped += OnEscapeCalled;

            InputManager.Instance.OnInventoryCalled -= OnInventoryCalled; // 중복 구독 방지
            InputManager.Instance.OnInventoryCalled += OnInventoryCalled;

            InputManager.Instance.OnSingleClicked -= OnPopupOutSideSelected; // 중복 구독 방지
            InputManager.Instance.OnSingleClicked += OnPopupOutSideSelected;
        }

        private void DisConnectInputEvents()
        {
            InputManager.Instance.OnEscaped -= OnEscapeCalled;
            InputManager.Instance.OnSingleClicked -= OnPopupOutSideSelected;
        }

        private void OnEscapeCalled()
        {
            Logg.Log($"[UIManager]OnEscapeCalled. popupStack.Count: {popupStacks.Count}",
                Logg.LoggingMode.Completed);
            if (popupStacks.Count != 0)
            {
                ClosePopupUI();
                return;
            }

            ShowOptionMenu();
        }

        private void OnInventoryCalled()
        {
            ShowInventoryUI();
        }

        private void OnPopupOutSideSelected(Vector2 selectedPos)
        {
            if (IsBeforePopupThreshold()) return;

            while (popupStacks.TryPeek(out var peek) && !peek.gameObject.activeSelf)
            {
                popupStacks.Pop();
            }

            if (popupStacks.TryPeek(out var peekPopup) && peekPopup is { CloseOnOuterBackgroundClick: true })
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(peekPopup.ContentArea, selectedPos))
                {
                    Logg.Log("Outer background touched. close popup", Logg.LoggingMode.Completed);
                    ClosePopupUI(peekPopup, escapableCheck: true, ignoreOpenThreshold: false, waitForAnimation: true);
                }
            }
        }

        #endregion
    }
}
