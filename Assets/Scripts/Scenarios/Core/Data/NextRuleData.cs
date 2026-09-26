using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>A conditional jump; the first rule whose conditions hold wins.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class NextRuleData
    {
        [JsonProperty("conditions")] private List<ConditionData> _conditions = new List<ConditionData>();
        [JsonProperty("node")] private string _node = string.Empty;

        public IReadOnlyList<ConditionData> Conditions => _conditions;
        public string Node => _node;
    }
}
