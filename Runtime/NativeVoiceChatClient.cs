#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;
using Cysharp.Threading.Tasks;
using UniRx;
using Extreal.Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using Extreal.Integration.SFU.OME;

namespace Extreal.Integration.Chat.OME
{
    public class NativeVoiceChatClient : VoiceChatClient
    {
        private readonly NativeOmeClient omeClient;
        private readonly VoiceChatConfig voiceChatConfig;
        private string localStreamName;

        private readonly Transform voiceChatContainer;
        private (AudioSource inAudio, AudioStreamTrack inTrack, MediaStream inStream) inResource;
        private readonly AudioClip mic;
        private readonly Dictionary<string, (AudioSource outAudio, MediaStream outStream)> outResources = new Dictionary<string, (AudioSource, MediaStream)>();

        private bool mute;
        private float inVolume;
        private float outVolume;
        private float[] samples = new float[2048];

        private readonly Dictionary<string, float> audioLevels = new Dictionary<string, float>();

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(VoiceChatClient));


        [SuppressMessage("Usage", "CC0022")]
        public NativeVoiceChatClient(NativeOmeClient omeClient, VoiceChatConfig voiceChatConfig) : base(voiceChatConfig)
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
                .Subscribe(streamName => localStreamName = streamName)
                .AddTo(disposables);

            this.omeClient.OnLeft
                .Subscribe(_ => localStreamName = null)
                .AddTo(disposables);
        }

        protected override void DoReleaseManagedResources()
        {
            Microphone.End(null);
            if (voiceChatContainer != null && voiceChatContainer.gameObject != null)
            {
                Object.Destroy(voiceChatContainer.gameObject);
            }
            disposables.Dispose();
        }

        private void CreatePublishPc(string streamName, OmeRTCPeerConnection pc)
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

        private void CreateSubscribePc(string streamName, OmeRTCPeerConnection pc)
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
                    var outAudio = CreateOutAudio(streamName);
                    outAudio.SetTrack(track);
                    outAudio.Play();
                    outAudio.volume = outVolume;
                    outResources[streamName] = (outAudio, outStream);
                }
            };
        }

        private void ClosePublishPc(string streamName)
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

        private void CloseSubscribePc(string streamName)
        {
            if (outResources.TryGetValue(streamName, out var outResource))
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

                outResources.Remove(streamName);
            }
        }

        private AudioSource CreateOutAudio(string name)
        {
            var outAudio = new GameObject(name).AddComponent<AudioSource>();
            outAudio.transform.SetParent(voiceChatContainer);
            outAudio.loop = true;
            return outAudio;
        }

        public override void Clear()
        {
            mute = voiceChatConfig.InitialMute;
            inVolume = voiceChatConfig.InitialInVolume;
            outVolume = voiceChatConfig.InitialOutVolume;
        }

        public override bool HasMicrophone() => mic != null || inResource.inAudio?.clip != null;

        protected override bool DoToggleMute()
        {
            mute = !mute;
            if (inResource.inAudio != null)
            {
                inResource.inAudio.mute = mute;
            }
            return mute;
        }

        protected override void DoSetInVolume(float volume)
        {
            inVolume = volume;
            if (inResource.inAudio != null)
            {
                inResource.inAudio.volume = inVolume;
            }
        }

        protected override void DoSetOutVolume(float volume)
        {
            outVolume = volume;
            outResources.Values.ToList().ForEach(outResource => outResource.outAudio.volume = outVolume);
        }

        protected override void HandleAudioLevelChange()
        {
            if (string.IsNullOrEmpty(localStreamName))
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
                if (!audioLevels.ContainsKey(localStreamName) || audioLevels[localStreamName] != audioLevel)
                {
                    audioLevels[localStreamName] = audioLevel;
                    FireOnAudioLevelChanged(localStreamName, audioLevel);
                }
            }
        }

        private void HandleOutAudioLevelChange()
        {
            foreach (var streamName in outResources.Keys)
            {
                var outAudio = outResources[streamName].outAudio;
                var audioLevel = GetAudioLevel(outAudio);
                if (!audioLevels.ContainsKey(streamName) || audioLevels[streamName] != audioLevel)
                {
                    audioLevels[streamName] = audioLevel;
                    FireOnAudioLevelChanged(streamName, audioLevel);
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
