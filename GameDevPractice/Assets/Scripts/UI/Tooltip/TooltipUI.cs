using System;
using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using TH.Utils;

namespace RPG.UI
{
    public class TooltipUI : BaseUI
    {
        #region Enum

        enum GameObjects
        {
            background,
        }

        enum TMPTexts
        {
            text,
        }

        #endregion

        [SerializeField] private RectTransform canvasRect;
        public Canvas tooltipCanvas;
        private RectTransform tooltipRect;
        private RectTransform backgroundRect;

        private TextMeshProUGUI textMeshPro;

        // private InputActions controls;
        private Vector2 rectOffset;
        private const float DefaultDelayDuration = 2.0f;

        private void Awake()
        {
            if (Init() == false)
                return;
        }

        public override bool Init()
        {
            if (base.Init() == false) return false;

            BindObject(typeof(GameObjects));
            BindTMPText(typeof(TMPTexts));

            if (canvasRect == null)
            {
                var result = FindFirstObjectByType<Canvas>();
                if (result == null)
                {
                    Logg.Log($"[{typeof(TooltipUI)}] Failed to find canvas for overlay");
                    return false;
                }

                canvasRect = result.GetComponent<RectTransform>();
            }

            tooltipCanvas = GetComponent<Canvas>();
            tooltipRect = GetComponent<RectTransform>();
            backgroundRect = GetObject((int)GameObjects.background).GetComponent<RectTransform>();
            textMeshPro = GetTMPText((int)TMPTexts.text).GetComponent<TextMeshProUGUI>();
            rectOffset = textMeshPro.GetComponent<RectTransform>().localPosition.GetVectorTwo() * 2;

            Hide();

            return true;
        }

        private void Update()
        {
            HandleMouseFollow();
        }

        public void Show(string tooltipDesc, bool hideAfterDelay = false, float delayDuration = DefaultDelayDuration)
        {
            if (String.IsNullOrEmpty(tooltipDesc)) return;

            gameObject.SetActive(true);
            textMeshPro.SetText(tooltipDesc);
            textMeshPro.ForceMeshUpdate(); // 메쉬 업데이트 지연으로 인한 오류 방지

            Vector2 textSize = textMeshPro.GetRenderedValues(false) + rectOffset;
            backgroundRect.sizeDelta = textSize;
            HandleMouseFollow();

            if (hideAfterDelay)
                DelayHide(delayDuration).Forget();
        }

        public void Show(int tooltipErrorType, bool hideAfterDelay = false, float delayDuration = DefaultDelayDuration)
        {
            Show(GetTooltipTextByType(tooltipErrorType), hideAfterDelay, delayDuration);
        }

        private string GetTooltipTextByType(int tooltipErrorType)
        {
            return tooltipErrorType switch
            {
                (int)Enums.TooltipErrorType.Occupied => "Occupied",
                (int)Enums.TooltipErrorType.Limited => "No more duplicate building",
                (int)Enums.TooltipErrorType.OutOfRange => "Out of range",
                (int)Enums.TooltipErrorType.Insufficient => "Insufficient resource",
                _ => null
            };
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }


        private async UniTaskVoid DelayHide(float delayDuration)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delayDuration), DelayType.DeltaTime, PlayerLoopTiming.Update);
            Hide();
        }

        private void HandleMouseFollow()
        {
            // if (Util.IsQuitting) return;

            Vector2 pointerPos = InputManager.Instance.PointerPos;

// #if UNITY_EDITOR
//         pointerPos = Mouse.current.position.ReadValue();
// #else
//     // InputSystem 사용 시 마우스/터치 겸용
//     pointerPos = controls.GamePlay.Point.ReadValue<Vector2>();
// #endif

            Rect canvas = canvasRect.rect;
            Rect background = backgroundRect.rect;

            if (pointerPos.x + background.width > canvas.width)
            {
                pointerPos.x = canvas.width - background.width;
            }

            if (pointerPos.y + background.height > canvas.height)
            {
                pointerPos.y = canvas.height - background.height;
            }

            tooltipRect.anchoredPosition = pointerPos;
        }

    }
}