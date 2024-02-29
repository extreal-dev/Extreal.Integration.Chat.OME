namespace Extreal.Integration.Chat.OME
{
    /// <summary>
    /// Class that holds the config for voice chat.
    /// </summary>
    public class VoiceChatConfig
    {
        /// <summary>
        /// Initial status of mute.
        /// </summary>
        /// <value>True if initial muted, false otherwise.</value>
        public bool InitialMute { get; }
        /// <summary>
        /// Initial value of input volume.
        /// </summary>
        /// <value>Initial input volume (0.0 - 1.0)</value>
        public float InitialInVolume { get; }
        /// <summary>
        /// Initial value of output volume.
        /// </summary>
        /// <value>Initial output volume (0.0 - 1.0)</value>
        public float InitialOutVolume { get; }
        /// <summary>
        /// Value of audio level check interval.
        /// </summary>
        /// <value>Interval to check audioLevel (seconds)</value>
        public float AudioLevelCheckIntervalSeconds { get; }

        /// <summary>
        /// Creates VoiceChatConfig with initialMute.
        /// </summary>
        /// <param name="initialMute">True if initial muted, false otherwise.</param>
        /// <param name="initialInVolume">Initial input volume (0.0 - 1.0)</param>
        /// <param name="initialOutVolume">Initial output volume (0.0 - 1.0)</param>
        /// <param name="audioLevelCheckIntervalSeconds">Interval to check audioLevel (seconds)</param>
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
