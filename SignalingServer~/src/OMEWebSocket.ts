import WebSocket from "ws";

export interface OMECommand {
    id?: string;
    command?: string;
    sdp?: RTCSessionDescriptionInit;
    candidates?: RTCIceCandidateInit[];

    roomName?: string;
    streamName?: string;
    userName?: string;

    code?: number;
    error?: string;
}

class OMEWebSocket extends WebSocket {
    onMessageCallback: ((command: OMECommand) => void) | null = null;

    constructor(url: string, protocols?: string | string[]) {
        super(url, protocols);
        this.onopen = () => {
            console.log("OMEWebSocket opened");
        };
        this.onclose = () => {
            console.log("OMEWebSocket closed");
        };
        this.onerror = (error) => {
            console.log("OMEWebSocket error: %o", error);
        };
        this.onmessage = (event) => {
            let msg;
            try {
                msg = JSON.parse(event.data.toString());
            } catch (e) {
                console.log("JSON parse error: %o", e);
                return;
            }
            console.log("OMEWebSocket onMessage: %o", msg);

            if (this.onMessageCallback) {
                /*
                if (!msg.id) msg.id = "";
                if (!msg.command) msg.command = "unknown";
                if (!msg.candidates) msg.candidates = [];
                if (!msg.sdp) msg.sdp = { type: "unknown", sdp: "" };
                if (!msg.roomName) msg.roomName = "";
                if (!msg.streamName) msg.streamName = "";
                */

                this.onMessageCallback(msg);
            }
        };
    }
}

export default OMEWebSocket;
