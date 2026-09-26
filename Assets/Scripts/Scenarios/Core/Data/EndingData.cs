using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>A scenario outcome; the first ending whose conditions hold is chosen.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EndingData
    {
        [JsonProperty("id")] private string _id = string.Empty;
        [JsonProperty("title")] private string _title = string.Empty;
        [JsonProperty("conditions")] private List<ConditionData> _conditions = new List<ConditionData>();
        [JsonProperty("lines")] private List<LineData> _lines = new List<LineData>();

        public string Id => _id;
        public string Title => _title;
        public IReadOnlyList<ConditionData> Conditions => _conditions;
        public IReadOnlyList<LineData> Lines => _lines;
    }
}
