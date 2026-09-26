using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>An interrupt that fires once when its conditions hold; limited to one hub when Hub is set.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TriggerData
    {
        [JsonProperty("id")] private string _id = string.Empty;
        [JsonProperty("hub")] private string _hub = string.Empty;
        [JsonProperty("conditions")] private List<ConditionData> _conditions = new List<ConditionData>();
        [JsonProperty("node")] private string _node = string.Empty;

        public string Id => _id;
        public string Hub => _hub;
        public IReadOnlyList<ConditionData> Conditions => _conditions;
        public string Node => _node;
    }
}
