using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>A hidden scenario variable with a clamped range.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class VariableData
    {
        [JsonProperty("key")] private string _key = string.Empty;
        [JsonProperty("initial")] private int _initial = 0;
        [JsonProperty("min")] private int _min = 0;
        [JsonProperty("max")] private int _max = 100;

        public string Key => _key;
        public int Initial => _initial;
        public int Min => _min;
        public int Max => _max;
    }
}
