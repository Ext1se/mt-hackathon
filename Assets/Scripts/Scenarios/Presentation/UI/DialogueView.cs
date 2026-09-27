using System;
using System.Collections.Generic;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Bottom dialogue panel: speaker, line, answer buttons, hub counter, hint and continue buttons. The line is typed
    /// out character by character; answers and the continue button unlock once it is complete. A click, Space or Enter
    /// while typing shows the whole line at once.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _speaker;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _optionsContainer;
        [Tooltip("On the options list: hidden and locked while the line is being typed, so the panel keeps its size.")]
        [SerializeField] private CanvasGroup _optionsGroup;
        [SerializeField] private OptionButton _optionPrefab;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _hintButton;
        [SerializeField] private TMP_Text _hubActions;
        [Tooltip("Format of the remaining hub actions; {0} is the number. The words live here, not in code.")]
        [SerializeField] private string _hubActionsFormat = "{0}";

        [Header("Typewriter")]
        [Tooltip("Characters per second; 0 shows the line at once.")]
        [SerializeField, Range(0f, 200f)] private float _charactersPerSecond = 45f;
        [Tooltip("Extra pause after . ! ? and an ellipsis, in seconds.")]
        [SerializeField, Range(0f, 1f)] private float _sentencePause = 0.25f;
        [Tooltip("Extra pause after , ; and :, in seconds.")]
        [SerializeField, Range(0f, 0.5f)] private float _commaPause = 0.08f;
        [Tooltip("A click, Space or Enter while typing shows the whole line.")]
        [SerializeField] private bool _skipOnInput = true;
        [Tooltip("Fade-in of the answers after the line is typed, in seconds.")]
        [SerializeField, Range(0f, 1f)] private float _optionsFadeSeconds = 0.2f;

        [Header("Actions")]
        [Tooltip("Colour of actions and descriptions (italics in the scenario texts) in lines and answers, "
            + "so they stand apart from speech.")]
        [SerializeField] private Color _actionColor = new Color(0.96f, 0.80f, 0.45f, 1f);
        [Tooltip("Colour of answers already chosen in a hub menu: they stay listed so the player can hear them again.")]
        [SerializeField] private Color _seenColor = new Color(0.55f, 0.58f, 0.63f, 1f);

        private readonly List<OptionButton> _buttons = new List<OptionButton>();
        private int _visibleCharacters;
        private int _totalCharacters;
        private float _nextCharacterIn;
        private bool _isTyping;
        private bool _isResponse;

        public event Action<string> OptionChosen;
        public event Action ContinueRequested;
        public event Action HintRequested;

        /// <summary>Raised when the whole line is on screen and the answers are unlocked.</summary>
        public event Action TextRevealed;

        public bool IsTyping => _isTyping;

        private void Awake()
        {
            _continueButton.onClick.AddListener(OnContinueClicked);
            _hintButton.onClick.AddListener(OnHintClicked);
            _root.SetActive(false);
        }

        private void Update()
        {
            if (_isTyping)
            {
                UpdateTyping();
            }
            else if (_optionsGroup != null && _optionsGroup.alpha < 1f)
            {
                float step = _optionsFadeSeconds > 0f ? Time.unscaledDeltaTime / _optionsFadeSeconds : 1f;
                _optionsGroup.alpha = Mathf.MoveTowards(_optionsGroup.alpha, 1f, step);
            }
        }

        private void OnDestroy()
        {
            _continueButton.onClick.RemoveListener(OnContinueClicked);
            _hintButton.onClick.RemoveListener(OnHintClicked);
        }

        public void ShowNode(string speaker, NodeView view)
        {
            _root.SetActive(true);
            _isResponse = false;
            _speaker.text = speaker;
            _continueButton.gameObject.SetActive(false);
            _hintButton.gameObject.SetActive(view.Node.Hints.Count > 0);
            _hubActions.gameObject.SetActive(view.IsHub);
            if (view.IsHub)
            {
                _hubActions.text = string.Format(_hubActionsFormat, view.HubActionsLeft);
            }

            ShowOptions(view.Options);
            StartTyping(view.Text);
        }

        /// <summary>
        /// Shows the hint button on a node line while the node has a hint to show or repeat; never on a response.
        /// </summary>
        public void SetHintAvailable(bool isAvailable)
        {
            _hintButton.gameObject.SetActive(isAvailable && !_isResponse && _root.activeSelf);
        }

        public void ShowResponse(string speaker, string text)
        {
            _root.SetActive(true);
            _isResponse = true;
            _speaker.text = speaker;
            _hintButton.gameObject.SetActive(false);
            _hubActions.gameObject.SetActive(false);
            _continueButton.gameObject.SetActive(false);
            ShowOptions(Array.Empty<OptionView>());
            StartTyping(text);
        }

        public void Hide()
        {
            _isTyping = false;
            _root.SetActive(false);
        }

        private static bool IsSkipPressed()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            Touchscreen touch = Touchscreen.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                || (touch != null && touch.primaryTouch.press.wasPressedThisFrame);
        }

        // A button keeps the "selected" tint after a click until something else is selected; nothing should be.
        private static void Deselect()
        {
            EventSystem current = EventSystem.current;
            if (current != null)
            {
                current.SetSelectedGameObject(null);
            }
        }

        private float PauseAfter(char character)
        {
            switch (character)
            {
                case '.':
                case '!':
                case '?':
                case '…':
                    return _sentencePause;
                case ',':
                case ';':
                case ':':
                    return _commaPause;
                default:
                    return 0f;
            }
        }

        private void StartTyping(string text)
        {
            _text.text = ActionMarkup.Highlight(text, _actionColor);
            _text.maxVisibleCharacters = 0;
            _text.ForceMeshUpdate();
            _totalCharacters = _text.textInfo.characterCount;
            _visibleCharacters = 0;
            _nextCharacterIn = 0f;
            SetOptionsUnlocked(false);
            _isTyping = true;
            if (_charactersPerSecond <= 0f || _totalCharacters == 0)
            {
                Reveal();
            }
        }

        private void UpdateTyping()
        {
            if (_skipOnInput && IsSkipPressed())
            {
                Reveal();
                return;
            }

            // Rich-text tags (italics) are not characters here: TMP counts only what is drawn.
            _nextCharacterIn -= Time.unscaledDeltaTime;
            float delay = 1f / _charactersPerSecond;
            while (_nextCharacterIn <= 0f && _visibleCharacters < _totalCharacters)
            {
                char character = _text.textInfo.characterInfo[_visibleCharacters].character;
                _visibleCharacters++;
                _nextCharacterIn += delay + PauseAfter(character);
            }

            _text.maxVisibleCharacters = _visibleCharacters;
            if (_visibleCharacters >= _totalCharacters)
            {
                Reveal();
            }
        }

        private void Reveal()
        {
            _isTyping = false;
            _text.maxVisibleCharacters = int.MaxValue;
            SetOptionsUnlocked(true);
            if (_isResponse)
            {
                _continueButton.gameObject.SetActive(true);
            }

            TextRevealed?.Invoke();
        }

        // Locked answers stay in the layout (invisible), so the panel does not jump when they appear.
        private void SetOptionsUnlocked(bool isUnlocked)
        {
            if (_optionsGroup == null)
            {
                return;
            }

            _optionsGroup.interactable = isUnlocked;
            _optionsGroup.blocksRaycasts = isUnlocked;
            if (!isUnlocked || _optionsFadeSeconds <= 0f)
            {
                _optionsGroup.alpha = isUnlocked ? 1f : 0f;
            }
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
                    _buttons[i].SetLabel(options[i].IsSeen
                        ? ActionMarkup.Tint(options[i].Text, _seenColor)
                        : ActionMarkup.Highlight(options[i].Text, _actionColor));
                }
            }

            Deselect();
        }

        private void OnOptionClicked(string optionId)
        {
            Deselect();
            OptionChosen?.Invoke(optionId);
        }

        private void OnContinueClicked()
        {
            Deselect();
            ContinueRequested?.Invoke();
        }

        private void OnHintClicked()
        {
            Deselect();
            HintRequested?.Invoke();
        }
    }
}
