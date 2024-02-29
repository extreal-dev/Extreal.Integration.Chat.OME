using System.Collections.Generic;
using Extreal.Integration.SFU.OME;
using VContainer;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.ClientControl
{
    public class ClientControlScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var omeConfig = new OmeConfig(
                "ws://localhost:3040",
                new List<IceServerConfig>
                {
                    new IceServerConfig(new List<string>
                    {
                        "stun:stun.l.google.com:19302",
                        "stun:stun1.l.google.com:19302",
                        "stun:stun2.l.google.com:19302",
                        "stun:stun3.l.google.com:19302",
                        "stun:stun4.l.google.com:19302"
                    }, "test-name", "test-credential")
                });

            var omeClient = OmeClientProvider.Provide(omeConfig);
            builder.RegisterComponent(omeClient);

            var voiceChatConfig = new VoiceChatConfig();
            var voiceChatClient = VoiceChatClientProvider.Provide(omeClient, voiceChatConfig);
            builder.RegisterComponent(voiceChatConfig);
            builder.RegisterComponent(voiceChatClient);

            builder.RegisterEntryPoint<ClientControlPresenter>();
        }
    }
}
