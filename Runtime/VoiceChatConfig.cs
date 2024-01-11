using System.Collections.Generic;

namespace Extreal.Integration.VoiceChat
{
    public class VoiceChatConfig
    {
        public string ServerUrl { get; }
        public List<IceServerConfig> IceServerConfigs { get; }
        public bool InitialMute { get; }
        public float InitialInVolume { get; }
        public float InitialOutVolume { get; }
        public float AudioLevelCheckIntervalSeconds { get; }

        public VoiceChatConfig
        (
            string serverUrl,
            List<IceServerConfig> iceServerConfigs = null,
            bool initialMute = true,
            float initialInVolume = 1f,
            float initialOutVolume = 1f,
            float audioLevelCheckIntervalSeconds = 1f
        )
        {
            ServerUrl = serverUrl;
            IceServerConfigs = iceServerConfigs ?? new List<IceServerConfig>();
            InitialMute = initialMute;
            InitialInVolume = initialInVolume;
            InitialOutVolume = initialOutVolume;
            AudioLevelCheckIntervalSeconds = audioLevelCheckIntervalSeconds;
        }
    }

    /// <summary>
    /// Class that holds ICE server configuration (such as a STUN or TURN server).
    /// </summary>
    public class IceServerConfig
    {
        /// <summary>
        /// ICE server URLs.
        /// </summary>
        public List<string> Urls { get; }

        /// <summary>
        /// Username for TURN server.
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Credential for TURN server.
        /// </summary>
        public string Credential { get; }

        /// <summary>
        /// Creates a new Ice server configuration.
        /// </summary>
        /// <param name="urls">ICE server URLs</param>
        /// <param name="username">Username for TURN server</param>
        /// <param name="credential">Credential for TURN server</param>
        public IceServerConfig(List<string> urls, string username = "", string credential = "")
        {
            Urls = urls;
            Username = username;
            Credential = credential;
        }
    }
}
