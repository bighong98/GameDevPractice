using UnityEngine;

namespace TH.UI.Data
{
    public interface IFloatingTextController
    {
        void Set(FloatingTextSO data, string text);
        void SetSetting(FloatingTextSO data);
        void SetText(string text);
    }
}

