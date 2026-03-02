using TH.Core.Service;
using System;
using System.Threading;

namespace TH.UI
{
    public sealed class LoadSlotConfirmModule
    {
        private const string DefaultLoadConfirmQuestion = "Load the selected save file?";

        private readonly string loadConfirmQuestion;
        private CancellationTokenSource requestCTS;
        private CancellationTokenSource linkedRequestCTS;

        public LoadSlotConfirmModule(string loadConfirmQuestion = DefaultLoadConfirmQuestion)
        {
            this.loadConfirmQuestion = string.IsNullOrWhiteSpace(loadConfirmQuestion)
                ? DefaultLoadConfirmQuestion
                : loadConfirmQuestion;
        }

        private static void CancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts == null)
                return;

            if (!cts.IsCancellationRequested)
                cts.Cancel();

            cts.Dispose();
            cts = null;
        }

        private void RenewRequestCTS()
        {
            CancelAndDispose(ref linkedRequestCTS);
            CancelAndDispose(ref requestCTS);
            requestCTS = new CancellationTokenSource();
        }

        private CancellationToken GetEffectiveOwnerToken(CancellationToken ownerToken)
        {
            if (!ownerToken.CanBeCanceled)
                return requestCTS.Token;

            linkedRequestCTS = CancellationTokenSource.CreateLinkedTokenSource(requestCTS.Token, ownerToken);
            return linkedRequestCTS.Token;
        }

        public void CancelActiveRequest()
        {
            CancelAndDispose(ref linkedRequestCTS);
            CancelAndDispose(ref requestCTS);
        }

        public void Request(string saveFile, Action<string> onConfirmed, CancellationToken ownerToken = default)
        {
            if (string.IsNullOrWhiteSpace(saveFile) || onConfirmed == null)
                return;

            if (UIManager.Instance.ShowPopupUI<QuestionPopupUI>() is not { } popup)
                return;

            RenewRequestCTS();
            popup.ChainPopupCTS(GetEffectiveOwnerToken(ownerToken));

            popup.SetQuestion(
                questionString: loadConfirmQuestion,
                YesAction: () =>
                {
                    popup.ClosePopupUI();
                    onConfirmed.Invoke(saveFile);
                },
                NoAction: () =>
                {
                    popup.ClosePopupUI();
                });
        }
    }
}
