using UnityEngine;
using UniRx;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Extreal.Integration.Chat.OME
{
    /// <summary>
    /// Abstract class that becomes the base of voice chat client classes.
    /// </summary>
    public abstract class VoiceChatClient : DisposableBase
    {
        /// <summary>
        /// <para>Invokes immediately after the mute status is changed.</para>
        /// Arg: True if muted, false otherwise
        /// </summary>
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

        /// <summary>
        /// <para>Invokes when there is a change in audio level at the specified frequency.</para>
        /// Arg: Client ID and audio level value
        /// </summary>
        public IObservable<(string id, float audioLevel)> OnAudioLevelChanged => onAudioLevelChanged;
        private readonly Subject<(string, float)> onAudioLevelChanged;
        protected void FireOnAudioLevelChanged(string id, float audioLevel)
            => onAudioLevelChanged.OnNext((id, audioLevel));

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(VoiceChatClient));

        /// <summary>
        /// Creates a new voice chat client.
        /// </summary>
        [SuppressMessage("Usage", "CC0022")]
        protected VoiceChatClient()
        {
            onMuted = new Subject<bool>().AddTo(disposables);
            onAudioLevelChanged = new Subject<(string, float)>().AddTo(disposables);
        }

        /// <inheritdoc/>
        protected sealed override void ReleaseManagedResources()
        {
            DoReleaseManagedResources();
            disposables.Dispose();
        }

        /// <summary>
        /// Releases managed resources in sub class.
        /// </summary>
        protected virtual void DoReleaseManagedResources() { }

        /// <summary>
        /// Clears the status of this instance.
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Returns whether a microphone is available or not.
        /// </summary>
        /// <returns>True if it is available, false otherwise</returns>
        public abstract bool HasMicrophone();

        /// <summary>
        /// Toggles mute or not.
        /// </summary>
        public void ToggleMute()
        {
            if (!HasMicrophone())
            {
                return;
            }

            var mute = DoToggleMute();
            FireOnMuted(mute);
        }

        /// <summary>
        /// Toggles mute or not in sub class.
        /// </summary>
        protected abstract bool DoToggleMute();

        /// <summary>
        /// Sets input volume.
        /// </summary>
        /// <param name="volume">volume to be set (0.0 - 1.0)</param>
        public void SetInVolume(float volume)
        {
            if (!HasMicrophone())
            {
                return;
            }

            var inVolume = Mathf.Clamp(volume, 0f, 1f);
            DoSetInVolume(inVolume);
        }

        /// <summary>
        /// Sets input volume in sub class.
        /// </summary>
        /// <param name="volume">volume to be set (0.0 - 1.0)</param>
        protected abstract void DoSetInVolume(float volume);

        /// <summary>
        /// Sets output volume.
        /// </summary>
        /// <param name="volume">volume to be set (0.0 - 1.0)</param>
        public void SetOutVolume(float volume)
        {
            var outVolume = Mathf.Clamp(volume, 0f, 1f);
            DoSetOutVolume(outVolume);
        }

        /// <summary>
        /// Sets output volume in sub class.
        /// </summary>
        /// <param name="volume">volume to be set (0.0 - 1.0)</param>
        protected abstract void DoSetOutVolume(float volume);
    }
}
