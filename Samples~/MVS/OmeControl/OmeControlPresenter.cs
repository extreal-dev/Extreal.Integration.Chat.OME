using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Chat.OME.MVS.App;
using Extreal.Integration.SFU.OME;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.OmeControl
{
    public class OmeControlPresenter : DisposableBase, IInitializable
    {
        private readonly StageNavigator<StageName, SceneName> stageNavigator;
        private readonly AppState appState;
        private readonly OmeClient omeClient;
        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public OmeControlPresenter(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            OmeClient omeClient)
        {
            this.stageNavigator = stageNavigator;
            this.appState = appState;
            this.omeClient = omeClient;
        }

        public void Initialize()
        {
            stageNavigator.OnStageTransitioned
                .Subscribe(_ => StartOmeClientAsync(appState).Forget())
                .AddTo(disposables);

            stageNavigator.OnStageTransitioning
                .Subscribe(async _ => await omeClient.LeaveAsync())
                .AddTo(disposables);
        }

        private async UniTask StartOmeClientAsync(AppState appState)
            => await omeClient.JoinAsync(appState.GroupName);

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
