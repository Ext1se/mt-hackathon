using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>One stop of the terminal's route tab.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TerminalStationData
    {
        [JsonProperty("name")] private string _name = string.Empty;
        [JsonProperty("time")] private string _time = string.Empty;
        [JsonProperty("passed")] private bool _passed = false;

        public string Name => _name;

        /// <summary>Arrival and departure as printed, e.g. "arrival 07:56 - departure 07:58".</summary>
        public string Time => _time;

        public bool Passed => _passed;
    }
}
