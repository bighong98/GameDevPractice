using System;
using UnityEngine;

public class MainMenuUI : BaseUI
{
    #region Enum

    enum Buttons
    {
        playButton,
        quitButton,
    }

    #endregion
    private void Awake()
    {
        BindButton(typeof(Buttons));
        BindAsyncEvent(GetButton((int)Buttons.playButton).gameObject, async () =>
        {
            await GameSceneManager.Instance.LoadSceneAsync(Enums.Scene.SelectScene);
        });
        
        BindAsyncEvent(GetButton((int)Buttons.quitButton).gameObject, async () =>
        {
            await GameSceneManager.Instance.QuitGame();
        });
    }
}
