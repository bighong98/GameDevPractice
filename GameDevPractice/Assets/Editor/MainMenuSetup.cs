#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class MainMenuSetup
{
    private const string SetupKey = "MainMenuSetup_Done_v1";
    
    static MainMenuSetup()
    {
        if (EditorPrefs.GetBool(SetupKey, false))
            return;
            
        EditorApplication.delayCall += SetupMainMenu;
    }
    
    private static void SetupMainMenu()
    {
        EditorPrefs.SetBool(SetupKey, true);
        
        var panel = GameObject.Find("Canvas/MainMenuPanel");
        if (panel == null)
        {
            Debug.LogWarning("[MainMenuSetup] MainMenuPanel not found");
            return;
        }
        
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 0);
        panelRect.pivot = new Vector2(0, 0);
        panelRect.anchoredPosition = new Vector2(120, 150);
        
        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.spacing = 15;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
        }
        
        string[] buttonNames = { "Btn_NewGame", "Btn_Continue", "Btn_Load", "Btn_Options", "Btn_Quit" };
        string[] buttonTexts = { "새로하기", "이어하기", "불러오기", "옵션", "게임 종료" };
        
        for (int i = 0; i < buttonNames.Length; i++)
        {
            var btnTransform = panel.transform.Find(buttonNames[i]);
            if (btnTransform == null) continue;
            
            var btnGO = btnTransform.gameObject;
            
            var btnRect = btnGO.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(250, 50);
            
            var layoutElement = btnGO.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredHeight = 50;
                layoutElement.preferredWidth = 250;
            }
            
            var image = btnGO.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0, 0, 0, 0);
            }
            
            var button = btnGO.GetComponent<Button>();
            if (button != null)
            {
                var colors = button.colors;
                colors.normalColor = new Color(1, 1, 1, 1);
                colors.highlightedColor = new Color(1, 0.8f, 0.2f, 1);
                colors.pressedColor = new Color(0.8f, 0.6f, 0.1f, 1);
                colors.selectedColor = new Color(1, 0.8f, 0.2f, 1);
                button.colors = colors;
            }
            
            var textTransform = btnTransform.Find("Text");
            if (textTransform != null)
            {
                var tmp = textTransform.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = buttonTexts[i];
                    tmp.fontSize = 32;
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.color = Color.white;
                    tmp.fontStyle = FontStyles.Bold;
                }
                
                var textRect = textTransform.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 0);
                textRect.offsetMax = new Vector2(-10, 0);
            }
        }
        
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        
        Debug.Log("[MainMenuSetup] Main menu UI setup completed!");
    }
    
    [MenuItem("Tools/Reset MainMenu Setup Flag")]
    public static void ResetSetupFlag()
    {
        EditorPrefs.DeleteKey(SetupKey);
        Debug.Log("[MainMenuSetup] Setup flag reset. Restart Unity to re-run setup.");
    }
}
#endif