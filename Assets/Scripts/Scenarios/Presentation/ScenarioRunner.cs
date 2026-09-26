using System;
using System.Collections.Generic;
using System.IO;
using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using Game.Scenarios.Presentation.Results;
using Game.Scenarios.Presentation.UI;
using Game.Scenarios.Presentation.World;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Scenarios.Presentation
{
    /// <summary>
    /// Bridges a <see cref="ScenarioSession"/> and the scene: shows nodes and NPC responses, runs the scenario clock only
    /// while the player can act, drives speaking actors, highlights world objectives, shows the debrief and stores results.
    /// </summary>
    public sealed class ScenarioRunner : MonoBehaviour
    {
        private const string ResultsFolder = "ScenarioResults";

        /// <summary>Target id of the radio: a HUD button instead of a scene object.</summary>
        public const string RadioTarget = "radio";

        [SerializeField] private ScenarioLabels _labels;
        [SerializeField] private DialogueView _dialogue;
        [SerializeField] private ScenarioHud _hud;
        [SerializeField] private HintView _hint;
        [SerializeField] private QuestTracker _quest;
        [SerializeField] private DebriefView _debrief;
        [Tooltip("Off: decisions are never timed out (for demos and content review).")]
        [SerializeField] private bool _timersEnabled = true;
        [Tooltip("Speaker name shown above the player's own line after a choice.")]
        [SerializeField] private string _playerName = string.Empty;
        [Tooltip("Raised with true while scenario UI needs the cursor. Wire to VSMCursorMode.SetUIOpen.")]
        [SerializeField] private UnityEvent<bool> _uiOpenChanged = new UnityEvent<bool>();

        private readonly List<ScenarioActor> _actors = new List<ScenarioActor>();
        private readonly List<ScenarioInteractable> _targets = new List<ScenarioInteractable>();
        private IScenarioResultSink _resultSink;
        private IViewFocus _viewFocus;
        private ScenarioSession _session;
        private NodeView _pendingNode;
        private ScenarioResult _pendingResult;
        private bool _isShowingResponse;
        private bool _isClockRunning;

        /// <summary>Raised when a scenario starts and when its debrief is closed.</summary>
        public event Action RunningChanged;

        /// <summary>True from start until the debrief is closed.</summary>
        public bool IsRunning => _session != null;

        /// <summary>The scenario being played, or null.</summary>
        public ScenarioData CurrentScenario => _session != null ? _session.Data : null;

        public bool TimersEnabled
        {
            get => _timersEnabled;
            set
            {
                _timersEnabled = value;
                if (_session != null)
                {
                    _session.TimersEnabled = value;
                }
            }
        }

        private void Awake()
        {
            _resultSink = new LocalResultSink(Path.Combine(Application.persistentDataPath, ResultsFolder));
            _dialogue.OptionChosen += OnOptionChosen;
            _dialogue.ContinueRequested += OnContinueRequested;
            _dialogue.HintRequested += OnHintRequested;
            _hud.RadioRequested += OnRadioRequested;
            _debrief.Closed += OnDebriefClosed;
            _viewFocus = FindViewFocus();
        }

        private void Update()
        {
            if (!_isClockRunning || _session == null || !_session.IsRunning)
            {
                return;
            }

            _session.Tick(Time.deltaTime);
            if (_isClockRunning && _session != null && _session.IsRunning)
            {
                _hud.ShowTimer(_session.TimeLeft, _session.TimeLimit);
            }
        }

        private void OnDestroy()
        {
            _dialogue.OptionChosen -= OnOptionChosen;
            _dialogue.ContinueRequested -= OnContinueRequested;
            _dialogue.HintRequested -= OnHintRequested;
            _hud.RadioRequested -= OnRadioRequested;
            _debrief.Closed -= OnDebriefClosed;
            DetachSession();
        }

        public void StartScenario(TextAsset scenario, string forcedVariantId)
        {
            if (_session != null)
            {
                return;
            }

            ScenarioData data;
            try
            {
                data = ScenarioLoader.Parse(scenario.text);
            }
            catch (ScenarioFormatException exception)
            {
                Debug.LogError(exception.Message, scenario);
                return;
            }

            _session = new ScenarioSession(data, new System.Random(),
                string.IsNullOrEmpty(forcedVariantId) ? null : forcedVariantId);
            _session.TimersEnabled = _timersEnabled;
            _session.NodeEntered += OnNodeEntered;
            _session.ChoiceResolved += OnChoiceResolved;
            _session.HintShown += _hint.Show;
            _session.Ended += OnEnded;
            _hud.Show(data.Title);

            try
            {
                _session.Start();
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"Scenario '{data.Id}' could not start: {exception.Message}", scenario);
                _hud.Hide();
                DetachSession();
                return;
            }

            _hud.SetScales(_session.State.Get(ScenarioKeys.Loyalty), _session.State.Get(ScenarioKeys.Safety));
            for (int i = 0; i < _actors.Count; i++)
            {
                _actors[i].OnScenarioStarted(_session.VariantId);
            }

            RunningChanged?.Invoke();
        }

        /// <summary>Completes the current roam objective whose target is <paramref name="targetId"/>, if any.</summary>
        public void Interact(string targetId)
        {
            if (_session == null || !_session.IsRunning || _isShowingResponse)
            {
                return;
            }

            IReadOnlyList<OptionView> options = _session.Current.Options;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Target == targetId)
                {
                    _session.Choose(options[i].Id);
                    return;
                }
            }
        }

        /// <summary>The objective text of the current option that uses <paramref name="targetId"/>, if the player can use it now.</summary>
        public bool TryGetObjective(string targetId, out string objective)
        {
            objective = null;
            if (_session == null || !_session.IsRunning || _isShowingResponse)
            {
                return false;
            }

            IReadOnlyList<OptionView> options = _session.Current.Options;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Target == targetId)
                {
                    objective = options[i].Objective;
                    return true;
                }
            }

            return false;
        }

        public void Register(ScenarioActor actor)
        {
            if (!_actors.Contains(actor))
            {
                _actors.Add(actor);
            }
        }

        public void Unregister(ScenarioActor actor)
        {
            _actors.Remove(actor);
        }

        public void Register(ScenarioInteractable target)
        {
            if (!_targets.Contains(target))
            {
                _targets.Add(target);
            }
        }

        public void Unregister(ScenarioInteractable target)
        {
            _targets.Remove(target);
        }

        private void OnNodeEntered(NodeView view)
        {
            if (_isShowingResponse)
            {
                _pendingNode = view;
                return;
            }

            ShowNode(view);
        }

        private void OnChoiceResolved(ChoiceOutcome outcome)
        {
            _isClockRunning = false;
            _hud.SetScales(_session.State.Get(ScenarioKeys.Loyalty), _session.State.Get(ScenarioKeys.Safety));
            _hud.ShowDeltas(outcome.Effects);
            _hud.ShowTimer(0f, 0f);
            _hud.SetRadioVisible(false);

            // Every choice pauses on a response screen: the NPC's answer, or the player's own line when there is none,
            // so the next node never appears without a button press. Walking up to something is not a choice.
            if (outcome.IsFree && string.IsNullOrEmpty(outcome.ResponseText))
            {
                return;
            }

            _isShowingResponse = true;
            string speakerName = _playerName;
            string text = outcome.OptionText;
            if (!string.IsNullOrEmpty(outcome.ResponseText))
            {
                ScenarioActor speaker = FindActor(outcome.Speaker);
                if (speaker != null)
                {
                    speaker.Speak(outcome.ResponseText);
                    FocusOn(speaker);
                }

                speakerName = SpeakerName(speaker);
                text = outcome.ResponseText;
            }

            _quest.Clear(_targets);
            _dialogue.ShowResponse(speakerName, text);
            SetUiOpen(true);
        }

        private void OnEnded(ScenarioResult result)
        {
            SaveResult(result);
            if (_isShowingResponse)
            {
                _pendingResult = result;
                return;
            }

            ShowDebrief(result);
        }

        private void OnContinueRequested()
        {
            _isShowingResponse = false;
            if (_pendingResult != null)
            {
                ScenarioResult result = _pendingResult;
                _pendingResult = null;
                ShowDebrief(result);
                return;
            }

            if (_pendingNode != null)
            {
                NodeView node = _pendingNode;
                _pendingNode = null;
                ShowNode(node);
            }
        }

        private void OnOptionChosen(string optionId)
        {
            if (_session != null && !_isShowingResponse)
            {
                _session.Choose(optionId);
            }
        }

        private void OnRadioRequested()
        {
            Interact(RadioTarget);
        }

        private void OnHintRequested()
        {
            if (_session != null)
            {
                _session.RequestHint();
            }
        }

        private void OnDebriefClosed()
        {
            _debrief.Hide();
            _hud.Hide();
            DetachSession();
            SetUiOpen(false);
            ClearFocus();
            for (int i = 0; i < _actors.Count; i++)
            {
                _actors[i].OnScenarioEnded();
            }

            RunningChanged?.Invoke();
        }

        private void ShowNode(NodeView view)
        {
            ScenarioActor speaker = FindActor(view.Node.Speaker);
            if (speaker != null)
            {
                speaker.SetEmotion(view.Node.Emotion);
                speaker.Speak(view.Text);
            }

            _quest.Show(view.Options, _targets);
            _hud.SetRadioVisible(view.IsRoam && HasTarget(view.Options, RadioTarget));
            if (view.IsRoam)
            {
                _dialogue.Hide();
                SetUiOpen(false);
                ClearFocus();
                if (!string.IsNullOrEmpty(view.Text))
                {
                    _hint.Show(view.Text);
                }
            }
            else
            {
                _hint.Hide();
                _dialogue.ShowNode(SpeakerName(speaker), view);
                SetUiOpen(true);
                if (speaker != null)
                {
                    FocusOn(speaker);
                }
            }

            _isClockRunning = true;
        }

        private void ShowDebrief(ScenarioResult result)
        {
            _isClockRunning = false;
            _dialogue.Hide();
            _hint.Hide();
            _quest.Clear(_targets);
            _hud.ShowTimer(0f, 0f);
            _hud.SetRadioVisible(false);
            _debrief.Show(result, _labels);
            SetUiOpen(true);
            ClearFocus();
        }

        private void SaveResult(ScenarioResult result)
        {
            try
            {
                _resultSink.Save(result);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Scenario result was not saved: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning($"Scenario result was not saved: {exception.Message}");
            }
        }

        private void DetachSession()
        {
            if (_session == null)
            {
                return;
            }

            _session.NodeEntered -= OnNodeEntered;
            _session.ChoiceResolved -= OnChoiceResolved;
            _session.HintShown -= _hint.Show;
            _session.Ended -= OnEnded;
            _session = null;
            _pendingNode = null;
            _pendingResult = null;
            _isShowingResponse = false;
            _isClockRunning = false;
        }

        private ScenarioActor FindActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return null;
            }

            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i].ActorId == actorId)
                {
                    return _actors[i];
                }
            }

            return null;
        }

        private static bool HasTarget(IReadOnlyList<OptionView> options, string targetId)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Target == targetId)
                {
                    return true;
                }
            }

            return false;
        }

        // The walking controller implements IViewFocus; it lives in another assembly, so it is found by interface.
        private static IViewFocus FindViewFocus()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IViewFocus focus)
                {
                    return focus;
                }
            }

            return null;
        }

        private void FocusOn(ScenarioActor actor)
        {
            if (_viewFocus != null && actor.TryGetFocusPoint(out Vector3 point))
            {
                _viewFocus.FocusOn(point);
            }
        }

        private void ClearFocus()
        {
            if (_viewFocus != null)
            {
                _viewFocus.ClearFocus();
            }
        }

        private static string SpeakerName(ScenarioActor actor)
        {
            return actor != null ? actor.DisplayName : string.Empty;
        }

        private void SetUiOpen(bool isOpen)
        {
            _uiOpenChanged.Invoke(isOpen);
        }
    }
}
