using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>Changes one state value. Its conditions are checked at the moment it is applied.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EffectData
    {
        [JsonProperty("key")] private string _key = string.Empty;
        [JsonProperty("op")] private string _op = EffectOps.Add;
        [JsonProperty("value")] private int _value = 0;
        [JsonProperty("conditions")] private List<ConditionData> _conditions = new List<ConditionData>();

        public string Key => _key;
        public string Op => _op;
        public int Value => _value;
        public IReadOnlyList<ConditionData> Conditions => _conditions;
    }
}
