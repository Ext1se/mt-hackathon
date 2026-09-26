using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>
    /// Compares one state value with a constant. Conditions with the same group are combined with AND,
    /// different groups with OR. The defaults ("ge", 1) make {"key": "flag.x"} mean "flag x is set".
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ConditionData
    {
        [JsonProperty("key")] private string _key = string.Empty;
        [JsonProperty("op")] private string _op = ConditionOps.Ge;
        [JsonProperty("value")] private int _value = 1;
        [JsonProperty("group")] private int _group = 0;

        public string Key => _key;
        public string Op => _op;
        public int Value => _value;
        public int Group => _group;
    }
}
