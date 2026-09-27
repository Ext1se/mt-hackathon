using System.Collections;
using System.Collections.Generic;
using Game.Characters.Passengers;
using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using Game.Scenarios.Presentation.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// Imitates what the few animations cannot show: when scenario conditions first hold (neighbours asked to move,
    /// the wagon evacuated), the screen fades out, passengers are moved or hidden, and the screen fades back in.
    /// Everything is put back when the scenario ends. The cast builder fills the states from the cast file.
    /// </summary>
    public sealed class ScenarioWorldStates : MonoBehaviour
    {
        [SerializeField] private ScenarioRunner _runner;
        [SerializeField] private List<WorldState> _states = new List<WorldState>();

        [Header("Fade")]
        [SerializeField, Range(0.1f, 3f)] private float _fadeOutSeconds = 0.6f;
        [Tooltip("Time on a black screen after the change, so the new arrangement does not pop in with the fade.")]
        [SerializeField, Range(0f, 3f)] private float _holdSeconds = 0.5f;
        [SerializeField, Range(0.1f, 3f)] private float _fadeInSeconds = 0.9f;

        private readonly List<List<ConditionData>> _conditions = new List<List<ConditionData>>();
        private readonly HashSet<int> _applied = new HashSet<int>();
        private readonly List<int> _pending = new List<int>();
        private readonly Dictionary<GameObject, bool> _originalActive = new Dictionary<GameObject, bool>();
        private readonly Dictionary<Transform, Transform> _originalParents = new Dictionary<Transform, Transform>();
        private readonly Dictionary<Passenger, (PassengerSpot Spot, PassengerPose Pose, string HeldClip)> _originalSpots =
            new Dictionary<Passenger, (PassengerSpot Spot, PassengerPose Pose, string HeldClip)>();
        private ScreenFader _fader;
        private Coroutine _transition;
        private bool _isPlacingPlayer;

        /// <summary>True while the screen is fading for a change.</summary>
        public bool IsTransitioning => _transition != null;

        /// <summary>True while a change under way puts the player somewhere itself (a "player" action).</summary>
        public bool IsPlacingPlayer => _transition != null && _isPlacingPlayer;

        private void Awake()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                List<ConditionData> conditions = JsonConvert.DeserializeObject<List<ConditionData>>(_states[i].Conditions);
                _conditions.Add(conditions ?? new List<ConditionData>());
            }

            if (_runner != null)
            {
                _runner.Register(this);
            }
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.Unregister(this);
            }
        }

        /// <summary>Applies every state whose conditions now hold for the first time: instant ones at once, faded ones in one transition.</summary>
        public void Refresh(ScenarioState state, ScreenFader fader)
        {
            _fader = fader;
            for (int i = 0; i < _states.Count; i++)
            {
                if (_applied.Contains(i) || !ConditionEvaluator.IsMet(_conditions[i], state))
                {
                    continue;
                }

                _applied.Add(i);
                if (_states[i].Fade && fader != null)
                {
                    _pending.Add(i);
                    _isPlacingPlayer |= MovesPlayer(_states[i]);
                }
                else
                {
                    Apply(_states[i]);
                }
            }

            if (_pending.Count > 0 && _transition == null)
            {
                _transition = StartCoroutine(Transition());
            }
        }

        /// <summary>Puts every moved passenger and switched object back as it was before the scenario.</summary>
        public void ResetWorld()
        {
            if (_transition != null)
            {
                StopCoroutine(_transition);
                _transition = null;
            }

            if (_fader != null)
            {
                _fader.SetAlpha(0f);
            }

            foreach (KeyValuePair<Transform, Transform> pair in _originalParents)
            {
                if (pair.Key != null)
                {
                    pair.Key.SetParent(pair.Value, true);
                }
            }

            foreach (KeyValuePair<Passenger, (PassengerSpot Spot, PassengerPose Pose, string HeldClip)> pair in _originalSpots)
            {
                if (pair.Key != null && pair.Value.Spot != null)
                {
                    pair.Key.HoldClip(pair.Value.HeldClip);
                    pair.Key.TakeSpot(pair.Value.Spot, pair.Value.Pose);
                }
            }

            foreach (KeyValuePair<GameObject, bool> pair in _originalActive)
            {
                if (pair.Key != null)
                {
                    pair.Key.SetActive(pair.Value);
                }
            }

            _originalParents.Clear();
            _originalSpots.Clear();
            _originalActive.Clear();
            _applied.Clear();
            _pending.Clear();
            _isPlacingPlayer = false;
        }

        private IEnumerator Transition()
        {
            while (_pending.Count > 0)
            {
                yield return _fader.Fade(1f, _fadeOutSeconds);

                // States that became due during the fade out join this change.
                for (int i = 0; i < _pending.Count; i++)
                {
                    Apply(_states[_pending[i]]);
                }

                _pending.Clear();
                yield return new WaitForSecondsRealtime(_holdSeconds);
                yield return _fader.Fade(0f, _fadeInSeconds);
            }

            _transition = null;
            _isPlacingPlayer = false;
        }

        private static bool MovesPlayer(WorldState state)
        {
            IReadOnlyList<WorldAction> actions = state.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Kind == WorldActionKind.MovePlayer)
                {
                    return true;
                }
            }

            return false;
        }

        private void Apply(WorldState state)
        {
            IReadOnlyList<WorldAction> actions = state.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                WorldAction action = actions[i];
                switch (action.Kind)
                {
                    case WorldActionKind.Hide:
                        SetActive(action.Objects, false);
                        break;
                    case WorldActionKind.Show:
                        SetActive(action.Objects, true);
                        break;
                    case WorldActionKind.Move:
                        Move(action);
                        break;
                    case WorldActionKind.MovePlayer:
                        if (action.Spot != null && _runner != null)
                        {
                            _runner.PlacePlayer(action.Spot.transform.position, action.LookAt);
                        }

                        break;
                }
            }
        }

        private void SetActive(GameObject[] objects, bool isActive)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject target = objects[i];
                if (target == null)
                {
                    continue;
                }

                if (!_originalActive.ContainsKey(target))
                {
                    _originalActive[target] = target.activeSelf;
                }

                target.SetActive(isActive);
            }
        }

        private void Move(WorldAction action)
        {
            Passenger passenger = action.Passenger;
            if (passenger == null || action.Spot == null)
            {
                return;
            }

            if (!_originalSpots.ContainsKey(passenger))
            {
                _originalSpots[passenger] = (passenger.Spot, passenger.Pose, passenger.HeldClip);
            }

            // Into another wagon's actor group, so that wagon's culling shows the passenger, not the old one's.
            if (action.Group != null && passenger.transform.parent != action.Group)
            {
                if (!_originalParents.ContainsKey(passenger.transform))
                {
                    _originalParents[passenger.transform] = passenger.transform.parent;
                }

                passenger.transform.SetParent(action.Group, true);
            }

            if (action.SetsHeldClip)
            {
                passenger.HoldClip(action.HeldClip);
            }

            passenger.TakeSpot(action.Spot, action.Pose);
        }
    }
}
