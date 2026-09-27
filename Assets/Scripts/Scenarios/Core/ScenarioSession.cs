using System;
using System.Collections.Generic;
using Game.Scenarios.Core.Data;

namespace Game.Scenarios.Core
{
    /// <summary>
    /// Runs one playthrough of a scenario: walks the node graph, applies effects, runs hubs and trigger interrupts,
    /// counts down timers, serves hints and builds the result. Time advances only through <see cref="Tick"/>,
    /// so the presentation decides when the clock runs (not while an NPC response is on screen).
    /// </summary>
    public sealed class ScenarioSession
    {
        private readonly ScenarioData _data;
        private readonly Random _random;
        private readonly string _forcedVariantId;
        private readonly Dictionary<string, NodeData> _nodes = new Dictionary<string, NodeData>();
        private readonly ScenarioState _state = new ScenarioState();
        private readonly HashSet<string> _firedTriggers = new HashSet<string>();
        private readonly HashSet<string> _takenHubOptions = new HashSet<string>();
        private readonly Stack<string> _returnStack = new Stack<string>();
        private readonly List<DecisionRecord> _decisions = new List<DecisionRecord>();
        private readonly Dictionary<string, float> _timeInNode = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _hintProgress = new Dictionary<string, int>();

        private NodeData _current;
        private NodeView _currentView;
        private NodeData _activeHub;
        private float _nodeTimeLeft;
        private float _hubTimeLeft;
        private float _totalTime;
        private int _hintsUsed;
        private string _variantId = string.Empty;
        private ScenarioResult _result;

        /// <param name="forcedVariantId">Plays this variant instead of a weighted random one; for demos and tests.</param>
        public ScenarioSession(ScenarioData data, Random random, string forcedVariantId = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            _data = data;
            _random = random;
            _forcedVariantId = forcedVariantId;
            foreach (NodeData node in data.Nodes)
            {
                _nodes[node.Id] = node;
            }
        }

        public event Action<NodeView> NodeEntered;

        /// <summary>Raised before the jump, so <see cref="Current"/> is still the node where the choice was made.</summary>
        public event Action<ChoiceOutcome> ChoiceResolved;

        public event Action<string> HintShown;
        public event Action<ScenarioResult> Ended;

        public ScenarioData Data => _data;
        public ScenarioState State => _state;
        public NodeView Current => _currentView;
        public string VariantId => _variantId;
        public ScenarioResult Result => _result;
        public bool IsRunning => _current != null && _result == null;
        public IReadOnlyList<DecisionRecord> Decisions => _decisions;

        /// <summary>When false, timed nodes and hubs never time out; time is still tracked for hints and records.</summary>
        public bool TimersEnabled { get; set; } = true;

        /// <summary>Length of the clock that runs now: the hub clock inside a hub, otherwise the node clock. 0 = untimed.</summary>
        public float TimeLimit
        {
            get
            {
                if (!IsRunning || !TimersEnabled)
                {
                    return 0f;
                }

                return IsInActiveHub ? _activeHub.TimeLimit : _current.TimeLimit;
            }
        }

        public float TimeLeft
        {
            get
            {
                if (!IsRunning)
                {
                    return 0f;
                }

                return IsInActiveHub ? _hubTimeLeft : _nodeTimeLeft;
            }
        }

        private bool IsInActiveHub => _activeHub != null && _current == _activeHub;

        public void Start()
        {
            if (_current != null || _result != null)
            {
                throw new InvalidOperationException("The session has already been started.");
            }

            _state.DefineRange(ScenarioKeys.Loyalty, ScenarioKeys.ScaleMin, ScenarioKeys.ScaleMax);
            _state.DefineRange(ScenarioKeys.Safety, ScenarioKeys.ScaleMin, ScenarioKeys.ScaleMax);
            _state.Set(ScenarioKeys.Loyalty, _data.Loyalty);
            _state.Set(ScenarioKeys.Safety, _data.Safety);
            foreach (VariableData variable in _data.Variables)
            {
                _state.DefineRange(variable.Key, variable.Min, variable.Max);
                _state.Set(variable.Key, variable.Initial);
            }

            _variantId = PickVariant();
            if (_variantId.Length > 0)
            {
                _state.Set(ScenarioKeys.VariantPrefix + _variantId, 1);
            }

            GoTo(_data.Start);
        }

        /// <summary>Picks a visible option of the current node. Returns false for anything the player cannot pick now.</summary>
        public bool Choose(string optionId)
        {
            if (!IsRunning)
            {
                return false;
            }

            OptionData option = FindVisibleOption(optionId);
            if (option == null)
            {
                return false;
            }

            Resolve(option, false);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning || deltaTime <= 0f)
            {
                return;
            }

            NodeData node = _current;
            _totalTime += deltaTime;
            _timeInNode.TryGetValue(node.Id, out float spent);
            spent += deltaTime;
            _timeInNode[node.Id] = spent;
            ShowDueHints(node, spent);
            if (!TimersEnabled)
            {
                return;
            }

            if (IsInActiveHub)
            {
                if (node.TimeLimit > 0f)
                {
                    _hubTimeLeft -= deltaTime;
                    if (_hubTimeLeft <= 0f)
                    {
                        ExitHub();
                    }
                }

                return;
            }

            if (node.TimeLimit > 0f)
            {
                _nodeTimeLeft -= deltaTime;
                if (_nodeTimeLeft <= 0f)
                {
                    Resolve(FindOption(node, node.TimeoutOption), true);
                }
            }
        }

        /// <summary>
        /// A world event, e.g. the player crossed a zone: sets <paramref name="key"/> to 1 and, while the player roams the
        /// active hub, fires a trigger interrupt the key completes. Inside an object's menu the key waits: the interrupt
        /// fires on the way back to the hub. Returns true when an interrupt started now.
        /// </summary>
        public bool Signal(string key)
        {
            if (!IsRunning || string.IsNullOrEmpty(key))
            {
                return false;
            }

            _state.Set(key, 1);
            return IsInActiveHub && _returnStack.Count == 0 && TryFireTrigger(NextTargets.Hub);
        }

        /// <summary>Shows the next hint of the current node on the player's request. Null when there is none.</summary>
        public string RequestHint()
        {
            if (!IsRunning)
            {
                return null;
            }

            HintData hint = NextHint(_current);
            if (hint == null)
            {
                return null;
            }

            AdvanceHint(_current);
            _hintsUsed++;
            HintShown?.Invoke(hint.Text);
            return hint.Text;
        }

        private void Resolve(OptionData option, bool timedOut)
        {
            NodeData node = _current;
            // Anything chosen while a hub is active belongs to it (its menus included), except trigger interrupts.
            bool isInsideHub = _activeHub != null && _returnStack.Count == 0;
            List<AppliedEffect> applied = new List<AppliedEffect>();
            EffectApplier.Apply(option.Effects, _state, applied);
            ApplySpeedRule(node, option, timedOut, applied);

            if (isInsideHub)
            {
                if (!option.Repeatable)
                {
                    _takenHubOptions.Add(TakenKey(node, option));
                }

                if (!option.Free)
                {
                    _state.Add(ScenarioKeys.HubActions, 1);
                }
            }

            _timeInNode.TryGetValue(node.Id, out float seconds);
            _decisions.Add(new DecisionRecord(node.Id, _currentView.Text, option.Id, option.Text, option.Feedback,
                option.Reference, timedOut, applied, seconds));
            string speaker = string.IsNullOrEmpty(option.Speaker) ? node.Speaker : option.Speaker;
            ChoiceResolved?.Invoke(new ChoiceOutcome(node.Id, option.Id, option.Text, speaker, ResolveText(option.Lines),
                timedOut, option.Free, applied));

            string next = ResolveNext(option.NextRules, option.Next);
            if (string.IsNullOrEmpty(next))
            {
                // Only hub options may omit next (the validator enforces it): they return to the hub.
                next = NextTargets.Hub;
            }

            if (next != NextTargets.End && _returnStack.Count == 0 && TryFireTrigger(next))
            {
                return;
            }

            GoTo(next);
        }

        private void ApplySpeedRule(NodeData node, OptionData option, bool timedOut, List<AppliedEffect> applied)
        {
            int delta = 0;
            if (timedOut)
            {
                delta = -1;
            }
            else if (node.Critical && option.Reference)
            {
                delta = 1;
            }

            if (delta != 0)
            {
                _state.Add(ScenarioKeys.Speed, delta);
                applied.Add(new AppliedEffect(ScenarioKeys.Speed, delta));
            }
        }

        private bool TryFireTrigger(string pendingNext)
        {
            foreach (TriggerData trigger in _data.Triggers)
            {
                if (_firedTriggers.Contains(trigger.Id))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(trigger.Hub) && (_activeHub == null || _activeHub.Id != trigger.Hub))
                {
                    continue;
                }

                // Inside an object's menu the conversation goes on; a hub interrupt waits for the way back to the hub.
                if (!string.IsNullOrEmpty(trigger.Hub) && pendingNext != NextTargets.Hub && pendingNext != _activeHub.Id)
                {
                    continue;
                }

                if (!ConditionEvaluator.IsMet(trigger.Conditions, _state))
                {
                    continue;
                }

                _firedTriggers.Add(trigger.Id);
                _returnStack.Push(pendingNext);
                GoTo(trigger.Node);
                return true;
            }

            return false;
        }

        private void GoTo(string target)
        {
            switch (target)
            {
                case NextTargets.End:
                    Finish();
                    return;
                case NextTargets.Return:
                    if (_returnStack.Count == 0)
                    {
                        throw new InvalidOperationException($"'{NextTargets.Return}' used outside of a trigger in '{_data.Id}'.");
                    }

                    GoTo(_returnStack.Pop());
                    return;
                case NextTargets.Hub:
                    ResumeHub();
                    return;
            }

            NodeData node = FindNode(target);
            if (node == _activeHub)
            {
                ResumeHub();
                return;
            }

            if (node.Kind == NodeKinds.Hub)
            {
                _activeHub = node;
                _takenHubOptions.Clear();
                _state.Set(ScenarioKeys.HubActions, 0);
                _hubTimeLeft = node.TimeLimit;
            }

            Enter(node, true);
        }

        private void ResumeHub()
        {
            if (_activeHub == null)
            {
                throw new InvalidOperationException($"No active hub to return to in '{_data.Id}'.");
            }

            string early = ResolveNext(_activeHub.Leave, null);
            if (early != null)
            {
                _activeHub = null;
                GoTo(early);
                return;
            }

            if (_state.Get(ScenarioKeys.HubActions) >= _activeHub.MaxActions)
            {
                ExitHub();
                return;
            }

            // A signal that arrived during another interrupt fires on the way back to the hub.
            if (_returnStack.Count == 0 && TryFireTrigger(NextTargets.Hub))
            {
                return;
            }

            Enter(_activeHub, false);
        }

        private void ExitHub()
        {
            NodeData hub = _activeHub;
            _activeHub = null;
            GoTo(ResolveNext(hub.Exit, hub.ExitNext));
        }

        private void Enter(NodeData node, bool isFirstEntry)
        {
            _current = node;
            _nodeTimeLeft = node.TimeLimit;
            if (isFirstEntry)
            {
                _state.Set(ScenarioKeys.VisitedPrefix + node.Id, 1);
            }

            if (isFirstEntry && node.OnEnter.Count > 0)
            {
                List<AppliedEffect> applied = new List<AppliedEffect>();
                EffectApplier.Apply(node.OnEnter, _state, applied);
                if (applied.Count > 0)
                {
                    _decisions.Add(new DecisionRecord(node.Id, ResolveText(node.Lines), string.Empty, string.Empty,
                        node.Feedback, false, false, applied, 0f));
                }
            }

            _currentView = BuildView(node);
            if (node == _activeHub && _currentView.Options.Count == 0)
            {
                ExitHub();
                return;
            }

            NodeEntered?.Invoke(_currentView);
        }

        private void Finish()
        {
            EndingData ending = null;
            foreach (EndingData candidate in _data.Endings)
            {
                if (ConditionEvaluator.IsMet(candidate.Conditions, _state))
                {
                    ending = candidate;
                    break;
                }
            }

            if (ending == null)
            {
                throw new InvalidOperationException($"No ending matched in '{_data.Id}'.");
            }

            Dictionary<string, int> competencies = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> pair in _state.Values)
            {
                if (pair.Key.StartsWith(ScenarioKeys.CompetencyPrefix, StringComparison.Ordinal))
                {
                    competencies[pair.Key] = pair.Value;
                }
            }

            _result = new ScenarioResult(_data.Id, _variantId, ending.Id, ending.Title, ResolveText(ending.Lines),
                _state.Get(ScenarioKeys.Loyalty), _state.Get(ScenarioKeys.Safety), competencies, _decisions, _hintsUsed,
                _totalTime);
            Ended?.Invoke(_result);
        }

        private NodeView BuildView(NodeData node)
        {
            List<OptionView> options = new List<OptionView>();
            foreach (OptionData option in node.Options)
            {
                if (IsVisible(node, option))
                {
                    options.Add(new OptionView(option.Id, option.Text, option.Target, option.Objective, option.Quiet));
                }
            }

            int actionsLeft = node.Kind == NodeKinds.Hub ? node.MaxActions - _state.Get(ScenarioKeys.HubActions) : 0;
            return new NodeView(node, ResolveText(node.Lines), options, actionsLeft);
        }

        private bool IsVisible(NodeData node, OptionData option)
        {
            if (option.Hidden || !ConditionEvaluator.IsMet(option.Conditions, _state))
            {
                return false;
            }

            return _activeHub == null || !_takenHubOptions.Contains(TakenKey(node, option));
        }

        private static string TakenKey(NodeData node, OptionData option)
        {
            return node.Id + "/" + option.Id;
        }

        private OptionData FindVisibleOption(string optionId)
        {
            foreach (OptionData option in _current.Options)
            {
                if (option.Id == optionId && IsVisible(_current, option))
                {
                    return option;
                }
            }

            return null;
        }

        private OptionData FindOption(NodeData node, string optionId)
        {
            foreach (OptionData option in node.Options)
            {
                if (option.Id == optionId)
                {
                    return option;
                }
            }

            throw new InvalidOperationException($"Node '{node.Id}' has no option '{optionId}'.");
        }

        private NodeData FindNode(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out NodeData node))
            {
                throw new InvalidOperationException($"Node '{nodeId}' does not exist in '{_data.Id}'.");
            }

            return node;
        }

        private string ResolveText(IReadOnlyList<LineData> lines)
        {
            foreach (LineData line in lines)
            {
                if (ConditionEvaluator.IsMet(line.Conditions, _state))
                {
                    return line.Text;
                }
            }

            return string.Empty;
        }

        private string ResolveNext(IReadOnlyList<NextRuleData> rules, string fallback)
        {
            foreach (NextRuleData rule in rules)
            {
                if (ConditionEvaluator.IsMet(rule.Conditions, _state))
                {
                    return rule.Node;
                }
            }

            return fallback;
        }

        private void ShowDueHints(NodeData node, float secondsInNode)
        {
            while (true)
            {
                HintData hint = NextHint(node);
                if (hint == null || hint.AfterSeconds < 0f || secondsInNode < hint.AfterSeconds)
                {
                    return;
                }

                AdvanceHint(node);
                HintShown?.Invoke(hint.Text);
            }
        }

        private HintData NextHint(NodeData node)
        {
            _hintProgress.TryGetValue(node.Id, out int index);
            while (index < node.Hints.Count && !ConditionEvaluator.IsMet(node.Hints[index].Conditions, _state))
            {
                index++;
            }

            _hintProgress[node.Id] = index;
            return index < node.Hints.Count ? node.Hints[index] : null;
        }

        private void AdvanceHint(NodeData node)
        {
            _hintProgress.TryGetValue(node.Id, out int index);
            _hintProgress[node.Id] = index + 1;
        }

        private string PickVariant()
        {
            if (!string.IsNullOrEmpty(_forcedVariantId))
            {
                foreach (VariantData variant in _data.Variants)
                {
                    if (variant.Id == _forcedVariantId)
                    {
                        return variant.Id;
                    }
                }

                throw new ArgumentException($"Variant '{_forcedVariantId}' does not exist in '{_data.Id}'.");
            }

            int totalWeight = 0;
            foreach (VariantData variant in _data.Variants)
            {
                totalWeight += variant.Weight;
            }

            if (totalWeight <= 0)
            {
                return string.Empty;
            }

            int roll = _random.Next(totalWeight);
            foreach (VariantData variant in _data.Variants)
            {
                if (roll < variant.Weight)
                {
                    return variant.Id;
                }

                roll -= variant.Weight;
            }

            return string.Empty;
        }
    }
}
