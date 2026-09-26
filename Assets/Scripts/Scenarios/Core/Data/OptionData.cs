using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>One player decision: a menu answer, a hub action or, with a target, an interaction in the world.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class OptionData
    {
        [JsonProperty("id")] private string _id = string.Empty;
        [JsonProperty("text")] private string _text = string.Empty;
        [JsonProperty("hidden")] private bool _hidden = false;
        [JsonProperty("repeatable")] private bool _repeatable = false;
        [JsonProperty("reference")] private bool _reference = false;
        [JsonProperty("speaker")] private string _speaker = string.Empty;
        [JsonProperty("target")] private string _target = string.Empty;
        [JsonProperty("objective")] private string _objective = string.Empty;
        [JsonProperty("conditions")] private List<ConditionData> _conditions = new List<ConditionData>();
        [JsonProperty("effects")] private List<EffectData> _effects = new List<EffectData>();
        [JsonProperty("lines")] private List<LineData> _lines = new List<LineData>();
        [JsonProperty("next")] private string _next = string.Empty;
        [JsonProperty("nextRules")] private List<NextRuleData> _nextRules = new List<NextRuleData>();
        [JsonProperty("feedback")] private string _feedback = string.Empty;

        public string Id => _id;
        public string Text => _text;

        /// <summary>Never offered to the player; used as a node's timeout outcome.</summary>
        public bool Hidden => _hidden;

        /// <summary>In a hub, stays available after being chosen.</summary>
        public bool Repeatable => _repeatable;

        /// <summary>The reference answer from the dataset.</summary>
        public bool Reference => _reference;

        /// <summary>Who says the response lines; empty means the node's speaker.</summary>
        public string Speaker => _speaker;

        /// <summary>World interaction id; only used in roam nodes.</summary>
        public string Target => _target;

        public string Objective => _objective;
        public IReadOnlyList<ConditionData> Conditions => _conditions;
        public IReadOnlyList<EffectData> Effects => _effects;

        /// <summary>The NPC response shown after the choice.</summary>
        public IReadOnlyList<LineData> Lines => _lines;

        public string Next => _next;
        public IReadOnlyList<NextRuleData> NextRules => _nextRules;

        /// <summary>Debrief text: why this decision was good or bad and what would be better.</summary>
        public string Feedback => _feedback;
    }
}
