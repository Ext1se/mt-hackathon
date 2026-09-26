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

        [SerializeField] private ScenarioLabels _labels;
        [SerializeField] private DialogueView _dialogue;
        [SerializeField] private ScenarioHud _hud;
        [SerializeField] private HintView _hint;
        [SerializeField] private QuestTracker _quest;
        [SerializeField] private DebriefView _debrief;
        [Tooltip("Raised with true while scenario UI needs the cursor. Wire to VSMCursorMode.SetUIOpen.")]
        [SerializeField] private UnityEvent<bool> _uiOpenChanged = new UnityEvent<bool>();

        private readonly List<ScenarioActor> _actors = new List<ScenarioActor>();
        private readonly List<ScenarioInteractable> _targets = new List<ScenarioInteractable>();
        private IScenarioResultSink _resultSink;
        private ScenarioSession _session;
        private NodeView _pendingNode;
        private ScenarioResult _pendingResult;
        private bool _isShowingResponse;
        private bool _isClockRunning;

        /// <summary>True from start until the debrief is closed.</summary>
        public bool IsRunning => _session != null;

        /// <summary>The scenario being played, or null.</summary>
        public ScenarioData CurrentScenario => _session != null ? _session.Data : null;

        private void Awake()
        {
            _resultSink = new LocalResultSink(Path.Combine(Application.persistentDataPath, ResultsFolder));
            _dialogue.OptionChosen += OnOptionChosen;
            _dialogue.ContinueRequested += OnContinueRequested;
            _dialogue.HintRequested += OnHintRequested;
            _debrief.Closed += OnDebriefClosed;
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
            if (string.IsNullOrEmpty(outcome.ResponseText))
            {
                return;
            }

            _isShowingResponse = true;
            ScenarioActor speaker = FindActor(outcome.Speaker);
            if (speaker != null)
            {
                speaker.Speak(outcome.ResponseText);
            }

            _quest.Clear(_targets);
            _dialogue.ShowResponse(SpeakerName(speaker), outcome.ResponseText);
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
            for (int i = 0; i < _actors.Count; i++)
            {
                _actors[i].OnScenarioEnded();
            }
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
            if (view.IsRoam)
            {
                _dialogue.Hide();
                SetUiOpen(false);
                if (!string.IsNullOrEmpty(view.Text))
                {
                    _hint.Show(view.Text);
                }
            }
            else
            {
                _dialogue.ShowNode(SpeakerName(speaker), view);
                SetUiOpen(true);
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
            _debrief.Show(result, _labels);
            SetUiOpen(true);
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
