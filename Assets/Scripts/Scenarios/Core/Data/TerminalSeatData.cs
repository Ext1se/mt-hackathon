using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>A sold seat of the terminal: its ticket records, the first whose conditions hold is shown.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TerminalSeatData
    {
        [JsonProperty("seat")] private string _seat = string.Empty;
        [JsonProperty("option")] private string _option = string.Empty;
        [JsonProperty("records")] private List<TerminalRecordData> _records = new List<TerminalRecordData>();

        public string Seat => _seat;

        /// <summary>
        /// Optional node option chosen when the player opens this seat, if the option is available: it applies the
        /// check's effects and debrief without leaving the terminal. Empty for background passengers.
        /// </summary>
        public string Option => _option;

        public IReadOnlyList<TerminalRecordData> Records => _records;
    }
}
