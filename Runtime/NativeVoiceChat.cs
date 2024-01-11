using UnityEngine;
using Cysharp.Threading.Tasks;
using UniRx;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;

namespace Extreal.Integration.VoiceChat
{
    public class NativeVoiceChat : DisposableBase
    {
        public IObservable<string> OnJoined => websocket.OnJoined;
        public IObservable<string> OnLeft => websocket.OnLeft;
        public IObservable<string> OnUserJoined => websocket.OnUserJoined;
        public IObservable<string> OnUserLeft => websocket.OnUserLeft;

        public IObservable<bool> OnMuted => onMuted;
        private readonly Subject<bool> onMuted;

        public IObservable<IReadOnlyDictionary<string, float>> OnAudioLevelChanged => onAudioLevelChanged;
        private readonly Subject<IReadOnlyDictionary<string, float>> onAudioLevelChanged;

        private readonly VoiceChatConfig voiceChatConfig;
        private readonly string userName = Guid.NewGuid().ToString();

        private readonly OmeWebSocket websocket;
        private string localStreamName;

        private readonly Transform voiceChatContainer;
        private (AudioSource inAudio, AudioStreamTrack inTrack, MediaStream inStream) inResource;
        private readonly AudioClip mic;
        private readonly Dictionary<string, (AudioSource outAudio, MediaStream outStream)> outResources = new Dictionary<string, (AudioSource, MediaStream)>();

        private bool mute;
        private float inVolume;
        private float outVolume;
        private float[] samples = new float[2048];

        private readonly Dictionary<string, float> audioLevelList = new Dictionary<string, float>();
        private readonly Dictionary<string, float> previousAudioLevelList = new Dictionary<string, float>();

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(NativeVoiceChat));


        [SuppressMessage("Usage", "CC0022")]
        public NativeVoiceChat(VoiceChatConfig voiceChatConfig)
        {
            voiceChatContainer = new GameObject("VoiceChatContainer").transform;
            UnityEngine.Object.DontDestroyOnLoad(voiceChatContainer);

            this.voiceChatConfig = voiceChatConfig;
            mute = this.voiceChatConfig.InitialMute;
            inVolume = this.voiceChatConfig.InitialInVolume;
            outVolume = this.voiceChatConfig.InitialOutVolume;
            var iceServers = voiceChatConfig.IceServerConfigs.Select(iceServerConfig => new RTCIceServer
            {
                urls = iceServerConfig.Urls.ToArray(),
                username = iceServerConfig.Username,
                credential = iceServerConfig.Credential,
            }).ToList();

            websocket = new OmeWebSocket(voiceChatConfig.ServerUrl, iceServers, userName).AddTo(disposables);
            onMuted = new Subject<bool>().AddTo(disposables);
            onAudioLevelChanged = new Subject<IReadOnlyDictionary<string, float>>().AddTo(disposables);

            websocket.AddPublishPcCreateHook(CreatePublishPc);
            websocket.AddSubscribePcCreateHook(CreateSubscribePc);
            websocket.AddPublishPcCloseHook(ClosePublishPc);
            websocket.AddSubscribePcCloseHook(CloseSubscribePc);

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

            OnJoined
                .Subscribe(streamName => localStreamName = streamName)
                .AddTo(disposables);

            OnLeft
                .Subscribe(_ => localStreamName = null)
                .AddTo(disposables);

            Observable.Interval(TimeSpan.FromSeconds(this.voiceChatConfig.AudioLevelCheckIntervalSeconds))
                .Subscribe(_ => AudioLevelChangeHandler())
                .AddTo(disposables);
        }

        protected override void ReleaseManagedResources()
        {
            Microphone.End(null);
            disposables.Dispose();
        }

        private void CreatePublishPc(string streamName, OmeRTCPeerConnection pc)
        {
            if (HasMicrophone())
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

        private void ClosePublishPc(string streamName, OmeRTCPeerConnection pc)
        {
            if (inResource.inAudio != null)
            {
                inResource.inAudio.Stop();
                UnityEngine.Object.Destroy(inResource.inAudio.gameObject);
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

        private void CloseSubscribePc(string streamName, OmeRTCPeerConnection pc)
        {
            if (outResources.TryGetValue(streamName, out var outResource))
            {
                if (outResource.outAudio != null)
                {
                    outResource.outAudio.Stop();
                    UnityEngine.Object.Destroy(outResource.outAudio.gameObject);
                }
                if (outResource.outStream != null)
                {
                    outResource.outStream.GetTracks().ToList().ForEach((track) => track.Stop());
                    outResource.outStream.Dispose();
                }
            }
        }

        private AudioSource CreateOutAudio(string name)
        {
            var outAudio = new GameObject(name).AddComponent<AudioSource>();
            outAudio.transform.SetParent(voiceChatContainer);
            outAudio.loop = true;
            return outAudio;
        }

        public async UniTask ConnectAsync(string roomName)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Connect: RoomName={roomName}, UserName={userName}, ServerUrl={voiceChatConfig.ServerUrl}");
            }

            await websocket.ConnectAsync(roomName);
        }

        public async UniTask DisconnectAsync()
        {
            await websocket.Close();

            mute = voiceChatConfig.InitialMute;
            inVolume = voiceChatConfig.InitialInVolume;
            outVolume = voiceChatConfig.InitialOutVolume;
        }

        public bool HasMicrophone() => mic != null;

        public void ToggleMute()
        {
            mute = !mute;
            inResource.inAudio.mute = mute;
            onMuted.OnNext(mute);
        }

        public void SetInVolume(float volume)
        {
            inVolume = Mathf.Clamp(volume, 0f, 1f);
            inResource.inAudio.volume = inVolume;
        }

        public void SetOutVolume(float volume)
        {
            outVolume = Mathf.Clamp(volume, 0f, 1f);
            outResources.Values.ToList().ForEach(outResource => outResource.outAudio.volume = outVolume);
        }

        private void AudioLevelChangeHandler()
        {
            if (string.IsNullOrEmpty(localStreamName))
            {
                return;
            }

            previousAudioLevelList.Clear();
            foreach (var streamName in audioLevelList.Keys)
            {
                previousAudioLevelList[streamName] = audioLevelList[streamName];
            }
            audioLevelList.Clear();

            if (inResource.inAudio != null)
            {
                var inAudioLevel = inResource.inAudio.mute ? 0f : GetAudioLevel(inResource.inAudio);
                audioLevelList[localStreamName] = inAudioLevel;
            }
            foreach ((var streamName, var outResource) in outResources)
            {
                if (outResource.outAudio != null)
                {
                    var outAudioLevel = GetAudioLevel(outResource.outAudio);
                    audioLevelList[streamName] = outAudioLevel;
                }
            }

            foreach (var streamName in previousAudioLevelList.Keys)
            {
                if (!audioLevelList.ContainsKey(streamName) || audioLevelList[streamName] != previousAudioLevelList[streamName])
                {
                    onAudioLevelChanged.OnNext(audioLevelList);
                    return;
                }
            }
            foreach (var streamName in audioLevelList.Keys)
            {
                if (!previousAudioLevelList.ContainsKey(streamName))
                {
                    onAudioLevelChanged.OnNext(audioLevelList);
                    return;
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
