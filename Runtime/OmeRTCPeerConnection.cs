using Unity.WebRTC;
using Cysharp.Threading.Tasks;
using Extreal.Core.Logging;

namespace Extreal.Integration.VoiceChat
{
    public class OmeRTCPeerConnection : RTCPeerConnection
    {
        public delegate void DelegateOnCreateAnswerCompletion(RTCSessionDescription answerSDP);
        private DelegateOnCreateAnswerCompletion onCreateAnswerCompletion;

        public delegate void DelegateOnIceCandidateCallback(RTCIceCandidate candidate);
        private DelegateOnIceCandidateCallback onIceCandidateCallBack;

        public delegate void DelegateOnConnected();
        private DelegateOnConnected onConnected;

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(OmeRTCPeerConnection));

        public OmeRTCPeerConnection(ref RTCConfiguration config) : base(ref config)
        {
            OnIceCandidate += OnIceCandidateEvent;
            OnConnectionStateChange += OnConnectionStateChangeEvent;
        }

        public void SetCreateAnswerCompletion(DelegateOnCreateAnswerCompletion callback)
            => onCreateAnswerCompletion = callback;

        public void SetIceCandidateCallback(DelegateOnIceCandidateCallback callback)
            => onIceCandidateCallBack = callback;

        public void SetConnectedCallback(DelegateOnConnected callback)
            => onConnected = callback;

        public async UniTask CreateAnswerSdpAsync(RTCSessionDescription offerSdp)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"SetRemoteDescription[{offerSdp.type}]: {offerSdp.sdp}");
            }

            await SetRemoteDescription(ref offerSdp);
            var op = CreateAnswer();
            await op;
            if (op.IsError)
            {
                if (Logger.IsError())
                {
                    Logger.LogError($"CreateAnswer failure {op.Error.message}");
                }
                return;
            }

            var answerSDP = op.Desc;
            await SetLocalDescription(ref answerSDP);
            onCreateAnswerCompletion?.Invoke(answerSDP);
        }

        private void OnIceCandidateEvent(RTCIceCandidate candidate)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnIceCandidate: {candidate.Candidate}");
            }

            onIceCandidateCallBack?.Invoke(candidate);
        }

        private void OnConnectionStateChangeEvent(RTCPeerConnectionState state)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnConnectionStateChange: {state}");
            }

            if (state == RTCPeerConnectionState.Connected)
            {
                onConnected?.Invoke();
            }
        }
    }
}
