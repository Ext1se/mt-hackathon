using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>A hidden story variant picked at start with the given weight.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class VariantData
    {
        [JsonProperty("id")] private string _id = string.Empty;
        [JsonProperty("weight")] private int _weight = 1;
        [JsonProperty("title")] private string _title = string.Empty;

        public string Id => _id;
        public int Weight => _weight;
        public string Title => _title;
    }
}
