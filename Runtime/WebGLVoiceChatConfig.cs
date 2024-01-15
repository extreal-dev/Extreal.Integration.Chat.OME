using Extreal.Core.Logging;

namespace Extreal.Integration.Chat.OME
{
    public class WebGLVoiceChatConfig : VoiceChatConfig
    {
        public bool IsDebug => Logger.IsDebug();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(WebGLVoiceChatConfig));

        public WebGLVoiceChatConfig(VoiceChatConfig voiceChatConfig)
            : base(voiceChatConfig.ServerUrl, voiceChatConfig.IceServerConfigs, voiceChatConfig.InitialMute,
                voiceChatConfig.InitialInVolume, voiceChatConfig.InitialOutVolume, voiceChatConfig.AudioLevelCheckIntervalSeconds)
        {
        }
    }
}
