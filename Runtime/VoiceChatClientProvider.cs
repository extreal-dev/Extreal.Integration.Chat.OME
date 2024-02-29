using System;
using Extreal.Integration.SFU.OME;

namespace Extreal.Integration.Chat.OME
{
    /// <summary>
    /// Class that provides voice chat client.
    /// </summary>
    public class VoiceChatClientProvider
    {
        /// <summary>
        /// Provides voice chat client.
        /// </summary>
        /// <param name="omeClient">OME client.</param>
        /// <param name="voiceChatConfig">Voice chat config.</param>
        /// <returns>Voice chat client.</returns>
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
