import { OmeClientProvider } from "@extreal-dev/extreal.integration.sfu.ome";

type VoiceChatConfig = {
    initialMute: boolean;
    initialInVolume: number;
    initialOutVolume: number;
    audioLevelCheckIntervalSeconds: number;
    isDebug: boolean;
};

type VoiceChatClientCallbacks = {
    onAudioLevelChanged: (id: string, audioLevel: number) => void;
};

class InResource {
    public inStream: MediaStream | undefined;
    public inTrack: MediaStreamTrack | undefined;
    public inGainNode: GainNode | undefined;
    public inAnalyzerNode: AnalyserNode | undefined;
}

class OutResource {
    public outAudio: HTMLAudioElement | undefined;
    public outStream: MediaStream | undefined;
    public outGainNode: GainNode | undefined;
    public outAnalyzerNode: AnalyserNode | undefined;
}

class VoiceChatClient {
    private readonly getOmeClient;
    private readonly voiceChatConfig;
    private readonly hasMicrophone;
    private readonly callbacks;

    private mute;
    private inVolume;
    private outVolume;

    private audioContext: AudioContext | undefined;
    private inResource: InResource | undefined;
    private outResources = new Map<string, OutResource>();

    private audioLevels = new Map<string, number>();

    constructor(
        getOmeClient: OmeClientProvider,
        voiceChatConfig: VoiceChatConfig,
        hasMicrophone: boolean,
        callbacks: VoiceChatClientCallbacks,
    ) {
        this.getOmeClient = getOmeClient;
        this.voiceChatConfig = voiceChatConfig;
        this.hasMicrophone = hasMicrophone;
        this.callbacks = callbacks;

        this.mute = this.voiceChatConfig.initialMute;
        this.inVolume = this.voiceChatConfig.initialInVolume;
        this.outVolume = this.voiceChatConfig.initialOutVolume;

        this.getOmeClient().addPublishPcCreateHook(this.createPublishPc);
        this.getOmeClient().addSubscribePcCreateHook(this.createSubscribePc);
        this.getOmeClient().addPublishPcCloseHook(this.closePublishPc);
        this.getOmeClient().addSubscribePcCloseHook(this.closeSubscribePc);

        const resumeAudioContext = () => {
            if (!this.audioContext) {
                this.audioContext = new AudioContext();
            }
            this.audioContext.resume();
        };

        this.executeOnceOnSpecifiedEvents("unity-canvas", ["touchstart", "mousedown", "keydown"], resumeAudioContext);

        setInterval(this.handleAudioLevelChange, voiceChatConfig.audioLevelCheckIntervalSeconds * 1000);
    }

    private executeOnceOnSpecifiedEvents = (targetElementId: string, eventNames: string[], action: () => void) => {
        const executeActionAndRemove = () => {
            action();
            eventNames.forEach((eventName) => {
                document.getElementById(targetElementId)?.removeEventListener(eventName, executeActionAndRemove);
            });
        };

        eventNames.forEach((eventName) => {
            document.getElementById(targetElementId)?.addEventListener(eventName, executeActionAndRemove);
        });
    };

    private createPublishPc = async (clientId: string, pc: RTCPeerConnection) => {
        if (!this.audioContext) {
            this.audioContext = new AudioContext();
        }

        if (this.hasMicrophone) {
            this.inResource = new InResource();
            const micStream = await navigator.mediaDevices.getUserMedia({ audio: true });

            const sourceNode = this.audioContext.createMediaStreamSource(micStream);
            const inGainNode = this.audioContext.createGain();
            const inAnalyzerNode = this.audioContext.createAnalyser();
            const destinationNode = this.audioContext.createMediaStreamDestination();
            const inStream = destinationNode.stream;
            sourceNode.connect(inGainNode);
            inGainNode.connect(inAnalyzerNode);
            inAnalyzerNode.connect(destinationNode);

            const inTrack = inStream.getAudioTracks()[0];
            pc.addTrack(inTrack, inStream);
            inTrack.enabled = !this.mute;

            inGainNode.gain.value = this.inVolume;

            this.inResource.inStream = inStream;
            this.inResource.inTrack = inTrack;
            this.inResource.inGainNode = inGainNode;
            this.inResource.inAnalyzerNode = inAnalyzerNode;
        }
    };

    private createSubscribePc = (clientId: string, pc: RTCPeerConnection) => {
        const outAudio = new Audio();
        const outResource = new OutResource();
        outResource.outAudio = outAudio;
        this.outResources.set(clientId, outResource);

        pc.addEventListener("track", (event) => {
            if (!this.audioContext) {
                this.audioContext = new AudioContext();
            }
            const outStream = event.streams[0];
            const sourceNode = this.audioContext.createMediaStreamSource(outStream);
            const outGainNode = this.audioContext.createGain();
            const outAnalyzerNode = this.audioContext.createAnalyser();

            sourceNode.connect(outGainNode);
            outGainNode.connect(outAnalyzerNode);
            outAnalyzerNode.connect(this.audioContext.destination);

            outAudio.srcObject = outStream;

            outResource.outStream = outStream;
            outResource.outGainNode = outGainNode;
            outResource.outAnalyzerNode = outAnalyzerNode;
        });
    };

    private closePublishPc = (clientId: string) => {
        if (!this.inResource) {
            return;
        }

        if (this.inResource.inStream) {
            this.inResource.inStream.getTracks().forEach((track) => track.stop());
        }

        this.inResource = undefined;
    };

    private closeSubscribePc = (clientId: string) => {
        const outResource = this.outResources.get(clientId);
        if (!outResource) {
            return;
        }

        if (outResource.outAudio) {
            outResource.outAudio.pause();
            outResource.outAudio.remove();
        }
        if (outResource.outStream) {
            outResource.outStream.getTracks().forEach((track) => track.stop());
        }

        this.outResources.delete(clientId);
    };

    public clear = () => {
        this.mute = this.voiceChatConfig.initialMute;
        this.inVolume = this.voiceChatConfig.initialInVolume;
        this.outVolume = this.voiceChatConfig.initialOutVolume;
    };

    public toggleMute = () => {
        if (!this.inResource || !this.inResource.inTrack) {
            return this.mute;
        }

        this.mute = !this.mute;
        this.inResource.inTrack.enabled = !this.mute;
        return this.mute;
    };

    public setInVolume = (volume: number) => {
        if (!this.inResource || !this.inResource.inGainNode) {
            return;
        }

        if (!this.audioContext) {
            this.audioContext = new AudioContext();
        }
        this.inVolume = volume;
        this.inResource.inGainNode.gain.setValueAtTime(this.inVolume, this.audioContext.currentTime);
    };

    public setOutVolume = (volume: number) => {
        if (!this.audioContext) {
            this.audioContext = new AudioContext();
        }

        this.outVolume = volume;
        for (const outResource of this.outResources.values()) {
            if (outResource.outGainNode) {
                outResource.outGainNode.gain.setValueAtTime(this.outVolume, this.audioContext.currentTime);
            }
        }
    };

    public handleAudioLevelChange = () => {
        if (!this.getOmeClient().localClientId) {
            return;
        }

        this.handleInAudioLevelChange(this.getOmeClient().localClientId);
        this.handleOutAudioLevelChange();
    };

    private handleInAudioLevelChange = (localClientId: string) => {
        if (this.inResource?.inAnalyzerNode) {
            const audioLevel = this.mute ? 0 : this.getAudioLevel(this.inResource.inAnalyzerNode);
            if (!this.audioLevels.has(localClientId) || this.audioLevels.get(localClientId) !== audioLevel) {
                this.audioLevels.set(localClientId, audioLevel);
                this.callbacks.onAudioLevelChanged(localClientId, audioLevel);
            }
        }
    };

    private handleOutAudioLevelChange = () => {
        this.outResources.forEach((outResource, clientId) => {
            if (outResource.outAnalyzerNode) {
                const audioLevel = this.getAudioLevel(outResource.outAnalyzerNode);
                if (!this.audioLevels.has(clientId) || this.audioLevels.get(clientId) !== audioLevel) {
                    this.audioLevels.set(clientId, audioLevel);
                    this.callbacks.onAudioLevelChanged(clientId, audioLevel);
                }
            }
        });
    };

    private getAudioLevel = (analyserNode: AnalyserNode) => {
        const samples = new Float32Array(analyserNode.fftSize);
        analyserNode.getFloatTimeDomainData(samples);
        const audioLevel = this.absAverage(samples);
        return audioLevel;
    };

    private absAverage = (values: Float32Array) => {
        const total = values.reduce((accumulator, current) => accumulator + Math.abs(current));
        return total / values.length;
    };
}

export { VoiceChatClient };
