using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>One step of a scenario graph. See the scenario format section of Docs/ScenarioEngine_RU.md.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class NodeData
    {
        [JsonProperty("id")] private string _id = string.Empty;
        [JsonProperty("kind")] private string _kind = NodeKinds.Choice;
        [JsonProperty("speaker")] private string _speaker = string.Empty;
        [JsonProperty("emotion")] private string _emotion = string.Empty;
        [JsonProperty("lines")] private List<LineData> _lines = new List<LineData>();
        [JsonProperty("timeLimit")] private float _timeLimit = 0f;
        [JsonProperty("critical")] private bool _critical = false;
        [JsonProperty("timeoutOption")] private string _timeoutOption = string.Empty;
        [JsonProperty("options")] private List<OptionData> _options = new List<OptionData>();
        [JsonProperty("maxActions")] private int _maxActions = 0;
        [JsonProperty("leave")] private List<NextRuleData> _leave = new List<NextRuleData>();
        [JsonProperty("exit")] private List<NextRuleData> _exit = new List<NextRuleData>();
        [JsonProperty("exitNext")] private string _exitNext = string.Empty;
        [JsonProperty("onEnter")] private List<EffectData> _onEnter = new List<EffectData>();
        [JsonProperty("feedback")] private string _feedback = string.Empty;
        [JsonProperty("hints")] private List<HintData> _hints = new List<HintData>();

        public string Id => _id;
        public string Kind => _kind;
        public string Speaker => _speaker;
        public string Emotion => _emotion;
        public IReadOnlyList<LineData> Lines => _lines;

        /// <summary>Seconds to decide; 0 means no timer. For a hub it is the time for the whole hub.</summary>
        public float TimeLimit => _timeLimit;

        public bool Critical => _critical;
        public string TimeoutOption => _timeoutOption;
        public IReadOnlyList<OptionData> Options => _options;
        public int MaxActions => _maxActions;
        /// <summary>Hub only: checked every time the hub resumes; the first rule whose conditions hold ends the hub early.</summary>
        public IReadOnlyList<NextRuleData> Leave => _leave;

        /// <summary>Hub only: where to go when the hub is exhausted or its timer runs out.</summary>
        public IReadOnlyList<NextRuleData> Exit => _exit;
        public string ExitNext => _exitNext;
        public IReadOnlyList<EffectData> OnEnter => _onEnter;

        /// <summary>Debrief text for the consequences applied by OnEnter.</summary>
        public string Feedback => _feedback;

        public IReadOnlyList<HintData> Hints => _hints;
    }
}
