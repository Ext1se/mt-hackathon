using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// A circle on the floor around this object (e.g. a passenger, moving with her when she changes seats): while the
    /// player stands inside during a scenario, the state key is 1, and it drops back to 0 when the player walks out.
    /// A trigger that needs the key fires as soon as the player comes close, e.g. a panicking passenger speaks up.
    /// </summary>
    public sealed class ScenarioProximity : MonoBehaviour
    {
        [SerializeField] private ScenarioRunner _runner;
        [Tooltip("Player root at feet level.")]
        [SerializeField] private Transform _player;
        [Tooltip("State key that is 1 while the player is inside, e.g. flag.near_5b.")]
        [SerializeField] private string _signal = string.Empty;
        [Tooltip("Radius on the floor around this object, in metres.")]
        [SerializeField, Range(0.3f, 5f)] private float _radius = 1.6f;

        private bool _isInside;

        private void Update()
        {
            if (_runner == null || _player == null)
            {
                return;
            }

            Vector3 offset = _player.position - transform.position;
            offset.y = 0f;
            // Counted only while a scenario runs, so a new playthrough sees the player already standing here.
            bool isInside = _runner.IsRunning && offset.sqrMagnitude <= _radius * _radius;
            if (isInside == _isInside)
            {
                return;
            }

            _isInside = isInside;
            if (isInside)
            {
                _runner.Signal(_signal);
            }
            else
            {
                _runner.ClearSignal(_signal);
            }
        }

        // A hidden passenger or a culled wagon is not near the player any more.
        private void OnDisable()
        {
            if (_isInside && _runner != null)
            {
                _runner.ClearSignal(_signal);
            }

            _isInside = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Vector3 center = transform.position;
            const int segments = 32;
            Vector3 previous = center + new Vector3(_radius, 0.05f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * _radius, 0.05f, Mathf.Sin(angle) * _radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
