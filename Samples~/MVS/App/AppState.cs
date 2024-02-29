using System;
using System.Diagnostics.CodeAnalysis;
using Extreal.Core.Common.System;
using UniRx;

namespace Extreal.Integration.Chat.OME.MVS.App
{
    public class AppState : DisposableBase
    {
        public IObservable<string> OnNotificationReceived => onNotificationReceived.AddTo(disposables);
        [SuppressMessage("Usage", "CC0033")]
        private readonly Subject<string> onNotificationReceived = new Subject<string>();

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public JoinMethod JoinMethod { get; private set; } = JoinMethod.Create;
        public void SetJoinMethod(JoinMethod joinMethod) => JoinMethod = joinMethod;

        public string GroupName { get; private set; }
        public void SetGroupName(string groupName) => GroupName = groupName;

        public void Notify(string message) => onNotificationReceived.OnNext(message);

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
