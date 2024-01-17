using System;
using Extreal.Integration.SFU.OME;

namespace Extreal.Integration.Chat.OME
{
    public class VoiceChatClientProvider
    {
        public static VoiceChatClient Provide(OmeClient omeClient, VoiceChatConfig voiceChatConfig)
        {
            if (voiceChatConfig == null)
            {
                throw new ArgumentNullException(nameof(voiceChatConfig));
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            return new NativeVoiceChatClient(omeClient as NativeOmeClient, voiceChatConfig);
#else
            return new WebGLVoiceChatClient(new WebGLVoiceChatConfig(voiceChatConfig));
#endif
        }
    }
}
