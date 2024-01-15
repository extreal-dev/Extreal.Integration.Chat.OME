import { OmeRTCPeerConnection } from "./OmeRTCPeerConnection";
import { OmeWebSocket } from "./OmeWebSocket";
import { v4 as uuidv4 } from "uuid";

type VoiceChatConfig = {
    serverUrl: string;
    iceServers: RTCIceServer[];
    initialMute: boolean;
    initialInVolume: number;
    initialOutVolume: number;
    isDebug: boolean;
};

type VoiceChatClientCallbacks = {
    onJoined: (streamName: string) => void;
    onLeft: (reason: string) => void;
    onUserJoined: (streamName: string) => void;
    onUserLeft: (streamName: string) => void;
    onAudioLevelChanged: (audioLevels: Map<string, number>) => void;
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
    private readonly isDebug;

    private readonly voiceChatConfig;
    private readonly hasMicrophone;
    private readonly callbacks;

    private socket: OmeWebSocket | null = null;
    private userName = uuidv4();
    private localStreamName = "";

    private mute;
    private inVolume;
    private outVolume;

    private audioContext: AudioContext | undefined;
    private inResource: InResource | undefined;
    private outResources = new Map<string, OutResource>();

    private audioLevelList = new Map<string, number>();
    private previousAudioLevelList = new Map<string, number>();

    constructor(voiceChatConfig: VoiceChatConfig, hasMicrophone: boolean, callbacks: VoiceChatClientCallbacks) {
        this.voiceChatConfig = voiceChatConfig;
        this.isDebug = voiceChatConfig.isDebug;
        this.hasMicrophone = hasMicrophone;
        this.callbacks = callbacks;

        this.mute = this.voiceChatConfig.initialMute;
        this.inVolume = this.voiceChatConfig.initialInVolume;
        this.outVolume = this.voiceChatConfig.initialOutVolume;

        const audioContextResumeFunc = () => {
            if (!this.audioContext) {
                this.audioContext = new AudioContext();
            }
            this.audioContext.resume();
            document.getElementById("unity-canvas")?.removeEventListener("touchstart", audioContextResumeFunc);
            document.getElementById("unity-canvas")?.removeEventListener("mousedown", audioContextResumeFunc);
            document.getElementById("unity-canvas")?.removeEventListener("keydown", audioContextResumeFunc);
        };
        document.getElementById("unity-canvas")?.addEventListener("touchstart", audioContextResumeFunc);
        document.getElementById("unity-canvas")?.addEventListener("mousedown", audioContextResumeFunc);
        document.getElementById("unity-canvas")?.addEventListener("keydown", audioContextResumeFunc);
    }

    public releaseManagedResources = () => {
        if (this.socket) {
            this.socket.releaseManagedResources();
        }
    };

    private createPublishPc = async (streamName: string, pc: OmeRTCPeerConnection) => {
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

    private createSubscribePc = (streamName: string, pc: OmeRTCPeerConnection) => {
        const outAudio = new Audio();
        const outResource = new OutResource();
        outResource.outAudio = outAudio;
        this.outResources.set(streamName, outResource);

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

    private closePublishPc = (streamName: string, pc: OmeRTCPeerConnection) => {
        if (!this.inResource) {
            return;
        }

        if (this.inResource.inStream) {
            this.inResource.inStream.getTracks().forEach((track) => track.stop());
        }

        this.inResource = undefined;
    };

    private closeSubscribePc = (streamName: string, pc: OmeRTCPeerConnection) => {
        const outResource = this.outResources.get(streamName);
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

        this.outResources.delete(streamName);
    };

    public connect = (roomName: string) => {
        this.socket = new OmeWebSocket(
            this.voiceChatConfig.serverUrl,
            this.voiceChatConfig.iceServers,
            roomName,
            this.userName,
            this.isDebug,
            {
                onJoined: (streamName) => {
                    this.callbacks.onJoined(streamName);
                    this.localStreamName = streamName;
                },
                onLeft: (reason) => {
                    this.callbacks.onLeft(reason);
                    this.localStreamName = "";
                },
                onUserJoined: this.callbacks.onUserJoined,
                onUserLeft: this.callbacks.onUserLeft,
            },
        );

        this.socket.addPublishPcCreateHook(this.createPublishPc);
        this.socket.addSubscribePcCreateHook(this.createSubscribePc);
        this.socket.addPublishPcCloseHook(this.closePublishPc);
        this.socket.addSubscribePcCloseHook(this.closeSubscribePc);
    };

    public disconnect = () => {
        if (!this.socket) {
            return;
        }

        this.socket.close();

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

    public handleAudioLevels = () => {
        if (!this.localStreamName) {
            return;
        }

        this.previousAudioLevelList.clear();
        this.audioLevelList.forEach((level, id) => {
            this.previousAudioLevelList.set(id, level);
        });
        this.audioLevelList.clear();

        if (this.inResource?.inAnalyzerNode) {
            const inAudioLevel = this.mute ? 0 : this.getAudioLevel(this.inResource.inAnalyzerNode);
            this.audioLevelList.set(this.localStreamName, inAudioLevel);
        }

        this.outResources.forEach((outResource, streamName) => {
            if (outResource.outAnalyzerNode) {
                const outAudioLevel = this.getAudioLevel(outResource.outAnalyzerNode);
                this.audioLevelList.set(streamName, outAudioLevel);
            }
        });

        this.previousAudioLevelList.forEach((level, streamName) => {
            if (!this.audioLevelList.has(streamName) || this.audioLevelList.get(streamName) !== level) {
                this.callbacks.onAudioLevelChanged(this.audioLevelList);
                return;
            }
        });
        this.audioLevelList.forEach((_, id) => {
            if (!this.previousAudioLevelList.has(id)) {
                this.callbacks.onAudioLevelChanged(this.audioLevelList);
                return;
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
