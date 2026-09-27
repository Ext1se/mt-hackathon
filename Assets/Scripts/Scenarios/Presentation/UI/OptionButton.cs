using System;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>One answer button in the dialogue panel.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class OptionButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;

        private string _optionId = string.Empty;
        private Action<string> _clicked;

        private void Awake()
        {
            _button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnClicked);
        }

        private void Reset()
        {
            _button = GetComponent<Button>();
            _label = GetComponentInChildren<TMP_Text>();
        }

        public void Bind(OptionView option, Action<string> clicked)
        {
            _optionId = option.Id;
            _label.text = option.Text;
            _clicked = clicked;
        }

        /// <summary>Replaces the label with a formatted version of the option text.</summary>
        public void SetLabel(string text)
        {
            _label.text = text;
        }

        private void OnClicked()
        {
            _clicked?.Invoke(_optionId);
        }
    }
}
