using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>Starts a scenario when the player interacts with this object. Needs a collider.</summary>
    public sealed class ScenarioStarter : MonoBehaviour, IScenarioInteractable
    {
        [SerializeField] private ScenarioRunner _runner;
        [SerializeField] private TextAsset _scenario;
        [Tooltip("Optional: always play this variant, e.g. to show a specific branch in a demo.")]
        [SerializeField] private string _forcedVariant = string.Empty;
        [Tooltip("Optional: marker shown while no scenario is running, so the player knows where to start.")]
        [SerializeField] private GameObject _marker;

        private void OnEnable()
        {
            if (_runner != null)
            {
                _runner.RunningChanged += RefreshMarker;
            }

            RefreshMarker();
        }

        private void OnDisable()
        {
            if (_runner != null)
            {
                _runner.RunningChanged -= RefreshMarker;
            }
        }

        public void Interact()
        {
            if (_runner != null && _scenario != null && !_runner.IsRunning)
            {
                _runner.StartScenario(_scenario, _forcedVariant);
            }
        }

        private void RefreshMarker()
        {
            if (_marker != null)
            {
                _marker.SetActive(_runner != null && !_runner.IsRunning);
            }
        }
    }
}
