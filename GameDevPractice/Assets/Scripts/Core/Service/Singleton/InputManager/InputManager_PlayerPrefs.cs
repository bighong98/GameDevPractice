using UnityEngine;
using UnityEngine.InputSystem;

namespace TH.Core
{
    public sealed partial class InputManager
    {
        private const string RebindPrefsKey = "Global.InputManager.Rebinds";

        private void InitPlayerPrefsBindings()
        {
            LoadRebindsFromPlayerPrefs();
            OnRebindCompleted += HandleRebindCompletedForPrefs;
        }

        private void HandleRebindCompletedForPrefs(RebindResult result)
        {
            SaveRebindsToPlayerPrefs();
        }

        public void SaveRebindsToPlayerPrefs()
        {
            string json = UserInput.asset.SaveBindingOverridesAsJson();
            if (string.IsNullOrEmpty(json))
            {
                if (PlayerPrefs.HasKey(RebindPrefsKey))
                    PlayerPrefs.DeleteKey(RebindPrefsKey);
            }
            else
            {
                PlayerPrefs.SetString(RebindPrefsKey, json);
            }

            PlayerPrefs.Save();
        }

        public void LoadRebindsFromPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(RebindPrefsKey))
                return;

            string json = PlayerPrefs.GetString(RebindPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            UserInput.asset.LoadBindingOverridesFromJson(json);
        }

        public void ClearRebindsFromPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(RebindPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
