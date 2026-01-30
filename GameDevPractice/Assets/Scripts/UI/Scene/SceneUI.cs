using TH.UI;
using TH.Core.Pool;
using UnityEngine;

namespace TH.UI
{
    public abstract class SceneUI : BaseUI, IPoolObject
    {
        public override bool Init()
        {
            if (base.Init() == false) return false;

            // 캔버스/캔버스그룹 확보 //todo: UIManager.cs에서 초기화 고려(UIManager.Instance.SetCanvas)
            canvas = gameObject.GetOrAddComponent<Canvas>();
            canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();

            return true;
        }
        
        public virtual void RefreshUI() // call by OnCreateFromPool(), OnGetFromPool()
        {
            
        }

        public virtual bool GetQuickSlotPanelUI(out QuickSlotPanelUI quickSlotPanelUI)
        {
            quickSlotPanelUI = default;
            return false;
        }
        
        #region IPoolObject

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

        #endregion
        
    }
}


