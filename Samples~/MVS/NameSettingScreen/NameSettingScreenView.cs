using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Extreal.Integration.Chat.OME.MVS.NameSettingScreen
{
    public class NameSettingScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button goButton;

        public IObservable<string> OnNameChanged =>
            nameInputField.onEndEdit.AsObservable().TakeUntilDestroy(this);

        public IObservable<Unit> OnGoButtonClicked => goButton.OnClickAsObservable().TakeUntilDestroy(this);

        public void Initialize(string name)
        {
            SetName(name);
            OnNameChanged.Subscribe(_ => CanGo());
        }

        private void CanGo() => goButton.gameObject.SetActive(nameInputField.text.Length > 0);

        public void SetName(string name)
        {
            nameInputField.text = name;
            CanGo();
        }
    }
}
