using TH.Core.Service;

namespace TH.UI
{
    public class MainMenuUI : BaseUI
    {
        #region Enum

        enum Buttons
        {
            playButton,
            quitButton,
        }

        #endregion

        protected override void Awake()
        {
            base.Awake();
            
            BindButton(typeof(Buttons));
            BindAsyncEvent(GetButton((int)Buttons.playButton).gameObject,
                async () => { await GameSceneManager.Instance.LoadSceneAsync(Enums.Scene.SelectScene); });

            BindAsyncEvent(GetButton((int)Buttons.quitButton).gameObject,
                async () => { await GameSceneManager.Instance.QuitGame(); });
        }
    }
}
