using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Chat.OME.MVS.App;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.SpaceControl
{
    public class SpaceControlPresenter : DisposableBase, IInitializable
    {
        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly StageNavigator<StageName, SceneName> stageNavigator;
        private readonly SpaceControlView spaceControlView;

        public SpaceControlPresenter(
            StageNavigator<StageName, SceneName> stageNavigator,
            SpaceControlView spaceControlView)
        {
            this.stageNavigator = stageNavigator;
            this.spaceControlView = spaceControlView;
        }

        public void Initialize()
            => spaceControlView.OnBackButtonClicked
                .Subscribe(_ => stageNavigator.ReplaceAsync(StageName.GroupSelectionStage).Forget())
                .AddTo(disposables);

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
