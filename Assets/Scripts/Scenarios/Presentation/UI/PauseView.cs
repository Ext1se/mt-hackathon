using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Pause menu on Esc: stops game time, frees the cursor and offers to resume, restart the scene or go to the menu.
    /// </summary>
    public sealed class PauseView : MonoBehaviour
    {
        private const string PauseActionName = "Walk/Pause";

        [SerializeField] private ScenarioRunner _runner;
        [Tooltip("Walking actions with Walk/Pause; without it Esc is read from the keyboard directly.")]
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _menuButton;
        [Tooltip("Main menu scene; the menu button is hidden when empty.")]
        [SerializeField] private string _menuScene = string.Empty;
        [Tooltip("Wired to the cursor mode: true frees the cursor while paused, the scenario's own state comes back after.")]
        [SerializeField] private UnityEvent<bool> _cursorNeeded = new UnityEvent<bool>();

        private InputAction _pause;
        private float _timeScaleBefore = 1f;
        private bool _isPaused;

        public bool IsPaused => _isPaused;

        private void Awake()
        {
            if (_actions != null)
            {
                _pause = _actions.FindAction(PauseActionName, false);
            }

            _resumeButton.onClick.AddListener(Resume);
            _restartButton.onClick.AddListener(OnRestartClicked);
            if (_menuButton != null)
            {
                _menuButton.onClick.AddListener(OnMenuClicked);
                _menuButton.gameObject.SetActive(!string.IsNullOrEmpty(_menuScene));
            }

            _root.SetActive(false);
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
            _resumeButton.onClick.RemoveListener(Resume);
            _restartButton.onClick.RemoveListener(OnRestartClicked);
            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveListener(OnMenuClicked);
            }

            if (_isPaused)
            {
                Time.timeScale = _timeScaleBefore;
            }
        }

        public void Pause()
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            _root.SetActive(true);
            _cursorNeeded.Invoke(true);
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
            _cursorNeeded.Invoke(_runner != null && _runner.IsUiOpen);
        }

        private bool WasPausePressed()
        {
            if (_pause != null)
            {
                return _pause.WasPressedThisFrame();
            }

            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
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
