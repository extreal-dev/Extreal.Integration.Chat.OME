using System;
using UnityEngine;

namespace Extreal.Integration.Chat.OME
{
    public class VoiceChatClientProvider : MonoBehaviour
    {
        public static VoiceChatClient Provide(VoiceChatConfig voiceChatConfig)
        {
            if (voiceChatConfig == null)
            {
                throw new ArgumentNullException(nameof(voiceChatConfig));
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            return new NativeVoiceChatClient(voiceChatConfig);
#else
            return new WebGLVoiceChatClient(new WebGLVoiceChatConfig(voiceChatConfig));
#endif
        }
    }
}
