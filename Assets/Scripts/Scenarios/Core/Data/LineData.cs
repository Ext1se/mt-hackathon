using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>A text variant; the first line whose conditions hold is shown.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class LineData
    {
        [JsonProperty("conditions")] private List<ConditionData> _conditions = new List<ConditionData>();
        [JsonProperty("text")] private string _text = string.Empty;

        public IReadOnlyList<ConditionData> Conditions => _conditions;
        public string Text => _text;
    }
}
