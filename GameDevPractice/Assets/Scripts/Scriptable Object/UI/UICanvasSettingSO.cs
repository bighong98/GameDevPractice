using System;
using System.Collections.Generic;
using TH.Utils;
using UnityEngine;

namespace TH.UI
{
    [CreateAssetMenu(fileName = "UICanvasSettingSO", menuName = "Scriptable Objects/UI/UICanvasSettingSO")]
    public class UICanvasSettingSO : ScriptableObject
    #if UNITY_EDITOR
    , ISerializationCallbackReceiver
    #endif
    {
        [Header("General")] 
        [SerializeField] private SerializableVector2 referenceResolution;

        public Vector2 ReferenceResolution => referenceResolution.ToVector();
        
        [SerializeField] private List<SerializablePair<RPG.UI.UICanvas, CanvasSetting>> list;
        private IReadOnlyCollection<SerializablePair<RPG.UI.UICanvas, CanvasSetting>> capturedList;
        public IReadOnlyCollection<SerializablePair<RPG.UI.UICanvas, CanvasSetting>> UISettings { get {
            if (capturedList == null || capturedList.Count == 0)
                capturedList = list.AsReadOnly();
            return capturedList; }
        }

        private readonly Dictionary<RPG.UI.UICanvas, CanvasSetting> canvasSettingDict = new();

        public CanvasSetting GetCanvasSetting(RPG.UI.UICanvas canvasType)
        {
            if (canvasSettingDict.Count == 0) ForceInitDict();
            return canvasSettingDict.GetValueOrDefault(canvasType);
        }
        
        public void ForceInitDict()
        {
            foreach (var (canvasType, settingSO) in list)
            {
                canvasSettingDict[canvasType] = settingSO;
            }
        }

        [Serializable]
        public class CanvasSetting
        {
            public bool overrideSorting;
            public int defaultSortingOrder;
        }

        #region ISerializationCallbackReceiver

#if UNITY_EDITOR
        public void OnBeforeSerialize()
        {
            SerializablePair<RPG.UI.UICanvas, CanvasSetting>.ValidateUnitySerializable();
        }
        
        public void OnAfterDeserialize() {}
#endif
        
        #endregion
    }
}

