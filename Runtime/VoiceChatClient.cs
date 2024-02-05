using UnityEngine;
using Cysharp.Threading.Tasks;
using UniRx;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Extreal.Integration.Chat.OME
{
    public abstract class VoiceChatClient : DisposableBase
    {
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

        public IObservable<(string id, float audioLevel)> OnAudioLevelChanged => onAudioLevelChanged;
        private readonly Subject<(string, float)> onAudioLevelChanged;
        protected void FireOnAudioLevelChanged(string id, float audioLevel)
            => onAudioLevelChanged.OnNext((id, audioLevel));

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(VoiceChatClient));


        [SuppressMessage("Usage", "CC0022")]
        protected VoiceChatClient()
        {
            onMuted = new Subject<bool>().AddTo(disposables);
            onAudioLevelChanged = new Subject<(string, float)>().AddTo(disposables);
        }

        protected sealed override void ReleaseManagedResources()
        {
            DoReleaseManagedResources();
            disposables.Dispose();
        }

        protected virtual void DoReleaseManagedResources() { }

        public abstract void Clear();

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
    }
}
