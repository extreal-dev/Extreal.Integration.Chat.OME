using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Chat.OME.MVS.App;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.NameSettingScreen
{
    public class NameSettingScreenPresenter : DisposableBase, IInitializable
    {
        private readonly StageNavigator<StageName, SceneName> stageNavigator;
        private readonly AppState appState;
        private readonly NameSettingScreenView nameSettingScreenView;

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public NameSettingScreenPresenter(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            NameSettingScreenView nameSettingScreenView)
        {
            this.stageNavigator = stageNavigator;
            this.appState = appState;
            this.nameSettingScreenView = nameSettingScreenView;
        }

        public void Initialize()
        {
            nameSettingScreenView.OnGoButtonClicked
                .Subscribe(_ => stageNavigator.ReplaceAsync(StageName.GroupSelectionStage).Forget())
                .AddTo(disposables);

            nameSettingScreenView.OnNameChanged
                .Subscribe(appState.SetName)
                .AddTo(disposables);

            stageNavigator.OnStageTransitioned
                .Subscribe(_ => nameSettingScreenView.Initialize(appState.Name))
                .AddTo(disposables);
        }

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
