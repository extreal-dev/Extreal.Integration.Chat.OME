using System.Diagnostics.CodeAnalysis;
using Extreal.Core.Common.System;
using Extreal.Integration.Chat.OME.MVS.App;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.NotificationControl
{
    public class NotificationControlPresenter : DisposableBase, IInitializable
    {
        private readonly AppState appState;
        private readonly NotificationControlView notificationControlView;

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public NotificationControlPresenter(
            AppState appState,
            NotificationControlView notificationControlView)
        {
            this.appState = appState;
            this.notificationControlView = notificationControlView;
        }

        public void Initialize()
        {
            appState.OnNotificationReceived
                .Subscribe(notificationControlView.Show)
                .AddTo(disposables);

            notificationControlView.OnBackButtonClicked
                .Subscribe(_ => notificationControlView.Hide())
                .AddTo(disposables);
        }

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
