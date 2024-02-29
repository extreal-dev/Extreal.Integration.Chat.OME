using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Chat.OME.MVS.App;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.GroupSelectionScreen
{
    public class GroupSelectionScreenPresenter : DisposableBase, IInitializable
    {
        private readonly StageNavigator<StageName, SceneName> stageNavigator;
        private readonly AppState appState;
        private readonly GroupSelectionScreenView groupSelectionScreenView;
        private readonly GroupProvider groupProvider;

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public GroupSelectionScreenPresenter(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            GroupSelectionScreenView groupSelectionScreenView,
            GroupProvider groupProvider)
        {
            this.stageNavigator = stageNavigator;
            this.appState = appState;
            this.groupSelectionScreenView = groupSelectionScreenView;
            this.groupProvider = groupProvider;
        }

        public void Initialize()
        {
            groupSelectionScreenView.OnJoinMethodChanged
                .Subscribe(appState.SetJoinMethod)
                .AddTo(disposables);

            groupSelectionScreenView.OnGroupNameChanged
                .Subscribe(appState.SetGroupName)
                .AddTo(disposables);

            groupSelectionScreenView.OnUpdateButtonClicked
                .Subscribe(async _ => await groupProvider.UpdateGroupsAsync())
                .AddTo(disposables);

            groupSelectionScreenView.OnGoButtonClicked
                .Subscribe(_ => stageNavigator.ReplaceAsync(StageName.VirtualStage).Forget())
                .AddTo(disposables);

            groupProvider.OnGroupsUpdated
                .Subscribe(groups =>
                {
                    var groupNames = groups.Select(group => group.Name).ToArray();
                    groupSelectionScreenView.UpdateGroupNames(groupNames);
                })
                .AddTo(disposables);

            stageNavigator.OnStageTransitioned
                .Subscribe(_ =>
                {
                    groupSelectionScreenView.Initialize();
                    groupSelectionScreenView.SetInitialValues(appState.JoinMethod);
                })
                .AddTo(disposables);
        }

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
