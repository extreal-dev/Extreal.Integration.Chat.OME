using UnityEngine;
using Cysharp.Threading.Tasks;
using UniRx;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;

namespace Extreal.Integration.Chat.OME
{
    public abstract class VoiceChatClient : DisposableBase
    {
        public IObservable<string> OnJoined => onJoined;
        private readonly Subject<string> onJoined;
        protected void FireOnJoined(string streamName) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnJoined)}: streamName={streamName}");
            }
            onJoined.OnNext(streamName);
        });

        public IObservable<string> OnLeft => onLeft;
        private readonly Subject<string> onLeft;
        protected void FireOnLeft(string reason) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnLeft)}: reason={reason}");
            }
            onLeft.OnNext(reason);
        });

        public IObservable<string> OnUserJoined => onUserJoined;
        private readonly Subject<string> onUserJoined;
        protected void FireOnUserJoined(string streamName) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserJoined)}: streamName={streamName}");
            }
            onUserJoined.OnNext(streamName);
        });

        public IObservable<string> OnUserLeft => onUserLeft;
        private readonly Subject<string> onUserLeft;
        protected void FireOnUserLeft(string streamName) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserLeft)}: streamName={streamName}");
            }
            onUserLeft.OnNext(streamName);
        });

        public IObservable<bool> OnMuted => onMuted;
        private readonly Subject<bool> onMuted;
        protected void FireOnMuted(bool muted)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnMuted)}: muted={muted}");
            }
            onMuted.OnNext(muted);
        }

        public IObservable<IReadOnlyDictionary<string, float>> OnAudioLevelChanged => onAudioLevelChanged;
        private readonly Subject<IReadOnlyDictionary<string, float>> onAudioLevelChanged;
        protected void FireOnAudioLevelChanged(IReadOnlyDictionary<string, float> audioLevelList)
            => onAudioLevelChanged.OnNext(audioLevelList);

        private readonly string serverUrl;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(VoiceChatClient));


        [SuppressMessage("Usage", "CC0022")]
        protected VoiceChatClient(VoiceChatConfig voiceChatConfig)
        {
            serverUrl = voiceChatConfig.ServerUrl;

            onJoined = new Subject<string>().AddTo(disposables);
            onLeft = new Subject<string>().AddTo(disposables);
            onUserJoined = new Subject<string>().AddTo(disposables);
            onUserLeft = new Subject<string>().AddTo(disposables);
            onMuted = new Subject<bool>().AddTo(disposables);
            onAudioLevelChanged = new Subject<IReadOnlyDictionary<string, float>>().AddTo(disposables);

            Observable.Interval(TimeSpan.FromSeconds(voiceChatConfig.AudioLevelCheckIntervalSeconds))
                .Subscribe(_ => AudioLevelChangeHandler())
                .AddTo(disposables);
        }

        protected sealed override void ReleaseManagedResources()
        {
            DoReleaseManagedResources();
            disposables.Dispose();
        }

        protected abstract void DoReleaseManagedResources();

        public async UniTask ConnectAsync(string roomName)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Connect: RoomName={roomName}, ServerUrl={serverUrl}");
            }

            await DoConnectAsync(roomName);
        }

        protected abstract UniTask DoConnectAsync(string roomName);

        public abstract UniTask DisconnectAsync();

        public abstract bool HasMicrophone();

        public void ToggleMute()
        {
            if (!HasMicrophone())
            {
                return;
            }

            var mute = DoToggleMute();
            FireOnMuted(mute);
        }

        protected abstract bool DoToggleMute();

        public void SetInVolume(float volume)
        {
            if (!HasMicrophone())
            {
                return;
            }

            var inVolume = Mathf.Clamp(volume, 0f, 1f);
            DoSetInVolume(inVolume);
        }

        protected abstract void DoSetInVolume(float volume);

        public void SetOutVolume(float volume)
        {
            var outVolume = Mathf.Clamp(volume, 0f, 1f);
            DoSetOutVolume(outVolume);
        }

        protected abstract void DoSetOutVolume(float volume);

        protected abstract void AudioLevelChangeHandler();
    }
}
