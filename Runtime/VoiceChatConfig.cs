namespace Extreal.Integration.Chat.OME
{
    public class VoiceChatConfig
    {
        public bool InitialMute { get; }
        public float InitialInVolume { get; }
        public float InitialOutVolume { get; }
        public float AudioLevelCheckIntervalSeconds { get; }

        public VoiceChatConfig
        (
            bool initialMute = true,
            float initialInVolume = 1f,
            float initialOutVolume = 1f,
            float audioLevelCheckIntervalSeconds = 1f
        )
        {
            InitialMute = initialMute;
            InitialInVolume = initialInVolume;
            InitialOutVolume = initialOutVolume;
            AudioLevelCheckIntervalSeconds = audioLevelCheckIntervalSeconds;
        }
    }
}
