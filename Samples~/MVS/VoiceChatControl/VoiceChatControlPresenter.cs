using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Extreal.Core.Common.System;
using Extreal.Integration.Chat.OME.MVS.App;
using Extreal.Integration.SFU.OME;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.Controls.VoiceChatControl
{
    public class VoiceChatControlPresenter : DisposableBase, IInitializable
    {
        private readonly AppState appState;
        private readonly VoiceChatClient voiceChatClient;
        private readonly VoiceChatControlView voiceChatControlView;
        private readonly VoiceChatConfig voiceChatConfig;
        private readonly OmeClient omeClient;
        private readonly Dictionary<string, float> audioLevels = new Dictionary<string, float>();

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public VoiceChatControlPresenter
        (
            AppState appState,
            VoiceChatClient voiceChatClient,
            VoiceChatControlView voiceChatControlView,
            VoiceChatConfig voiceChatConfig,
            OmeClient omeClient
        )
        {
            this.appState = appState;
            this.voiceChatClient = voiceChatClient;
            this.voiceChatControlView = voiceChatControlView;
            this.voiceChatConfig = voiceChatConfig;
            this.omeClient = omeClient;
        }

        public void Initialize()
        {
            voiceChatControlView.OnMuteButtonClicked
                .Subscribe(_ => voiceChatClient.ToggleMute())
                .AddTo(disposables);

            voiceChatClient.OnMuted
                .Subscribe(voiceChatControlView.ToggleMute)
                .AddTo(disposables);

            voiceChatControlView.OnMicVolumeSliderChanged
                .Subscribe(volume =>
                {
                    voiceChatClient.SetInVolume(volume);
                    voiceChatControlView.SetMicVolumeText(volume);
                })
                .AddTo(disposables);

            voiceChatControlView.OnSpeakersVolumeSliderChanged
                .Subscribe(volume =>
                {
                    voiceChatClient.SetOutVolume(volume);
                    voiceChatControlView.SetSpeakersVolumeText(volume);
                })
                .AddTo(disposables);

            voiceChatClient.OnAudioLevelChanged
                .Subscribe(values =>
                {
                    audioLevels[values.id] = values.audioLevel;
                    UpdateAudioLevelText();
                })
                .AddTo(disposables);

            appState.NameDict.ObserveCountChanged()
                .Subscribe(_ => UpdateAudioLevelText())
                .AddTo(disposables);

            omeClient.OnUserLeft
                .Subscribe(id => audioLevels.Remove(id))
                .AddTo(disposables);

            voiceChatControlView.Initialize(voiceChatConfig.InitialMute);
        }

        private void UpdateAudioLevelText()
        {
            var stringBuilder = new StringBuilder();
            foreach (var id in audioLevels.Keys)
            {
                // if (appState.NameDict.TryGetValue(id, out var name))
                // {
                //     stringBuilder.Append($"{name}: {audioLevels[id]:f3}{System.Environment.NewLine}");
                // }
                stringBuilder.Append($"{id[..8]}: {audioLevels[id]:f3}{System.Environment.NewLine}");
            }
            voiceChatControlView.SetAudioLevelsText(stringBuilder.ToString());
        }

        protected override void ReleaseManagedResources()
        {
            voiceChatClient.Clear();
            disposables.Dispose();
        }
    }
}
