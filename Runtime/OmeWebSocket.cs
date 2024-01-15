#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NativeWebSocket;
using Unity.WebRTC;
using Cysharp.Threading.Tasks;
using Extreal.Core.Logging;
using Extreal.Core.Common.System;
using UniRx;
using System.Diagnostics.CodeAnalysis;

namespace Extreal.Integration.Chat.OME
{
    public class OmeWebSocket : WebSocket, IDisposable
    {
        public IObservable<string> OnJoined => onJoined;
        private readonly Subject<string> onJoined;

        public IObservable<string> OnLeft => onLeft;
        private readonly Subject<string> onLeft;

        public IObservable<string> OnUserJoined => onUserJoined;
        private readonly Subject<string> onUserJoined;

        public IObservable<string> OnUserLeft => onUserLeft;
        private readonly Subject<string> onUserLeft;

        private bool isConnected;
        private string roomName;
        private readonly List<RTCIceServer> defaultIceServers;
        private readonly string userName;
        private string localStreamName;

        private readonly List<Action<string, OmeRTCPeerConnection>> publishPcCreateHooks = new List<Action<string, OmeRTCPeerConnection>>();
        private readonly List<Action<string, OmeRTCPeerConnection>> subscribePcCreateHooks = new List<Action<string, OmeRTCPeerConnection>>();
        private readonly List<Action<string, OmeRTCPeerConnection>> publishPcCloseHooks = new List<Action<string, OmeRTCPeerConnection>>();
        private readonly List<Action<string, OmeRTCPeerConnection>> subscribePcCloseHooks = new List<Action<string, OmeRTCPeerConnection>>();

        private OmeRTCPeerConnection publishConnection;
        private readonly Dictionary<string, OmeRTCPeerConnection> subscribeConnections = new Dictionary<string, OmeRTCPeerConnection>();

        private readonly Dictionary<string, int> subscribeRetryCounts = new Dictionary<string, int>();
        private const int MaxSubscribeRetries = 20;
        private const float SubscribeRetryInterval = 0.5f;

        private readonly SafeDisposer safeDisposer;
        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(OmeWebSocket));

        [SuppressMessage("Usage", "CC0022")]
        public OmeWebSocket(string url, List<RTCIceServer> defaultIceServers, string userName) : base(url)
        {
            safeDisposer = new SafeDisposer(this, ReleaseManagedResources);

            onJoined = new Subject<string>().AddTo(disposables);
            onLeft = new Subject<string>().AddTo(disposables);
            onUserJoined = new Subject<string>().AddTo(disposables);
            onUserLeft = new Subject<string>().AddTo(disposables);

            OnOpen += OnOpenEvent;
            OnClose += OnCloseEvent;
            OnError += OnErrorEvent;
            OnMessage += OnMessageEvent;

            this.userName = userName;
            this.defaultIceServers = defaultIceServers;

#if !UNITY_WEBGL || UNITY_EDITOR
            Observable.EveryUpdate()
                .Subscribe(_ => DispatchMessageQueue())
                .AddTo(disposables);
#endif
        }

        ~OmeWebSocket()
            => safeDisposer.DisposeByFinalizer();

        public void Dispose()
            => safeDisposer.Dispose();

        private void ReleaseManagedResources()
        {
            OnOpen -= OnOpenEvent;
            OnClose -= OnCloseEvent;
            OnError -= OnErrorEvent;
            OnMessage -= OnMessageEvent;

            CloseAllRTCConnections(WebSocketCloseCode.Normal.ToString());
            UniTask.Void(async () => await Close());
            disposables.Dispose();
        }

        private void OnOpenEvent()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug("OnOpen");
            }

            isConnected = true;
            Publish(roomName, userName);
        }

        private void Publish(string roomName, string userName)
        {
            if (!isConnected)
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug("WebSocket is not connected.");
                }
                return;
            }
            Send(OmeCommand.CreatePublishOffer(roomName, userName));
        }

        private void OnCloseEvent(WebSocketCloseCode closeCode)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnClose: CloseCode=${closeCode}");
            }

            isConnected = false;
            CloseAllRTCConnections(closeCode.ToString());
        }

        private void OnErrorEvent(string e) => UniTask.Void(async () =>
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnError: {e}");
            }

            CloseAllRTCConnections(e);
            await Close();
        });

        private void CloseAllRTCConnections(string reason)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Left Room: roomName={roomName}");
            }

            if (publishConnection != null)
            {
                publishPcCloseHooks.ForEach(hook => HandleHook(nameof(CloseAllRTCConnections), () => hook.Invoke(localStreamName, publishConnection)));
                publishConnection.Close();
                publishConnection = null;
                localStreamName = null;
            }

            subscribeConnections.Keys.ToList().ForEach(CloseConnection);
            subscribeConnections.Clear();

            onLeft.OnNext(reason);
        }

        private void CloseConnection(string streamName)
        {
            if (subscribeConnections.TryGetValue(streamName, out var connection))
            {
                subscribePcCloseHooks.ForEach(hook => HandleHook(nameof(CloseConnection), () => hook.Invoke(streamName, connection)));
                connection.Close();
                subscribeConnections.Remove(streamName);
            }
        }

        private void OnMessageEvent(byte[] bytes)
        {
            var command = OmeCommand.FromJsonBytes(bytes);
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnMessage: {command.ToJson()}");
            }

            if (command.Command == "publishOffer")
            {
                PublishOfferEvent(command);
            }
            else if (command.Command == "subscribeOffer")
            {
                SubscribeOfferEventAsync(command);
            }
            else if (command.Command == "join")
            {
                JoinMemberEvent(command);
            }
            else if (command.Command == "leave")
            {
                LeaveMemberEvent(command);
            }
        }

        private void PublishOfferEvent(OmeCommand command)
        {
            var isSetLocalCandidate = false;

            var configuration = CreateRTCConfiguration(command.GetIceServers());
            var pc = new OmeRTCPeerConnection(ref configuration);
            pc.SetCreateAnswerCompletion(answer => UniTask.Void(async () =>
            {
                var messageBytes = OmeCommand.CreateAnswerMessage(command.Id, answer);
                await Send(messageBytes);
            }));
            pc.SetIceCandidateCallback(candidate => UniTask.Void(async () =>
            {
                if (!isSetLocalCandidate)
                {
                    var sdpMid = candidate.SdpMid;
                    foreach (var c in command.Candidates)
                    {
                        var candidateInit = c.RtcIceCandidateInit;
                        candidateInit.sdpMid = sdpMid;
                        var rtcIceCandidate = new RTCIceCandidate(candidateInit);
                        pc.AddIceCandidate(rtcIceCandidate);
                    }
                    isSetLocalCandidate = true;
                }
                var messageBytes = OmeCommand.CreateIceCandidate(command.Id, candidate);
                await Send(messageBytes);
            }));
            pc.SetConnectedCallback(() => UniTask.Void(async () =>
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug($"Joined Room: roomName={roomName}");
                }

                await Send(OmeCommand.CreateJoinMessage(command.Id));
                onJoined.OnNext(command.StreamName);
            }));

            publishPcCreateHooks.ForEach(hook => HandleHook(nameof(PublishOfferEvent), () => hook.Invoke(command.StreamName, pc)));

            localStreamName = command.StreamName;
            publishConnection = pc;
            pc.CreateAnswerSdpAsync(command.GetSdp()).Forget();
        }

        private async void SubscribeOfferEventAsync(OmeCommand command)
        {
            var currentRetryCount = subscribeRetryCounts.ContainsKey(command.StreamName) ? subscribeRetryCounts[command.StreamName] : 0;

            if (!string.IsNullOrEmpty(command.Error))
            {
                if (command.Error == "Cannot create offer")
                {
                    if (currentRetryCount < MaxSubscribeRetries)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(SubscribeRetryInterval));
                        Subscribe(command.StreamName);
                        subscribeRetryCounts[command.StreamName] = currentRetryCount + 1;
                    }
                    else
                    {
                        if (Logger.IsError())
                        {
                            Logger.LogError($"Maximum retryCount reached: {command.StreamName}");
                        }

                        subscribeRetryCounts.Remove(command.StreamName);
                    }
                }
                else
                {
                    if (Logger.IsError())
                    {
                        Logger.LogError($"Subscribe error: {command.Error}");
                    }

                    subscribeRetryCounts.Remove(command.StreamName);
                }
                return;
            }
            else
            {
                // エラーではないが，SDPがない場合は何もしない
                if (Logger.IsDebug())
                {
                    Logger.LogDebug($"SubscribeOfferEvent: Id={command.Id}, Sdp={command.Sdp.RtcSessionDescription.sdp}");
                }

                if (command.Id == 0)
                {
                    return;
                }
            }
            subscribeRetryCounts.Remove(command.StreamName);

            var isSetLocalCandidate = false;

            var configuration = CreateRTCConfiguration(command.GetIceServers());
            var pc = new OmeRTCPeerConnection(ref configuration);
            pc.SetCreateAnswerCompletion(answer => UniTask.Void(async () =>
            {
                var messageBytes = OmeCommand.CreateAnswerMessage(command.Id, answer);
                await Send(messageBytes);
            }));
            pc.SetIceCandidateCallback(candidate => UniTask.Void(async () =>
            {
                if (!isSetLocalCandidate)
                {
                    var sdpMid = candidate.SdpMid;
                    foreach (var c in command.Candidates)
                    {
                        var candidateInit = c.RtcIceCandidateInit;
                        candidateInit.sdpMid = sdpMid;
                        var rtcIceCandidate = new RTCIceCandidate(candidateInit);
                        pc.AddIceCandidate(rtcIceCandidate);
                    }
                    isSetLocalCandidate = true;
                }
                var msgBytes = OmeCommand.CreateIceCandidate(command.Id, candidate);
                await Send(msgBytes);
            }));

            subscribePcCreateHooks.ForEach(hook => HandleHook(nameof(SubscribeOfferEventAsync), () => hook.Invoke(command.StreamName, pc)));

            subscribeConnections[command.StreamName] = pc;
            pc.CreateAnswerSdpAsync(command.GetSdp()).Forget();
        }

        private RTCConfiguration CreateRTCConfiguration(List<RTCIceServer> optionalIceServers)
        {
            var iceServers = optionalIceServers;
            iceServers.InsertRange(0, defaultIceServers);

            var configuration = new RTCConfiguration
            {
                iceServers = iceServers.ToArray()
            };

            return configuration;
        }

        private static void HandleHook(string name, Action hook)
        {
            try
            {
                hook.Invoke();
            }
            catch (Exception e)
            {
                Logger.LogError($"Error has occurred at {name}", e);
            }
        }

        private void Subscribe(string streamName)
        {
            if (!isConnected)
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug("WebSocket is not connected.");
                }
                return;
            }
            Send(OmeCommand.CreateSubscribeOffer(streamName));
        }

        private void JoinMemberEvent(OmeCommand command)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Join[{command.StreamName}]: {command.UserName}");
            }

            Subscribe(command.StreamName);
            onUserJoined.OnNext(command.StreamName);
        }

        private void LeaveMemberEvent(OmeCommand command)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Leave[{command.StreamName}]: {command.UserName}");
            }

            CloseConnection(command.StreamName);
            onUserLeft.OnNext(command.StreamName);
        }

        public void AddPublishPcCreateHook(Action<string, OmeRTCPeerConnection> hook)
            => publishPcCreateHooks.Add(hook);

        public void AddSubscribePcCreateHook(Action<string, OmeRTCPeerConnection> hook)
            => subscribePcCreateHooks.Add(hook);

        public void AddPublishPcCloseHook(Action<string, OmeRTCPeerConnection> hook)
            => publishPcCloseHooks.Add(hook);

        public void AddSubscribePcCloseHook(Action<string, OmeRTCPeerConnection> hook)
            => subscribePcCloseHooks.Add(hook);

        public async UniTask ConnectAsync(string roomName)
        {
            this.roomName = roomName;
            await Connect();
        }
    }
}
#endif
