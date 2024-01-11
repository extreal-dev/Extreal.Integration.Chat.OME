import express from "express";
import cors from "cors";
import http from "http";
import https from "https";
import fs from "fs";
import bodyParser from "body-parser";
import WebSocket, { WebSocketServer } from "ws";
import OMEWebSocket, { OMECommand } from "./OMEWebSocket";
import { v4 as uuidv4 } from "uuid";

const app = express();
app.use(cors());
app.use(bodyParser.urlencoded({ extended: true }));
app.use(bodyParser.json());

let server;
// HTTPSを使用するかどうかを環境変数で判定
if (process.env.USE_HTTPS === "true") {
    // SSL証明書と秘密鍵を読み込む
    const privateKey = fs.readFileSync("./keys/privkey.pem", "utf8");
    const certificate = fs.readFileSync("./keys/fullchain.pem", "utf8");

    const credentials = { key: privateKey, cert: certificate };
    server = https.createServer(credentials, app);
} else {
    server = http.createServer(app);
}
const wss = new WebSocketServer({ server });

// const appSrvUrl = "wss://sfu.dev.comet.ninja:3334";
const appSrvUrl = "ws://localhost:3334";

// roomMembers: ルーム名(key)に所属しているメンバであるユーザ(streamName)の配列
// key: roomName, value: streamName
const roomMembers: Map<string, Set<string>> = new Map<string, Set<string>>();
// userSocketMap: ユーザリスト．各ユーザが接続しているWebSocketの辞書
// key: streamName, value: WebSocket
const userSocketMap: Map<string, WebSocket> = new Map<string, WebSocket>();
// streamMap: ユーザ名とストリーム名のマップ
// key: streamName, value: userName
const streamMap: Map<string, string> = new Map<string, string>();

wss.on("connection", (rootWs: WebSocket) => {
    let roomName = "";
    let streamName = "";
    let userName = "";

    // 他メンバのストリームを受信するためのWebSocket
    const subscribeWebSockets = new Map<string, OMEWebSocket>();
    console.log("connected");

    rootWs.on("message", (message) => {
        let msg: OMECommand;
        try {
            msg = JSON.parse(message.toString());
        } catch (e) {
            console.log("JSON parse error: %o", e);
            return;
        }
        console.log("rootWS onMessage: %o", msg);
        if (!msg.command) {
            console.error("command is not found:%o", msg);
            return;
        }
        const cmd = msg.command;
        switch (cmd) {
            case "publish": {
                if (!msg.roomName || !msg.userName) {
                    console.error("roomName or userName is not found:%o", msg);
                    return;
                }
                userName = msg.userName;
                streamName = uuidv4();
                roomName = msg.roomName;
                console.log("publish: Room:%o, userName:%o, stream: %o", roomName, userName, streamName);
                const publishWS = new OMEWebSocket(`${appSrvUrl}/app/${streamName}?direction=send`);
                publishWS.onMessageCallback = (command: OMECommand) => {
                    command.command = "publishOffer";
                    command.streamName = streamName;
                    command.userName = userName;
                    rootWs.send(JSON.stringify(command));

                    // ユーザのWebSocketを保存
                    userSocketMap.set(streamName, rootWs);
                    // IDとストリーム名のマップを保存
                    streamMap.set(streamName, userName);
                    // ストリーム名とWebSocketのマップを保存
                    subscribeWebSockets.set(command.id as string, publishWS);
                };
                publishWS.onopen = () => {
                    publishWS.send(JSON.stringify({ command: "request_offer" }));
                };
                break;
            }
            case "subscribe": {
                if (!msg.streamName) {
                    console.error("streamName is not found:%o", msg);
                    return;
                }
                console.log("subscribe:%s", msg.streamName);
                const subscribeWS = new OMEWebSocket(`${appSrvUrl}/app/${msg.streamName}`);
                subscribeWS.onMessageCallback = (command: OMECommand) => {
                    console.log("subscribeWS onMessage[%s]: %s", command.id, command.command);
                    command.command = "subscribeOffer";
                    command.streamName = msg.streamName;
                    if (msg.streamName) {
                        command.userName = streamMap.get(msg.streamName) as string;
                    }

                    if (command.code && command.code === 404 && command.error === "Cannot create offer") {
                        subscribeWS.close();
                        rootWs.send(JSON.stringify(command));
                        return;
                    }
                    rootWs.send(JSON.stringify(command));
                    // ストリーム名とWebSocketのマップを保存
                    subscribeWebSockets.set(command.id as string, subscribeWS);
                };
                subscribeWS.onopen = () => {
                    subscribeWS.send(JSON.stringify({ command: "request_offer" }));
                };
                subscribeWS.onclose = () => {
                    console.log("subscribeWS[%s] closed", msg.streamName);
                };
                break;
            }
            case "join": {
                if (!roomName || !userName) {
                    console.error("roomName or userName is not found:%o", msg);
                    return;
                }
                // ルームに既に入室しているメンバに対して、新規メンバの入室を通知
                const members = roomMembers.get(roomName);
                if (members) {
                    members.forEach((member) => {
                        const userWs = userSocketMap.get(member);
                        if (userWs) {
                            userWs.send(JSON.stringify({ command: "join", streamName: streamName }));
                        }
                        // 自分に対して，既存メンバの情報を通知
                        rootWs.send(JSON.stringify({ command: "join", streamName: member }));
                    });
                }
                // ルームに加入
                const roomSet = roomMembers.get(roomName);
                if (roomSet) {
                    roomSet.add(streamName);
                } else {
                    // 新規のルームであれば新規作成
                    roomMembers.set(roomName, new Set<string>([streamName]));
                }
                break;
            }
            case "leave": {
                break;
            }
            case "answer":
            case "candidate": {
                if (!msg.id) {
                    console.error("id is not found:%o", msg);
                    return;
                }
                const ws = subscribeWebSockets.get(msg.id);
                if (!ws) {
                    console.error("websocket is not found:%o", msg);
                    return;
                }
                console.log("send message to subscribeWS[%s]:%o", msg.id, msg);
                ws.send(message);
                break;
            }
            default:
                console.error("unknown command:%o", cmd);
                break;
        }
    });

    rootWs.on("close", () => {
        const members = roomMembers.get(roomName);
        if (members) {
            // ルームメンバから自分を削除
            members.delete(streamName);
            // ルームメンバに退出を通知
            members.forEach((member) => {
                const userWs = userSocketMap.get(member);
                if (userWs) {
                    userWs.send(
                        JSON.stringify({
                            command: "leave",
                            streamName: streamName,
                            userName: streamMap.get(streamName),
                        }),
                    );
                }
            });
            // セットが空になった場合、ルーム名も削除
            if (members.size === 0) {
                roomMembers.delete(roomName);
            }
        }
        // ユーザリストの辞書から削除
        userSocketMap.delete(streamName);
        // ユーザ名を辞書から削除
        streamMap.delete(streamName);
        // 残りの自分が送受信しているWebSocketを全て閉じる
        subscribeWebSockets.forEach((ws) => {
            ws.close();
        });
        console.log("closed");
    });
});

const port = process.env.PORT || 3000;
server.listen(port, () => {
    console.log(`Server is running on port ${port}`);
});
