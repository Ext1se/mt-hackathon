using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>
    /// A hint for one node. A non-negative delay shows it automatically after that many seconds in the node;
    /// -1 shows it only on request. A hint whose conditions fail when it is reached is skipped for good.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class HintData
    {
        [JsonProperty("afterSeconds")] private float _afterSeconds = -1f;
        [JsonProperty("conditions")] private List<ConditionData> _conditions = new List<ConditionData>();
        [JsonProperty("text")] private string _text = string.Empty;

        public float AfterSeconds => _afterSeconds;
        public IReadOnlyList<ConditionData> Conditions => _conditions;
        public string Text => _text;
    }
}
