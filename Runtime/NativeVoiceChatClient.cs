#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;
using UniRx;
using Extreal.Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using Extreal.Integration.SFU.OME;
using System;
using Object = UnityEngine.Object;

namespace Extreal.Integration.Chat.OME
{
    /// <summary>
    /// Class that handles voice chat client for native application.
    /// </summary>
    public class NativeVoiceChatClient : VoiceChatClient
    {
        private readonly NativeOmeClient omeClient;
        private readonly VoiceChatConfig voiceChatConfig;
        private string localClientId;

        private readonly Transform voiceChatContainer;
        private (AudioSource inAudio, AudioStreamTrack inTrack, MediaStream inStream) inResource;
        private readonly AudioClip mic;
        private readonly Dictionary<string, (AudioSource outAudio, MediaStream outStream)> outResources = new Dictionary<string, (AudioSource, MediaStream)>();

        private bool mute;
        private float inVolume;
        private float outVolume;
        private float[] samples = new float[2048];

        private readonly Dictionary<string, float> audioLevels = new Dictionary<string, float>();

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(VoiceChatClient));

        /// <summary>
        /// Creates NativeVoiceChatClient with omeClient and voiceChatConfig.
        /// </summary>
        /// <param name="omeClient">OME client.</param>
        /// <param name="voiceChatConfig">Voice chat config.</param>
        [SuppressMessage("Usage", "CC0022")]
        public NativeVoiceChatClient(NativeOmeClient omeClient, VoiceChatConfig voiceChatConfig)
        {
            voiceChatContainer = new GameObject("VoiceChatContainer").transform;
            Object.DontDestroyOnLoad(voiceChatContainer);

            this.voiceChatConfig = voiceChatConfig;
            mute = this.voiceChatConfig.InitialMute;
            inVolume = this.voiceChatConfig.InitialInVolume;
            outVolume = this.voiceChatConfig.InitialOutVolume;

            var audioConf = AudioSettings.GetConfiguration();
            audioConf.dspBufferSize = 256;
            AudioSettings.Reset(audioConf);

            if (Microphone.devices.Length > 0)
            {
                mic = Microphone.Start(null, true, 1, 48000);
                while (Microphone.GetPosition(null) > 0)
                {
                    // do nothing
                }
            }

            this.omeClient = omeClient;
            this.omeClient.AddPublishPcCreateHook(CreatePublishPc);
            this.omeClient.AddSubscribePcCreateHook(CreateSubscribePc);
            this.omeClient.AddPublishPcCloseHook(ClosePublishPc);
            this.omeClient.AddSubscribePcCloseHook(CloseSubscribePc);

            this.omeClient.OnJoined
                .Subscribe(clientId => localClientId = clientId)
                .AddTo(disposables);

            this.omeClient.OnLeft
                .Subscribe(_ => localClientId = null)
                .AddTo(disposables);

            Observable.Interval(TimeSpan.FromSeconds(voiceChatConfig.AudioLevelCheckIntervalSeconds))
                .Subscribe(_ => HandleAudioLevelChange())
                .AddTo(disposables);
        }

        /// <inheritdoc/>
        protected override void DoReleaseManagedResources()
        {
            Microphone.End(null);
            if (voiceChatContainer != null && voiceChatContainer.gameObject != null)
            {
                Object.Destroy(voiceChatContainer.gameObject);
            }
            disposables.Dispose();
        }

        private void CreatePublishPc(string clientId, RTCPeerConnection pc)
        {
            inResource.inAudio = new GameObject("InAudio").AddComponent<AudioSource>();
            inResource.inAudio.transform.SetParent(voiceChatContainer);

            inResource.inAudio.loop = true;
            inResource.inAudio.clip = mic;
            inResource.inAudio.Play();
            inResource.inAudio.mute = mute;
            inResource.inAudio.volume = inVolume;

            inResource.inTrack = new AudioStreamTrack(inResource.inAudio)
            {
                Loopback = false
            };
            inResource.inStream = new MediaStream();
            pc.AddTrack(inResource.inTrack, inResource.inStream);
        }

        private void CreateSubscribePc(string clientId, RTCPeerConnection pc)
        {
            var outStream = new MediaStream();
            pc.OnTrack = (RTCTrackEvent e) =>
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug($"OnTrack: Kind={e.Track.Kind}");
                }

                if (e.Track.Kind == TrackKind.Audio)
                {
                    outStream.AddTrack(e.Track);
                }
            };

            outStream.OnAddTrack = e =>
            {
                if (e.Track is AudioStreamTrack track)
                {
                    var outAudio = CreateOutAudio(clientId);
                    outAudio.SetTrack(track);
                    outAudio.Play();
                    outAudio.volume = outVolume;
                    outResources[clientId] = (outAudio, outStream);
                }
            };
        }

        private void ClosePublishPc(string clientId)
        {
            if (inResource.inAudio != null)
            {
                inResource.inAudio.Stop();
                Object.Destroy(inResource.inAudio.gameObject);
            }
            if (inResource.inTrack != null)
            {
                inResource.inTrack.Dispose();
            }
            if (inResource.inStream != null)
            {
                inResource.inStream.GetTracks().ToList().ForEach(track => track.Stop());
                inResource.inStream.Dispose();
            }
            inResource = (default, default, default);
        }

        private void CloseSubscribePc(string clientId)
        {
            if (outResources.TryGetValue(clientId, out var outResource))
            {
                if (outResource.outAudio != null)
                {
                    outResource.outAudio.Stop();
                    Object.Destroy(outResource.outAudio.gameObject);
                }
                if (outResource.outStream != null)
                {
                    outResource.outStream.GetTracks().ToList().ForEach((track) => track.Stop());
                    outResource.outStream.Dispose();
                }

                outResources.Remove(clientId);
            }
        }

        private AudioSource CreateOutAudio(string name)
        {
            var outAudio = new GameObject(name).AddComponent<AudioSource>();
            outAudio.transform.SetParent(voiceChatContainer);
            outAudio.loop = true;
            return outAudio;
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            mute = voiceChatConfig.InitialMute;
            inVolume = voiceChatConfig.InitialInVolume;
            outVolume = voiceChatConfig.InitialOutVolume;
        }

        /// <inheritdoc/>
        public override bool HasMicrophone() => mic != null || inResource.inAudio?.clip != null;

        /// <inheritdoc/>
        protected override bool DoToggleMute()
        {
            mute = !mute;
            if (inResource.inAudio != null)
            {
                inResource.inAudio.mute = mute;
            }
            return mute;
        }

        /// <inheritdoc/>
        protected override void DoSetInVolume(float volume)
        {
            inVolume = volume;
            if (inResource.inAudio != null)
            {
                inResource.inAudio.volume = inVolume;
            }
        }

        /// <inheritdoc/>
        protected override void DoSetOutVolume(float volume)
        {
            outVolume = volume;
            outResources.Values.ToList().ForEach(outResource => outResource.outAudio.volume = outVolume);
        }

        private void HandleAudioLevelChange()
        {
            if (string.IsNullOrEmpty(localClientId))
            {
                return;
            }

            HandleInAudioLevelChange();
            HandleOutAudioLevelChange();
        }

        private void HandleInAudioLevelChange()
        {
            if (inResource.inAudio != null)
            {
                var audioLevel = mute ? 0f : GetAudioLevel(inResource.inAudio);
                if (!audioLevels.ContainsKey(localClientId) || audioLevels[localClientId] != audioLevel)
                {
                    audioLevels[localClientId] = audioLevel;
                    FireOnAudioLevelChanged(localClientId, audioLevel);
                }
            }
        }

        private void HandleOutAudioLevelChange()
        {
            foreach (var clientId in outResources.Keys)
            {
                var outAudio = outResources[clientId].outAudio;
                var audioLevel = GetAudioLevel(outAudio);
                if (!audioLevels.ContainsKey(clientId) || audioLevels[clientId] != audioLevel)
                {
                    audioLevels[clientId] = audioLevel;
                    FireOnAudioLevelChanged(clientId, audioLevel);
                }
            }
        }

        private float GetAudioLevel(AudioSource audioSource)
        {
            audioSource.GetOutputData(samples, 0);
            var audioLevel = samples.Average(Mathf.Abs);
            return audioLevel;
        }
    }
}
#endif
