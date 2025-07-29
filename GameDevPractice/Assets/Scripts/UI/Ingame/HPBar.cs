using System;
using UnityEngine;
using UnityEngine.UI;
using RPG.Attribute;

namespace RPG.UI
{
    public class HPBar : BaseUI, IPoolObject
    {
        private Transform target;
        private RectTransform rect;
        private Image image;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            image = GetComponent<Image>();
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

        public void SetOwner(Health owner)
        {
            owner.OnHealthRatioChanged += this.OnHealthRatioChanged;
            owner.OnMaxHealthChanged += this.OnMaxHealthChanged;
            target = owner.transform;
        }

        private void OnHealthRatioChanged(float ratio)
        {
            Util.Log($"{target.gameObject.name}: health ratio is changed. {ratio}");
        }

        private void OnMaxHealthChanged(float amount)
        {
            Util.Log($"{target.gameObject.name}: max health is changed. {amount}");
        }

        private void Show()
        {
            if (image.isActiveAndEnabled) return;
            image.enabled = true;
        }

        private void Hide()
        {
            if (!image.isActiveAndEnabled) return;
            Util.Log($"not in screen. Hide HPBar");
            image.enabled = false;
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
            
        }

        public void OnDestroyFromPool()
        {
            
        }

        public void ReleaseSelf()
        {
            
        }
    }
}

