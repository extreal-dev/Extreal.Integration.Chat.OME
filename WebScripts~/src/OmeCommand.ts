class OmeCommand {
    public id: number | undefined;
    public command: string | undefined;
    private roomName: string | undefined;
    public streamName: string | undefined;
    public userName: string | undefined;
    public error: string | undefined;
    public sdp: RTCSessionDescriptionInit | undefined;
    public candidates: RTCIceCandidateInit[] | undefined;
    public iceServers: RTCIceServer[] | undefined;

    public getStreamName = () => {
        return this.streamName as string;
    };

    public getSdp = () => {
        return this.sdp as RTCSessionDescription;
    };

    public getIceServers = () => {
        return this.iceServers as RTCIceServer[];
    };

    public toString = () => {
        return JSON.stringify(this);
    };

    public static createAnswerMessage = (id: number | undefined, rtcSessionDescription: RTCSessionDescriptionInit) => {
        const commandName = "answer";
        const sendMessage = new OmeCommand();
        sendMessage.command = commandName;
        sendMessage.id = id;
        sendMessage.sdp = rtcSessionDescription;
        sendMessage.candidates = [];
        return sendMessage.toString();
    };

    public static createJoinMessage = (id: number | undefined) => {
        const commandName = "join";
        const sendMessage = new OmeCommand();
        sendMessage.command = commandName;
        sendMessage.id = id;
        return sendMessage.toString();
    };

    public static createIceCandidate = (id: number | undefined, rtcIceCandidate: RTCIceCandidateInit) => {
        const commandName = "candidate";
        const sendMessage = new OmeCommand();
        sendMessage.command = commandName;
        sendMessage.id = id;
        sendMessage.candidates = [rtcIceCandidate];
        return sendMessage.toString();
    };

    public static createPublishOffer = (roomName: string, userName: string) => {
        const message = new OmeCommand();
        message.command = "publish";
        message.roomName = roomName;
        message.userName = userName;
        return message.toString();
    };

    public static createSubscribeOffer = (streamName: string) => {
        const message = new OmeCommand();
        message.command = "subscribe";
        message.streamName = streamName;
        return message.toString();
    };
}

export { OmeCommand };
