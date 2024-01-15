import { isAsync } from "@extreal-dev/extreal.integration.web.common";
import { OmeCommand } from "./OmeCommand";
import { OmeRTCPeerConnection } from "./OmeRTCPeerConnection";

type OmeWebSocketCallbacks = {
    onJoined: (streamName: string) => void;
    onLeft: (reason: string) => void;
    onUserJoined: (streamName: string) => void;
    onUserLeft: (streamName: string) => void;
};

type PcHook = (streamName: string, pc: OmeRTCPeerConnection) => void | Promise<void>;

class OmeWebSocket extends WebSocket {
    private defaultIceServers;
    private roomName;
    private userName;
    private isDebug;
    private callbacks;

    private isConnected = false;
    private localStreamName = "";

    private publishPcCreateHooks: PcHook[] = [];
    private subscribePcCreateHooks: PcHook[] = [];
    private publishPcCloseHooks: PcHook[] = [];
    private subscribePcCloseHooks: PcHook[] = [];

    private publishConnection: OmeRTCPeerConnection | null = null;
    private subscribeConnections = new Map<string, OmeRTCPeerConnection>();

    private subscribeRetryCounts = new Map<string, number>();
    private readonly MaxSubscribeRetries = 20;
    private readonly SubscribeRetryInterval = 0.5;

    constructor(
        url: string,
        defaultIceServers: RTCIceServer[],
        roomName: string,
        userName: string,
        isDebug: boolean,
        callbacks: OmeWebSocketCallbacks,
    ) {
        super(url);
        this.defaultIceServers = defaultIceServers;
        this.roomName = roomName;
        this.userName = userName;
        this.isDebug = isDebug;
        this.callbacks = callbacks;

        this.onopen = this.onOpenEvent;
        this.onclose = this.onCloseEvent;
        this.onerror = this.onErrorEvent;
        this.onmessage = this.onMessageEvent;
    }

    public releaseManagedResources = () => {
        this.onopen = null;
        this.onclose = null;
        this.onerror = null;
        this.onmessage = null;

        this.closeAllRTCConnections("Normal");
        this.close();
    };

    private onOpenEvent = () => {
        if (this.isDebug) {
            console.log("OnOpen");
        }
        this.isConnected = true;
        this.publish(this.roomName, this.userName);
    };

    private publish = (roomName: string, userName: string) => {
        if (!this.isConnected) {
            if (this.isDebug) {
                console.log("WebSocket is not connected");
            }
            return;
        }
        this.send(OmeCommand.createPublishOffer(roomName, userName));
    };

    private onCloseEvent = (ev: CloseEvent) => {
        if (this.isDebug) {
            console.log(`OnClose: reason=${ev.reason}`);
        }
        this.isConnected = false;
        this.closeAllRTCConnections(ev.reason);
    };

    private onErrorEvent = (ev: Event) => {
        if (this.isDebug) {
            console.log(`OnError at ${ev.type}`);
        }
        this.closeAllRTCConnections("error");
    };

    private closeAllRTCConnections = (reason: string) => {
        if (this.isDebug) {
            console.log(`Left room: roomName=${this.roomName}`);
        }

        if (this.publishConnection) {
            this.publishPcCloseHooks.forEach((hook) =>
                this.handleHook(
                    this.closeAllRTCConnections.name,
                    hook,
                    this.localStreamName,
                    this.publishConnection as OmeRTCPeerConnection,
                ),
            );
            this.close();
            this.publishConnection = null;
            this.localStreamName = "";
        }

        for (const streamName of this.subscribeConnections.keys()) {
            this.closeConnection(streamName);
        }
        this.subscribeConnections.clear();

        this.callbacks.onLeft(reason);
    };

    private closeConnection = (streamName: string) => {
        const connection = this.subscribeConnections.get(streamName);
        if (connection) {
            this.subscribePcCloseHooks.forEach((hook) =>
                this.handleHook(this.closeConnection.name, hook, streamName, connection),
            );
            connection.close();
            this.subscribeConnections.delete(streamName);
        }
    };

    private onMessageEvent = (ev: MessageEvent<string>) => {
        const command = Object.assign(new OmeCommand(), JSON.parse(ev.data));
        if (this.isDebug) {
            console.log(`OnMessage: ${command.toString()}`);
        }

        if (command.command === "publishOffer") {
            this.publishOfferEvent(command);
        } else if (command.command === "subscribeOffer") {
            this.subscribeOfferEvent(command);
        } else if (command.command === "join") {
            this.joinMemberEvent(command);
        } else if (command.command === "leave") {
            this.leaveMemberEvent(command);
        }
    };

    private publishOfferEvent = async (command: OmeCommand) => {
        let isSetLocalCandidate = false;

        const configuration = this.createRTCConfiguration(command.getIceServers());
        const pc = new OmeRTCPeerConnection(configuration, this.isDebug);
        pc.setCreateAnswerCompletion((answer) => {
            const message = OmeCommand.createAnswerMessage(command.id, answer);
            this.send(message);
        });
        pc.setIceCandidateCallback((candidate) => {
            if (!isSetLocalCandidate) {
                // const sdpMid = candidate.sdpMid;
                if (command.candidates) {
                    for (const candidateInit of command.candidates) {
                        // candidateInit.sdpMid = sdpMid;
                        pc.addIceCandidate(candidateInit);
                    }
                }
                isSetLocalCandidate = true;
            }
            const message = OmeCommand.createIceCandidate(command.id, candidate);
            this.send(message);
        });
        pc.setConnectedCallback(() => {
            if (this.isDebug) {
                console.log(`Joined room: roomName=${this.roomName}`);
            }

            const message = OmeCommand.createJoinMessage(command.id);
            this.send(message);
            this.callbacks.onJoined(command.getStreamName());
        });

        for (const hook of this.publishPcCreateHooks) {
            await this.handleHook(this.publishOfferEvent.name, hook, command.getStreamName(), pc);
        }

        this.localStreamName = command.getStreamName();
        this.publishConnection = pc;
        pc.createAnswerSdpAsync(command.getSdp());
    };

    private subscribeOfferEvent = (command: OmeCommand) => {
        const currentRetryCount = this.subscribeRetryCounts.get(command.getStreamName()) ?? 0;

        if (command.error) {
            if (command.error === "Cannot create offer") {
                if (currentRetryCount < this.MaxSubscribeRetries) {
                    setTimeout(() => {
                        this.subscribe(command.getStreamName());
                        this.subscribeRetryCounts.set(command.getStreamName(), currentRetryCount + 1);
                    }, this.SubscribeRetryInterval * 1000);
                } else {
                    if (this.isDebug) {
                        console.error(`Maximum retryCount reached: ${command.getStreamName()}`);
                    }
                    this.subscribeRetryCounts.delete(command.getStreamName());
                }
            } else {
                if (this.isDebug) {
                    console.error(`Subscribe error: ${command.error}`);
                }
                this.subscribeRetryCounts.delete(command.getStreamName());
            }
            return;
        } else {
            // エラーではないが，SDPがない場合は何もしない
            if (this.isDebug) {
                console.log(`SubscribeOfferEvent: id=${command.id}, sdp=${command.sdp}`);
            }

            if (!command.id) {
                return;
            }
        }
        this.subscribeRetryCounts.delete(command.getStreamName());

        let isSetLocalCandidate = false;

        const configuration = this.createRTCConfiguration(command.getIceServers());
        const pc = new OmeRTCPeerConnection(configuration, this.isDebug);
        pc.setCreateAnswerCompletion((answer) => {
            const message = OmeCommand.createAnswerMessage(command.id, answer);
            this.send(message);
        });
        pc.setIceCandidateCallback((candidate) => {
            if (!isSetLocalCandidate) {
                const sdpMid = candidate.sdpMid;
                if (command.candidates) {
                    for (const candidateInit of command.candidates) {
                        candidateInit.sdpMid = sdpMid;
                        const rtcIceCandidate = new RTCIceCandidate(candidateInit);
                        pc.addIceCandidate(rtcIceCandidate);
                    }
                }
                isSetLocalCandidate = true;
            }
            const message = OmeCommand.createIceCandidate(command.id, candidate);
            this.send(message);
        });

        this.subscribePcCreateHooks.forEach((hook) =>
            this.handleHook(this.subscribeOfferEvent.name, hook, command.getStreamName(), pc),
        );

        this.subscribeConnections.set(command.getStreamName(), pc);
        pc.createAnswerSdpAsync(command.getSdp());
    };

    private createRTCConfiguration = (optionalServers: RTCIceServer[]) => {
        const iceServers = [...this.defaultIceServers];
        iceServers.push(...optionalServers);

        const configuration: RTCConfiguration = {
            iceServers: iceServers,
        };

        return configuration;
    };

    private handleHook = async (name: string, hook: PcHook, streamName: string, pc: OmeRTCPeerConnection) => {
        try {
            if (isAsync(hook)) {
                await hook(streamName, pc);
            } else {
                hook(streamName, pc);
            }
        } catch (e) {
            console.error(e);
        }
    };

    private subscribe = (streamName: string) => {
        if (!this.isConnected) {
            if (this.isDebug) {
                console.log("WebSocket is not connected");
            }
            return;
        }
        this.send(OmeCommand.createSubscribeOffer(streamName));
    };

    private joinMemberEvent = (command: OmeCommand) => {
        if (this.isDebug) {
            console.log(`User joined: streamName=${command.getStreamName()}`);
        }

        this.subscribe(command.getStreamName());
        this.callbacks.onUserJoined(command.getStreamName());
    };

    private leaveMemberEvent = (command: OmeCommand) => {
        if (this.isDebug) {
            console.log(`User left: streamName=${command.getStreamName()}`);
        }

        this.closeConnection(command.getStreamName());
        this.callbacks.onUserLeft(command.getStreamName());
    };

    public addPublishPcCreateHook = (hook: PcHook) => {
        this.publishPcCreateHooks.push(hook);
    };

    public addSubscribePcCreateHook = (hook: PcHook) => {
        this.subscribePcCreateHooks.push(hook);
    };

    public addPublishPcCloseHook = (hook: PcHook) => {
        this.publishPcCloseHooks.push(hook);
    };

    public addSubscribePcCloseHook = (hook: PcHook) => {
        this.subscribePcCloseHooks.push(hook);
    };
}

export { OmeWebSocket };
