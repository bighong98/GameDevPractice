using TH.UI;
using TH.Core.Pool;
using UnityEngine;

namespace TH.UI
{
    public abstract class SceneUI : BaseUI, IPoolObject
    {
        public virtual void RefreshUI() // call by OnCreateFromPool(), OnGetFromPool()
        {
            
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


