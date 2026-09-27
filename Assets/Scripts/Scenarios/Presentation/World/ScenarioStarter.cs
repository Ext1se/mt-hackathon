using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>Starts a scenario when the player interacts with this object. Needs a collider.</summary>
    public sealed class ScenarioStarter : MonoBehaviour, IScenarioInteractable
    {
        [SerializeField] private ScenarioRunner _runner;
        [SerializeField] private TextAsset _scenario;
        [Tooltip("Variant played when the scene is opened directly; empty for the weighted random pick. A menu request overrides it.")]
        [SerializeField] private string _forcedVariant = string.Empty;
        [Tooltip("Optional: marker shown while no scenario is running, so the player knows where to start.")]
        [SerializeField] private GameObject _marker;
        [Tooltip("Optional: where the player is put, facing this object, before the scenario starts.")]
        [SerializeField] private Transform _standPoint;

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
            if (_runner != null && _scenario != null && _runner.CanStart)
            {
                _runner.Approach(_standPoint, this, StartScenario);
            }
        }

        private void StartScenario()
        {
            if (_runner.CanStart)
            {
                string variant = ScenarioLaunch.ForcedVariant ?? _forcedVariant;
                _runner.StartScenario(_scenario, variant);
            }
        }

        private void RefreshMarker()
        {
            if (_marker != null)
            {
                _marker.SetActive(_runner != null && _runner.CanStart);
            }
        }
    }
}
