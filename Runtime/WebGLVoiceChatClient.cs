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

        public WebGLVoiceChatClient(WebGLVoiceChatConfig voiceChatConfig)
        {
            instance = this;
            WebGLHelper.CallAction(WithPrefix(nameof(WebGLVoiceChatClient)), JsonVoiceChatConfig.ToJson(voiceChatConfig));
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnAudioLevelChanged)), HandleOnAudioLevelChanged);
        }

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnAudioLevelChanged(string id, string audioLevelStr)
        {
            var audioLevel = float.Parse(audioLevelStr);
            instance.FireOnAudioLevelChanged(id, audioLevel);
        }

        public override void Clear()
            => WebGLHelper.CallAction(WithPrefix(nameof(Clear)));

        public override bool HasMicrophone()
            => bool.Parse(WebGLHelper.CallFunction(WithPrefix(nameof(HasMicrophone))));

        protected override bool DoToggleMute()
            => bool.Parse(WebGLHelper.CallFunction(WithPrefix(nameof(DoToggleMute))));

        protected override void DoSetInVolume(float volume)
            => WebGLHelper.CallAction(WithPrefix(nameof(DoSetInVolume)), volume.ToString());

        protected override void DoSetOutVolume(float volume)
            => WebGLHelper.CallAction(WithPrefix(nameof(DoSetOutVolume)), volume.ToString());

        private static string WithPrefix(string name) => $"{nameof(WebGLVoiceChatClient)}#{name}";
    }

    [Serializable]
    public class JsonVoiceChatConfig
    {
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private bool initialMute;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private float initialInVolume;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private float initialOutVolume;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private float audioLevelCheckIntervalSeconds;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private bool isDebug;

        public static string ToJson(WebGLVoiceChatConfig voiceChatConfig)
        {
            var jsonVoiceChatConfig = new JsonVoiceChatConfig
            {
                initialMute = voiceChatConfig.InitialMute,
                initialInVolume = voiceChatConfig.InitialInVolume,
                initialOutVolume = voiceChatConfig.InitialOutVolume,
                audioLevelCheckIntervalSeconds = voiceChatConfig.AudioLevelCheckIntervalSeconds,
                isDebug = voiceChatConfig.IsDebug,
            };
            return JsonUtility.ToJson(jsonVoiceChatConfig);
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
