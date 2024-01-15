#if UNITY_WEBGL
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Extreal.Integration.Web.Common;
using AOT;

namespace Extreal.Integration.Chat.OME
{
    public class WebGLVoiceChatClient : VoiceChatClient
    {
        private static WebGLVoiceChatClient instance;

        public WebGLVoiceChatClient(WebGLVoiceChatConfig voiceChatConfig) : base(voiceChatConfig)
        {
            instance = this;
            WebGLHelper.CallAction(WithPrefix(nameof(WebGLVoiceChatClient)), JsonVoiceChatConfig.ToJson(voiceChatConfig));
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnJoined)), HandleOnJoined);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnLeft)), HandleOnLeft);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserJoined)), HandleOnUserJoined);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserLeft)), HandleOnUserLeft);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnAudioLevelChanged)), HandleOnAudioLevelChanged);
        }

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnJoined(string streamName, string unused) => instance.FireOnJoined(streamName);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnLeft(string reason, string unused) => instance.FireOnLeft(reason);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserJoined(string streamName, string unused) => instance.FireOnUserJoined(streamName);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserLeft(string streamName, string unused) => instance.FireOnUserLeft(streamName);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnAudioLevelChanged(string audioLevelListStr, string unused)
            => instance.FireOnAudioLevelChanged(JsonUtility.FromJson<JsonDictionary<string, float>>(audioLevelListStr).Dict);

        protected override void DoReleaseManagedResources()
            => WebGLHelper.CallAction(WithPrefix(nameof(DoReleaseManagedResources)));

#pragma warning disable CS1998
        protected override async UniTask DoConnectAsync(string roomName)
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DoConnectAsync)), roomName);

#pragma warning disable CS1998
        public override async UniTask DisconnectAsync()
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DisconnectAsync)));

        public override bool HasMicrophone()
            => bool.Parse(WebGLHelper.CallFunction(WithPrefix(nameof(HasMicrophone))));

        protected override bool DoToggleMute()
            => bool.Parse(WebGLHelper.CallFunction(WithPrefix(nameof(DoToggleMute))));

        protected override void DoSetInVolume(float volume)
            => WebGLHelper.CallAction(WithPrefix(nameof(DoSetInVolume)), volume.ToString());

        protected override void DoSetOutVolume(float volume)
            => WebGLHelper.CallAction(WithPrefix(nameof(DoSetOutVolume)), volume.ToString());

        protected override void AudioLevelChangeHandler()
            => WebGLHelper.CallAction(WithPrefix(nameof(AudioLevelChangeHandler)));

        private static string WithPrefix(string name) => $"{nameof(WebGLVoiceChatClient)}#{name}";
    }

    [Serializable]
    public class JsonVoiceChatConfig
    {
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private string serverUrl;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private JsonRtcIceServer[] iceServers;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private bool initialMute;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private float initialInVolume;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private float initialOutVolume;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private bool isDebug;

        public static string ToJson(WebGLVoiceChatConfig voiceChatConfig)
        {
            var jsonVoiceChatConfig = new JsonVoiceChatConfig
            {
                serverUrl = voiceChatConfig.ServerUrl,
                iceServers = voiceChatConfig.IceServerConfigs != null
                    ? voiceChatConfig.IceServerConfigs.Select(iceServerConfig => new JsonRtcIceServer(iceServerConfig)).ToArray()
                    : Array.Empty<JsonRtcIceServer>(),
                initialMute = voiceChatConfig.InitialMute,
                initialInVolume = voiceChatConfig.InitialInVolume,
                initialOutVolume = voiceChatConfig.InitialOutVolume,
                isDebug = voiceChatConfig.IsDebug,
            };
            return JsonUtility.ToJson(jsonVoiceChatConfig);
        }
    }

    [Serializable]
    public class JsonRtcIceServer
    {
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private string[] urls;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private string userName;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private string credential;

        public JsonRtcIceServer(IceServerConfig iceServerConfig)
        {
            urls = iceServerConfig.Urls.ToArray();
            userName = iceServerConfig.UserName;
            credential = iceServerConfig.Credential;
        }
    }

    [Serializable]
    public class JsonKeyValuePair<TKey, TValue>
    {
        public TKey Key => key;
        [SerializeField] private TKey key;

        public TValue Value => value;
        [SerializeField] private TValue value;
    }

    [Serializable]
    public class JsonDictionary<TKey, TValue> : ISerializationCallbackReceiver
    {
        public Dictionary<TKey, TValue> Dict { get; private set; }
        [SerializeField] private List<JsonKeyValuePair<TKey, TValue>> pairs;

        public void OnAfterDeserialize()
            => Dict = new Dictionary<TKey, TValue>(pairs.Select(pair => new KeyValuePair<TKey, TValue>(pair.Key, pair.Value)));

        public void OnBeforeSerialize() { }
    }
}
#endif
