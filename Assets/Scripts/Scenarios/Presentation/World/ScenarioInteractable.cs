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
        [Tooltip("Optional: where the player is put, facing this object, before its objective is used.")]
        [SerializeField] private Transform _standPoint;

        public string TargetId => _targetId;

        /// <summary>Where the player stands to use this object; null when the player uses it from anywhere.</summary>
        public Transform StandPoint => _standPoint;

        /// <summary>Where the camera should look at this object: the middle of what is rendered, else the pivot.</summary>
        public Vector3 FocusPoint
        {
            get
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    return transform.position;
                }

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                return bounds.center;
            }
        }

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
            // Only an objective the player can use now moves the player there.
            if (_runner != null && _runner.TryGetObjective(_targetId, out string _))
            {
                _runner.Approach(_standPoint, this, InteractNow);
            }
        }

        private void InteractNow()
        {
            _runner.Interact(_targetId);
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
