import { addAction, addFunction, callback } from "@extreal-dev/extreal.integration.web.common";
import { OmeClientProvider } from "@extreal-dev/extreal.integration.sfu.ome";
import { VoiceChatClient } from "./VoiceChatClient";

let hasMicrophone = false;
(async () => {
    try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        stream.getTracks().forEach((track) => track.stop());
        hasMicrophone = true;
    } catch (e) {
        hasMicrophone = false;
    }
})();

class VoiceChatAdapter {
    private voiceChatClient: VoiceChatClient | null = null;

    public adapt = (getOmeClient: OmeClientProvider) => {
        addAction(this.withPrefix("WebGLVoiceChatClient"), (jsonVoiceChatConfig) => {
            const voiceChatConfig = JSON.parse(jsonVoiceChatConfig);
            if (voiceChatConfig.isDebug) {
                console.log(voiceChatConfig);
            }
            this.voiceChatClient = new VoiceChatClient(getOmeClient, voiceChatConfig, hasMicrophone, {
                onAudioLevelChanged: (id, audioLevel) => callback(this.withPrefix("HandleOnAudioLevelChanged"), id, audioLevel.toString()),
            });
        });

        addAction(this.withPrefix("Clear"), () => this.getVoiceChatClient().clear());
        addFunction(this.withPrefix("HasMicrophone"), () => hasMicrophone.toString());
        addFunction(this.withPrefix("DoToggleMute"), () => this.getVoiceChatClient().toggleMute().toString());
        addAction(this.withPrefix("DoSetInVolume"), (volume) => this.getVoiceChatClient().setInVolume(Number(volume)));
        addAction(this.withPrefix("DoSetOutVolume"), (volume) =>
            this.getVoiceChatClient().setOutVolume(Number(volume)),
        );
    };

    private withPrefix = (name: string) => `WebGLVoiceChatClient#${name}`;

    public getVoiceChatClient = () => {
        if (!this.voiceChatClient) {
            throw new Error("Call the WebGLVoiceChatClient constructor first in Unity.");
        }
        return this.voiceChatClient;
    };
}

export { VoiceChatAdapter };
