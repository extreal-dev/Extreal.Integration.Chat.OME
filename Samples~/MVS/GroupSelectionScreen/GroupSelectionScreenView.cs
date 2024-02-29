using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Extreal.Integration.Chat.OME.MVS.GroupSelectionScreen
{
    public class GroupSelectionScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown joinMethodDropdown;
        [SerializeField] private TMP_InputField groupNameInputField;
        [SerializeField] private TMP_Dropdown groupDropdown;
        [SerializeField] private Button updateButton;
        [SerializeField] private Button goButton;

        public IObservable<JoinMethod> OnJoinMethodChanged =>
            joinMethodDropdown.onValueChanged.AsObservable()
                .Select(index => JoinMethods[index]).TakeUntilDestroy(this);

        public IObservable<string> OnGroupNameChanged => onGroupNameChanged.TakeUntilDestroy(this);
        [SuppressMessage("Usage", "CC0033")]
        private readonly Subject<string> onGroupNameChanged = new Subject<string>();

        public IObservable<Unit> OnUpdateButtonClicked => updateButton.OnClickAsObservable().TakeUntilDestroy(this);

        public IObservable<Unit> OnGoButtonClicked => onGoButtonClicked.TakeUntilDestroy(this);
        [SuppressMessage("Usage", "CC0033")]
        private readonly Subject<Unit> onGoButtonClicked = new Subject<Unit>();


        private static readonly List<JoinMethod> JoinMethods = new List<JoinMethod> { JoinMethod.Create, JoinMethod.Join };
        private readonly List<string> groupNames = new List<string>();

        public void Initialize()
        {
            joinMethodDropdown.options = JoinMethods.Select(joinMethod => new TMP_Dropdown.OptionData(joinMethod.ToString())).ToList();
            groupDropdown.options = new List<TMP_Dropdown.OptionData>();

            groupNameInputField.onEndEdit.AsObservable()
                .Subscribe(_ => CanGo(JoinMethod.Create))
                .AddTo(this);

            groupDropdown.onValueChanged.AsObservable()
                .Select(index => groupNames[index])
                .Subscribe(_ => CanGo(JoinMethod.Join))
                .AddTo(this);

            OnJoinMethodChanged
                .Subscribe(SwitchInputMode)
                .AddTo(this);

            goButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    if (JoinMethods[joinMethodDropdown.value] == JoinMethod.Create)
                    {
                        onGroupNameChanged.OnNext(groupNameInputField.text);
                    }
                    else
                    {
                        onGroupNameChanged.OnNext(groupNames[groupDropdown.value]);
                    }
                    onGoButtonClicked.OnNext(Unit.Default);
                })
                .AddTo(this);
        }

        private void SwitchInputMode(JoinMethod joinMethod)
        {
            groupNameInputField.gameObject.SetActive(joinMethod == JoinMethod.Create);
            groupDropdown.gameObject.SetActive(joinMethod == JoinMethod.Join);
            updateButton.gameObject.SetActive(joinMethod == JoinMethod.Join);
            CanGo(joinMethod);
        }

        private void CanGo(JoinMethod joinMethod) =>
            goButton.gameObject.SetActive(
                (joinMethod == JoinMethod.Create && groupNameInputField.text.Length > 0)
                || (joinMethod == JoinMethod.Join && groupDropdown.options.Count > 0));

        public void SetInitialValues(JoinMethod joinMethod)
        {
            joinMethodDropdown.value = JoinMethods.IndexOf(joinMethod);
            groupNameInputField.text = string.Empty;
            groupDropdown.value = 0;
            SwitchInputMode(joinMethod);
        }

        public void UpdateGroupNames(string[] groupNames)
        {
            this.groupNames.Clear();
            this.groupNames.AddRange(groupNames);
            groupDropdown.options
                = this.groupNames.Select(groupName => new TMP_Dropdown.OptionData(groupName)).ToList();
            groupDropdown.value = 0;
            CanGo(JoinMethod.Join);
        }

        [SuppressMessage("Usage", "CC0068")]
        private void OnDestroy()
        {
            onGroupNameChanged.Dispose();
            onGoButtonClicked.Dispose();
        }
    }
}
