using System.Collections.Generic;
using Game.Scenarios.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// Draws running chevrons on the floor from the player to the objective selected in the quest tracker, or to the
    /// scenario starter before a scenario runs. The guide key switches the objective (and turns the trail off after
    /// the last one). The route follows the aisle, which runs along the train's local Z axis. Both ends fade out over
    /// a fixed distance through the line's alpha gradient; the chevrons scroll in the trail shader.
    /// </summary>
    public sealed class ScenarioGuide : MonoBehaviour
    {
        private const string GuideActionName = "Walk/Guide";
        private const int MaxPoints = 5;

        [SerializeField] private ScenarioRunner _runner;
        [SerializeField] private QuestTracker _quest;
        [SerializeField] private InputActionAsset _actions;
        [Tooltip("Player root at feet level; the trail starts here.")]
        [SerializeField] private Transform _player;
        [Tooltip("Train root: the aisle runs along its local Z axis at local X = Aisle X.")]
        [SerializeField] private Transform _train;
        [SerializeField] private float _aisleX;
        [Tooltip("Line lying flat on the floor, textured with chevrons that point along the line.")]
        [SerializeField] private LineRenderer _line;

        [Header("Shape")]
        [SerializeField, Range(0f, 0.2f)] private float _floorOffset = 0.03f;
        [Tooltip("The trail starts this far ahead of the player, so it is not drawn under the camera.")]
        [SerializeField, Range(0f, 2f)] private float _startGap = 0.7f;
        [Tooltip("Length of the stub from the aisle toward the target at the end of the trail.")]
        [SerializeField, Range(0f, 1f)] private float _endStub = 0.35f;
        [Tooltip("The trail disappears when the player is this close to the target (horizontally).")]
        [SerializeField, Range(0.5f, 3f)] private float _arriveDistance = 1.3f;
        [Tooltip("Metres over which the start of the trail fades in.")]
        [SerializeField, Range(0f, 3f)] private float _fadeInLength = 1.2f;
        [Tooltip("Metres over which the end of the trail fades out.")]
        [SerializeField, Range(0f, 3f)] private float _fadeOutLength = 0.9f;

        private readonly List<ScenarioInteractable> _targets = new List<ScenarioInteractable>();
        private readonly List<ScenarioStarter> _starters = new List<ScenarioStarter>();
        private readonly Vector3[] _route = new Vector3[MaxPoints];
        private readonly Vector3[] _points = new Vector3[MaxPoints];
        private readonly GradientColorKey[] _colorKeys = { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[4];
        private readonly Gradient _gradient = new Gradient();
        private InputAction _guide;
        private bool _isStarterGuideOff;

        private void Awake()
        {
            if (_actions != null)
            {
                _guide = _actions.FindAction(GuideActionName, false);
            }

            _line.enabled = false;
        }

        private void Start()
        {
            // Targets in culled wagons are inactive and unregistered from the runner, but the trail must still reach them.
            _targets.AddRange(FindObjectsByType<ScenarioInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            _starters.AddRange(FindObjectsByType<ScenarioStarter>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        private void Update()
        {
            if (_guide != null && _guide.WasPressedThisFrame())
            {
                OnGuidePressed();
            }

            if (!TryGetDestination(out Vector3 destination) || !TryBuildRoute(destination, out int count))
            {
                if (_line.enabled)
                {
                    _line.enabled = false;
                }

                return;
            }

            float length = 0f;
            _line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                _line.SetPosition(i, _points[i]);
                if (i > 0)
                {
                    length += Vector3.Distance(_points[i - 1], _points[i]);
                }
            }

            ApplyFade(length);
            _line.enabled = true;
        }

        private void OnGuidePressed()
        {
            if (_runner.IsRunning)
            {
                if (_runner.IsRoaming)
                {
                    _quest.SelectNext();
                }

                return;
            }

            _isStarterGuideOff = !_isStarterGuideOff;
        }

        // The gradient runs over the normalized line length, so fixed fade distances become per-frame key times.
        // A short trail shares its length between the two fades and never becomes fully opaque.
        private void ApplyFade(float length)
        {
            float fadeIn = length > 0f ? _fadeInLength / length : 0f;
            float fadeOut = length > 0f ? _fadeOutLength / length : 0f;
            float total = fadeIn + fadeOut;
            if (total > 1f)
            {
                fadeIn /= total;
                fadeOut /= total;
            }

            _alphaKeys[0] = new GradientAlphaKey(0f, 0f);
            _alphaKeys[1] = new GradientAlphaKey(1f, fadeIn);
            _alphaKeys[2] = new GradientAlphaKey(1f, 1f - fadeOut);
            _alphaKeys[3] = new GradientAlphaKey(0f, 1f);
            _gradient.SetKeys(_colorKeys, _alphaKeys);
            _line.colorGradient = _gradient;
        }

        private bool TryGetDestination(out Vector3 destination)
        {
            destination = Vector3.zero;
            if (_player == null || _train == null)
            {
                return false;
            }

            if (!_runner.IsRunning)
            {
                return !_isStarterGuideOff && TryFindNearestStarter(out destination);
            }

            return _runner.IsRoaming && TryFindNearestTarget(_quest.SelectedTargets, out destination);
        }

        private bool TryFindNearestStarter(out Vector3 destination)
        {
            destination = Vector3.zero;
            float best = float.MaxValue;
            for (int i = 0; i < _starters.Count; i++)
            {
                ScenarioStarter starter = _starters[i];
                if (starter == null || !starter.gameObject.activeSelf)
                {
                    continue;
                }

                float distance = RouteLength(starter.transform.position);
                if (distance < best)
                {
                    best = distance;
                    destination = starter.transform.position;
                }
            }

            return best < float.MaxValue;
        }

        // An objective may have several targets ("find the owner" among look-alikes): the trail leads to the closest one.
        private bool TryFindNearestTarget(IReadOnlyList<string> targetIds, out Vector3 destination)
        {
            destination = Vector3.zero;
            float best = float.MaxValue;
            for (int i = 0; i < _targets.Count; i++)
            {
                ScenarioInteractable target = _targets[i];
                if (target == null || !target.gameObject.activeSelf || !Contains(targetIds, target.TargetId))
                {
                    continue;
                }

                float distance = RouteLength(target.transform.position);
                if (distance < best)
                {
                    best = distance;
                    destination = target.transform.position;
                }
            }

            return best < float.MaxValue;
        }

        private float RouteLength(Vector3 destination)
        {
            Vector3 from = _train.InverseTransformPoint(_player.position);
            Vector3 to = _train.InverseTransformPoint(destination);
            return Mathf.Abs(from.x - _aisleX) + Mathf.Abs(from.z - to.z) + Mathf.Abs(to.x - _aisleX);
        }

        // Player -> aisle -> along the aisle -> a short stub toward the target; the start is trimmed by the start gap.
        private bool TryBuildRoute(Vector3 destination, out int count)
        {
            Vector3 from = _train.InverseTransformPoint(_player.position);
            Vector3 to = _train.InverseTransformPoint(destination);
            float y = from.y + _floorOffset;
            if (new Vector2(to.x - from.x, to.z - from.z).magnitude < _arriveDistance)
            {
                count = 0;
                return false;
            }

            int routeCount = 0;
            _route[routeCount++] = new Vector3(from.x, y, from.z);
            _route[routeCount++] = new Vector3(_aisleX, y, from.z);
            _route[routeCount++] = new Vector3(_aisleX, y, to.z);
            float side = to.x - _aisleX;
            if (Mathf.Abs(side) > 0.01f)
            {
                _route[routeCount++] = new Vector3(_aisleX + Mathf.Sign(side) * Mathf.Min(_endStub, Mathf.Abs(side)), y, to.z);
            }

            count = Trim(routeCount, _startGap);
            for (int i = 0; i < count; i++)
            {
                _points[i] = _train.TransformPoint(_points[i]);
            }

            return count >= 2;
        }

        // Copies the route to _points without its first `gap` metres and without zero-length segments.
        private int Trim(int routeCount, float gap)
        {
            int count = 0;
            float left = gap;
            for (int i = 0; i < routeCount - 1; i++)
            {
                Vector3 a = _route[i];
                Vector3 b = _route[i + 1];
                float length = Vector3.Distance(a, b);
                if (length < 0.001f)
                {
                    continue;
                }

                if (left >= length)
                {
                    left -= length;
                    continue;
                }

                if (count == 0)
                {
                    _points[count++] = Vector3.Lerp(a, b, left / length);
                    left = 0f;
                }

                _points[count++] = b;
            }

            return count;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
