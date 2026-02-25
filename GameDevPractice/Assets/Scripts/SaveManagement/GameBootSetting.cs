using UnityEngine;

namespace TH.Core.Data
{
#if UNITY_EDITOR
    [CreateAssetMenu(fileName = "GameBootSetting", menuName = "Scriptable Objects/SaveLoad/GameBootSetting")]
#endif
    public class GameBootSetting : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField] private bool loadMainMenuInEditor;
        [SerializeField] private bool ignoreBootStrapperInEditor;
        [SerializeField] private bool disableSaveLoadInEditor;

        public bool LoadMainMenuInEditor => loadMainMenuInEditor;
        public bool IgnoreBootStrapperInEditor => ignoreBootStrapperInEditor;
        public bool DisableSaveLoadInEditor => ignoreBootStrapperInEditor || disableSaveLoadInEditor;
#else
        public bool LoadMainMenuInEditor => false;
        public bool IgnoreBootStrapperInEditor => false;
        public bool DisableSaveLoadInEditor => false;
#endif
    }
}

