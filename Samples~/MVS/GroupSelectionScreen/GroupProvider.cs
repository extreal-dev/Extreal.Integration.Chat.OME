using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Integration.SFU.OME;
using UniRx;

namespace Extreal.Integration.Chat.OME.MVS.GroupSelectionScreen
{
    public class GroupProvider : DisposableBase
    {
        public IObservable<List<Group>> OnGroupsUpdated => groups.AddTo(disposables).Skip(1);
        [SuppressMessage("Usage", "CC0033")]
        private readonly ReactiveProperty<List<Group>> groups = new ReactiveProperty<List<Group>>(new List<Group>());

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly OmeClient omeClient;

        [SuppressMessage("CodeCracker", "CC0057")]
        public GroupProvider(OmeClient omeClient) => this.omeClient = omeClient;

        protected override void ReleaseManagedResources() => disposables.Dispose();

        public async UniTask UpdateGroupsAsync()
        {
            this.groups.Value = await omeClient.ListGroupsAsync();
        }

        public Group FindByName(string name) => groups.Value.First(groups => groups.Name == name);
    }
}
