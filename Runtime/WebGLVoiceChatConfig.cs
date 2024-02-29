using Extreal.Core.Logging;

namespace Extreal.Integration.Chat.OME
{
    /// <summary>
    /// Class that holds config for WebGL voice chat.
    /// </summary>
    public class WebGLVoiceChatConfig : VoiceChatConfig
    {
        public bool IsDebug => Logger.IsDebug();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(WebGLVoiceChatConfig));

        public WebGLVoiceChatConfig(VoiceChatConfig voiceChatConfig)
            : base(voiceChatConfig.InitialMute, voiceChatConfig.InitialInVolume,
                voiceChatConfig.InitialOutVolume, voiceChatConfig.AudioLevelCheckIntervalSeconds)
        {
        }
    }
}
