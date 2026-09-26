using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// A world object that completes roam objectives whose option target equals <see cref="TargetId"/>.
    /// Needs a collider so the interaction ray can hit it.
    /// </summary>
    public sealed class ScenarioInteractable : MonoBehaviour, IScenarioInteractable
    {
        [SerializeField] private ScenarioRunner _runner;
        [Tooltip("Option target id in scenario JSON, e.g. passenger_12V.")]
        [SerializeField] private string _targetId = string.Empty;
        [Tooltip("Optional marker shown while this object is a current objective.")]
        [SerializeField] private GameObject _marker;

        public string TargetId => _targetId;

        private void Awake()
        {
            SetHighlighted(false);
        }

        private void OnEnable()
        {
            if (_runner != null)
            {
                _runner.Register(this);
            }
        }

        private void OnDisable()
        {
            if (_runner != null)
            {
                _runner.Unregister(this);
            }
        }

        public void Interact()
        {
            if (_runner != null)
            {
                _runner.Interact(_targetId);
            }
        }

        public void SetHighlighted(bool isHighlighted)
        {
            if (_marker != null)
            {
                _marker.SetActive(isHighlighted);
            }
        }
    }
}
