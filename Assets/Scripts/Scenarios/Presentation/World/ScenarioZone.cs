using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// An invisible line across the wagon: when the player crosses it during a scenario, the runner receives the signal
    /// (a state key set to 1), e.g. "the player walked away from the bag". The zone is the plane local Z = 0 of this
    /// object, Half Width to each side. Position is compared between frames, so a teleport across the line counts too.
    /// </summary>
    public sealed class ScenarioZone : MonoBehaviour
    {
        [SerializeField] private ScenarioRunner _runner;
        [Tooltip("Player root at feet level.")]
        [SerializeField] private Transform _player;
        [Tooltip("State key set to 1 on crossing, e.g. flag.left_bag.")]
        [SerializeField] private string _signal = string.Empty;
        [Tooltip("Half width of the line across the wagon (local X), in metres.")]
        [SerializeField, Range(0.1f, 5f)] private float _halfWidth = 1.8f;

        private float _previousSide;
        private bool _hasPrevious;

        private void Update()
        {
            if (_runner == null || _player == null)
            {
                return;
            }

            Vector3 local = transform.InverseTransformPoint(_player.position);
            float side = local.z >= 0f ? 1f : -1f;
            if (_hasPrevious && side != _previousSide && Mathf.Abs(local.x) <= _halfWidth && _runner.IsRunning)
            {
                _runner.Signal(_signal);
            }

            _previousSide = side;
            _hasPrevious = true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 1f, 0f), new Vector3(_halfWidth * 2f, 2f, 0.02f));
        }
    }
}
