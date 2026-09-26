using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Scenario description overlay: title, summary and status. Shown while the briefing action (Tab) is held,
    /// or toggled by its own button for touch screens. Before a scenario starts it describes the scenario of the scene.
    /// </summary>
    public sealed class BriefingView : MonoBehaviour
    {
        private const string BriefingActionName = "Walk/Briefing";

        [SerializeField] private ScenarioRunner _runner;
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _summary;
        [SerializeField] private TMP_Text _status;
        [Tooltip("Scenario described before one is started; the cast builder sets it.")]
        [SerializeField] private TextAsset _scenario;
        [Tooltip("Where to go to start the scenario; shown while none is running.")]
        [SerializeField] private string _startHint = string.Empty;
        [SerializeField] private string _notStartedLabel = string.Empty;
        [SerializeField] private string _runningLabel = string.Empty;
        [Tooltip("Optional: returns to the main menu scene.")]
        [SerializeField] private Button _menuButton;
        [SerializeField] private string _menuScene = string.Empty;

        private InputAction _briefing;
        private ScenarioData _sceneScenario;
        private bool _isToggledOn;
        private bool _isShown;

        private void Awake()
        {
            if (_actions != null)
            {
                _briefing = _actions.FindAction(BriefingActionName, false);
            }

            _toggleButton.onClick.AddListener(OnToggleClicked);
            if (_menuButton != null)
            {
                _menuButton.onClick.AddListener(OnMenuClicked);
                _menuButton.gameObject.SetActive(!string.IsNullOrEmpty(_menuScene));
            }

            _root.SetActive(false);
        }

        private void Update()
        {
            bool isHeld = _briefing != null && _briefing.IsPressed();
            bool shouldShow = isHeld || _isToggledOn;
            if (shouldShow && !_isShown)
            {
                Refresh();
            }

            if (shouldShow != _isShown)
            {
                _isShown = shouldShow;
                _root.SetActive(shouldShow);
            }
        }

        private void OnDestroy()
        {
            _toggleButton.onClick.RemoveListener(OnToggleClicked);
            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveListener(OnMenuClicked);
            }
        }

        public void SetSceneScenario(TextAsset scenario, string startHint)
        {
            _scenario = scenario;
            _startHint = startHint;
            _sceneScenario = null;
        }

        private void OnMenuClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_menuScene);
        }

        private void OnToggleClicked()
        {
            _isToggledOn = !_isToggledOn;
        }

        private void Refresh()
        {
            ScenarioData data = _runner != null ? _runner.CurrentScenario : null;
            bool isRunning = data != null;
            if (!isRunning)
            {
                data = SceneScenario();
            }

            _title.text = data != null ? data.Title : string.Empty;
            _summary.text = data != null ? data.Summary : string.Empty;
            _status.text = isRunning ? $"{_runningLabel}: {VariantLabel(data, _runner.CurrentVariantId)}" : $"{_notStartedLabel}. {_startHint}";
        }

        // Testers need to know which branch they are on; the variant title comes from the scenario file.
        private static string VariantLabel(ScenarioData data, string variantId)
        {
            foreach (VariantData variant in data.Variants)
            {
                if (variant.Id == variantId)
                {
                    return $"{variant.Id} — {variant.Title}";
                }
            }

            return variantId;
        }

        private ScenarioData SceneScenario()
        {
            if (_sceneScenario == null && _scenario != null)
            {
                try
                {
                    _sceneScenario = ScenarioLoader.Parse(_scenario.text);
                }
                catch (ScenarioFormatException exception)
                {
                    Debug.LogError(exception.Message, _scenario);
                }
            }

            return _sceneScenario;
        }
    }
}
