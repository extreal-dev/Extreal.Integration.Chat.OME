class OmeRTCPeerConnection extends RTCPeerConnection {
    private isDebug: boolean;
    private onCreateAnswerCompletion: ((sdp: RTCSessionDescriptionInit) => void) | undefined;
    private onIceCandidateCallBack: ((candidate: RTCIceCandidate) => void) | undefined;
    private onConnected: (() => void) | undefined;

    constructor(config: RTCConfiguration, isDebug: boolean) {
        super(config);
        this.isDebug = isDebug;
        this.onicecandidate = this.onIceCandidateEvent;
        this.onconnectionstatechange = this.onConnectionStateChangeEvent;
    }

    public setCreateAnswerCompletion = (callback: (sdp: RTCSessionDescriptionInit) => void) => {
        this.onCreateAnswerCompletion = callback;
    };

    public setIceCandidateCallback = (callback: (candidate: RTCIceCandidate) => void) => {
        this.onIceCandidateCallBack = callback;
    };

    public setConnectedCallback = (callback: () => void) => {
        this.onConnected = callback;
    };

    public createAnswerSdpAsync = async (offerSdp: RTCSessionDescription) => {
        if (this.isDebug) {
            console.log(`SetRemoteDescription[${offerSdp.type}]: ${offerSdp.sdp}`);
        }

        await this.setRemoteDescription(offerSdp);
        const answer = await this.createAnswer();
        await this.setLocalDescription(answer);
        if (this.onCreateAnswerCompletion) {
            this.onCreateAnswerCompletion(answer);
        }
    };

    private onIceCandidateEvent = (peerConnectionIceEvent: RTCPeerConnectionIceEvent) => {
        if (this.isDebug) {
            console.log(`OnIceCandidate: ${peerConnectionIceEvent.candidate?.candidate}`);
        }

        if (this.onIceCandidateCallBack && peerConnectionIceEvent.candidate) {
            this.onIceCandidateCallBack(peerConnectionIceEvent.candidate);
        }
    };

    private onConnectionStateChangeEvent = (event: Event) => {
        if (this.isDebug) {
            console.log(`OnConnectionStateChange: ${this.connectionState}`);
        }

        if (this.connectionState === "connected" && this.onConnected) {
            this.onConnected();
        }
    };
}

export { OmeRTCPeerConnection };
