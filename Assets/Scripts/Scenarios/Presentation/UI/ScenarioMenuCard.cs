using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>One scenario in the main menu: its start button loads the scenario scene. Placeholders have no scene.</summary>
    public sealed class ScenarioMenuCard : MonoBehaviour
    {
        [SerializeField] private Button _startButton;
        [Tooltip("Scene loaded by the start button; empty for a placeholder card.")]
        [SerializeField] private string _sceneName = string.Empty;

        public bool IsAvailable => !string.IsNullOrEmpty(_sceneName);

        private void Awake()
        {
            _startButton.interactable = IsAvailable;
            _startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnDestroy()
        {
            _startButton.onClick.RemoveListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            if (IsAvailable)
            {
                SceneManager.LoadScene(_sceneName);
            }
        }
    }
}
