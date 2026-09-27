using DG.Tweening;
using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Intro screen and pause menu in one panel of two pages: what the scenario is about (no branches, no answers), then
    /// the controls and where the yellow trail leads. Shown when the scene starts and again on Esc, where it also offers
    /// to restart the scene or go to the menu. Game time is stopped and the cursor freed while it is open.
    /// </summary>
    public sealed class PauseView : MonoBehaviour
    {
        private const string PauseActionName = "Walk/Pause";

        [SerializeField] private ScenarioRunner _runner;
        [Tooltip("Walking actions with Walk/Pause; without it Esc is read from the keyboard directly.")]
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private GameObject _root;
        [Tooltip("Opens the panel as the intro when the scene starts.")]
        [SerializeField] private bool _showOnStart = true;

        [Header("Pages")]
        [SerializeField] private GameObject _scenarioPage;
        [SerializeField] private GameObject _controlsPage;
        [SerializeField] private TMP_Text _caption;
        [SerializeField] private TMP_Text _scenarioTitle;
        [SerializeField] private TMP_Text _scenarioText;
        [SerializeField] private TMP_Text _guideText;

        [Header("Buttons")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private TMP_Text _resumeLabel;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _menuButton;
        [Tooltip("Main menu scene; the menu button is hidden when empty.")]
        [SerializeField] private string _menuScene = string.Empty;
        [Tooltip("On-screen pause button for touch screens, where there is no Esc; hidden while the menu is open.")]
        [SerializeField] private Button _pauseButton;
        [Tooltip("Shows the pause button on a desktop too, e.g. to check it in the editor.")]
        [SerializeField] private bool _pauseButtonOnDesktop;

        [Header("Content")]
        [Tooltip("Scenario described before one is started; the cast builder sets it.")]
        [SerializeField] private TextAsset _scenario;
        [Tooltip("Who to talk to to start the scenario; the cast builder sets it.")]
        [SerializeField] private string _startHint = string.Empty;
        [SerializeField] private string _scenarioCaption = string.Empty;
        [SerializeField] private string _controlsCaption = string.Empty;
        [SerializeField] private string _pauseCaption = string.Empty;
        [SerializeField] private string _startLabel = string.Empty;
        [SerializeField] private string _resumeText = string.Empty;
        [Tooltip("Where the trail leads before the scenario starts.")]
        [SerializeField, TextArea] private string _guideBeforeStart = string.Empty;
        [Tooltip("Where the trail leads while the scenario runs.")]
        [SerializeField, TextArea] private string _guideRunning = string.Empty;

        [Tooltip("Wired to the cursor mode: true frees the cursor while paused, the scenario's own state comes back after.")]
        [SerializeField] private UnityEvent<bool> _cursorNeeded = new UnityEvent<bool>();

        private InputAction _pause;
        private ScenarioData _sceneScenario;
        private float _timeScaleBefore = 1f;
        private bool _isPaused;
        private bool _isIntro;
        private Vector2 _nextPrimaryPosition;
        private Vector2 _nextSecondaryPosition;
        private Color _nextPrimaryColor;
        private Color _nextSecondaryColor;

        public bool IsPaused => _isPaused;

        private void Awake()
        {
            if (_actions != null)
            {
                _pause = _actions.FindAction(PauseActionName, false);
            }

            _nextButton.onClick.AddListener(OnNextClicked);
            _backButton.onClick.AddListener(OnBackClicked);
            _resumeButton.onClick.AddListener(Resume);
            _restartButton.onClick.AddListener(OnRestartClicked);
            _menuButton.onClick.AddListener(OnMenuClicked);
            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(Pause);
            }

            // In the pause menu the first page also offers to resume: "Next" steps aside into the back button's slot.
            _nextPrimaryPosition = ((RectTransform)_nextButton.transform).anchoredPosition;
            _nextSecondaryPosition = ((RectTransform)_backButton.transform).anchoredPosition;
            _nextPrimaryColor = _nextButton.image.color;
            _nextSecondaryColor = _backButton.image.color;
            _root.SetActive(false);
            RefreshPauseButton();
        }

        private void Start()
        {
            if (_showOnStart)
            {
                Open(true);
            }
        }

        private void Update()
        {
            if (!WasPausePressed())
            {
                return;
            }

            if (_isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        private void OnDestroy()
        {
            _nextButton.onClick.RemoveListener(OnNextClicked);
            _backButton.onClick.RemoveListener(OnBackClicked);
            _resumeButton.onClick.RemoveListener(Resume);
            _restartButton.onClick.RemoveListener(OnRestartClicked);
            _menuButton.onClick.RemoveListener(OnMenuClicked);
            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(Pause);
            }

            if (_isPaused)
            {
                Time.timeScale = _timeScaleBefore;
            }
        }

        public void Pause()
        {
            Open(false);
        }

        public void Resume()
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            Time.timeScale = _timeScaleBefore;
            _root.SetActive(false);
            RefreshPauseButton();
            _cursorNeeded.Invoke(_runner != null && _runner.IsUiOpen);
        }

        private void Open(bool isIntro)
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            _isIntro = isIntro;
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            Refresh();
            ShowPage(false);
            _root.SetActive(true);
            RefreshPauseButton();
            _cursorNeeded.Invoke(true);
        }

        private void RefreshPauseButton()
        {
            if (_pauseButton != null)
            {
                _pauseButton.gameObject.SetActive(!_isPaused && (Application.isMobilePlatform || _pauseButtonOnDesktop));
            }
        }

        private void Refresh()
        {
            ScenarioData data = _runner != null ? _runner.CurrentScenario : null;
            bool isRunning = data != null;
            if (!isRunning)
            {
                data = SceneScenario();
            }

            _scenarioTitle.text = data != null ? data.Title : string.Empty;
            _scenarioText.text = data == null ? string.Empty : string.IsNullOrEmpty(data.Intro) ? data.Summary : data.Intro;
            string guide = isRunning ? _guideRunning : _guideBeforeStart;
            if (!isRunning && !string.IsNullOrEmpty(_startHint))
            {
                guide += $"\n<size=85%><color=#BFC7D4>{_startHint}</color></size>";
            }

            _guideText.text = guide;
            _resumeLabel.text = _isIntro ? _startLabel : _resumeText;
            // The intro is a clean first screen; leaving the scene is offered from the pause menu only.
            _restartButton.gameObject.SetActive(!_isIntro);
            _menuButton.gameObject.SetActive(!_isIntro && !string.IsNullOrEmpty(_menuScene));
        }

        private void ShowPage(bool isControls)
        {
            _scenarioPage.SetActive(!isControls);
            _controlsPage.SetActive(isControls);
            _nextButton.gameObject.SetActive(!isControls);
            _backButton.gameObject.SetActive(isControls);
            _resumeButton.gameObject.SetActive(isControls || !_isIntro);
            ((RectTransform)_nextButton.transform).anchoredPosition = _isIntro ? _nextPrimaryPosition : _nextSecondaryPosition;
            _nextButton.image.color = _isIntro ? _nextPrimaryColor : _nextSecondaryColor;
            string caption = isControls ? _controlsCaption : _scenarioCaption;
            _caption.text = _isIntro || string.IsNullOrEmpty(_pauseCaption) ? caption : $"{_pauseCaption}  ·  {caption}";
        }

        private ScenarioData SceneScenario()
        {
            if (_sceneScenario == null && _scenario != null)
            {
                try
                {
                    _sceneScenario = ScenarioSource.Parse(_scenario);
                }
                catch (ScenarioFormatException exception)
                {
                    Debug.LogError(exception.Message, _scenario);
                }
            }

            return _sceneScenario;
        }

        private bool WasPausePressed()
        {
            if (_pause != null)
            {
                return _pause.WasPressedThisFrame();
            }

            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        private void OnNextClicked()
        {
            ShowPage(true);
        }

        private void OnBackClicked()
        {
            ShowPage(false);
        }

        private void OnRestartClicked()
        {
            LeaveTo(SceneManager.GetActiveScene().path);
        }

        private void OnMenuClicked()
        {
            LeaveTo(_menuScene);
        }

        // Time and tweens are global: both are reset before the next scene starts.
        private void LeaveTo(string scene)
        {
            _isPaused = false;
            Time.timeScale = 1f;
            DOTween.KillAll();
            SceneManager.LoadScene(scene);
        }
    }
}
