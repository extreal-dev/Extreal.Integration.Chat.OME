using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Extreal.Integration.Chat.OME.MVS.Controls.VoiceChatControl
{
    public class VoiceChatControlView : MonoBehaviour
    {
        [SerializeField] private Button muteButton;
        [SerializeField] private TMP_Text mutedString;
        [SerializeField] private TMP_Text micVolumeText;
        [SerializeField] private Slider micVolumeSlider;
        [SerializeField] private TMP_Text speakersVolumeText;
        [SerializeField] private Slider speakersVolumeSlider;
        [SerializeField] private TMP_Text audioLevelsText;

        public IObservable<Unit> OnMuteButtonClicked
            => muteButton.OnClickAsObservable().TakeUntilDestroy(this);

        public IObservable<float> OnMicVolumeSliderChanged
            => micVolumeSlider.OnValueChangedAsObservable().TakeUntilDestroy(this);

        public IObservable<float> OnSpeakersVolumeSliderChanged
            => speakersVolumeSlider.OnValueChangedAsObservable().TakeUntilDestroy(this);

        private string muteOffButtonLabel;
        private string muteOnButtonLabel;

        public void Initialize(bool initialMute)
        {
            muteOffButtonLabel = "OFF";
            muteOnButtonLabel = "ON";
            ToggleMute(initialMute);
            SetMicVolumeText(micVolumeSlider.value);
            SetSpeakersVolumeText(speakersVolumeSlider.value);
        }

        public void ToggleMute(bool isMute)
            => mutedString.text = isMute ? muteOffButtonLabel : muteOnButtonLabel;

        public void SetMicVolumeText(float volume)
            => micVolumeText.text = $"Mic volume: {volume:f2}";

        public void SetSpeakersVolumeText(float volume)
            => speakersVolumeText.text = $"Speakers volume: {volume:f2}";

        public void SetAudioLevelsText(string audioLevels)
            => audioLevelsText.text = $"Audio level:{Environment.NewLine}{audioLevels}";
    }
}
