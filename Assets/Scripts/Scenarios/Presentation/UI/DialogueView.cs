using System;
using System.Collections.Generic;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>Bottom dialogue panel: speaker, line, answer buttons, hub counter, hint and continue buttons.</summary>
    public sealed class DialogueView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _speaker;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _optionsContainer;
        [SerializeField] private OptionButton _optionPrefab;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _hintButton;
        [SerializeField] private TMP_Text _hubActions;
        [Tooltip("Format of the remaining hub actions; {0} is the number. The words live here, not in code.")]
        [SerializeField] private string _hubActionsFormat = "{0}";

        private readonly List<OptionButton> _buttons = new List<OptionButton>();

        public event Action<string> OptionChosen;
        public event Action ContinueRequested;
        public event Action HintRequested;

        private void Awake()
        {
            _continueButton.onClick.AddListener(OnContinueClicked);
            _hintButton.onClick.AddListener(OnHintClicked);
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            _continueButton.onClick.RemoveListener(OnContinueClicked);
            _hintButton.onClick.RemoveListener(OnHintClicked);
        }

        public void ShowNode(string speaker, NodeView view)
        {
            _root.SetActive(true);
            _speaker.text = speaker;
            _text.text = view.Text;
            _continueButton.gameObject.SetActive(false);
            _hintButton.gameObject.SetActive(view.Node.Hints.Count > 0);
            _hubActions.gameObject.SetActive(view.IsHub);
            if (view.IsHub)
            {
                _hubActions.text = string.Format(_hubActionsFormat, view.HubActionsLeft);
            }

            ShowOptions(view.Options);
        }

        public void ShowResponse(string speaker, string text)
        {
            _root.SetActive(true);
            _speaker.text = speaker;
            _text.text = text;
            _hintButton.gameObject.SetActive(false);
            _hubActions.gameObject.SetActive(false);
            ShowOptions(Array.Empty<OptionView>());
            _continueButton.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        private void ShowOptions(IReadOnlyList<OptionView> options)
        {
            while (_buttons.Count < options.Count)
            {
                _buttons.Add(Instantiate(_optionPrefab, _optionsContainer));
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool isUsed = i < options.Count;
                _buttons[i].gameObject.SetActive(isUsed);
                if (isUsed)
                {
                    _buttons[i].Bind(options[i], OnOptionClicked);
                }
            }
        }

        private void OnOptionClicked(string optionId)
        {
            OptionChosen?.Invoke(optionId);
        }

        private void OnContinueClicked()
        {
            ContinueRequested?.Invoke();
        }

        private void OnHintClicked()
        {
            HintRequested?.Invoke();
        }
    }
}
