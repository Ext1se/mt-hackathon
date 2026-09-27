using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>
    /// One passenger's ticket as the terminal prints it. Every field is optional; empty fields are not shown.
    /// All data is synthetic.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TerminalRecordData
    {
        [JsonProperty("conditions")] private List<ConditionData> _conditions = new List<ConditionData>();
        [JsonProperty("name")] private string _name = string.Empty;
        [JsonProperty("birthDate")] private string _birthDate = string.Empty;
        [JsonProperty("document")] private string _document = string.Empty;
        [JsonProperty("ticket")] private string _ticket = string.Empty;
        [JsonProperty("from")] private string _from = string.Empty;
        [JsonProperty("to")] private string _to = string.Empty;
        [JsonProperty("departure")] private string _departure = string.Empty;
        [JsonProperty("arrival")] private string _arrival = string.Empty;
        [JsonProperty("tariff")] private string _tariff = string.Empty;
        [JsonProperty("status")] private string _status = string.Empty;
        [JsonProperty("alert")] private bool _alert = false;
        [JsonProperty("baggage")] private string _baggage = string.Empty;
        [JsonProperty("phone")] private string _phone = string.Empty;
        [JsonProperty("note")] private string _note = string.Empty;

        public IReadOnlyList<ConditionData> Conditions => _conditions;
        public string Name => _name;
        public string BirthDate => _birthDate;
        public string Document => _document;
        public string Ticket => _ticket;
        public string From => _from;
        public string To => _to;
        public string Departure => _departure;
        public string Arrival => _arrival;
        public string Tariff => _tariff;
        public string Status => _status;

        /// <summary>The status needs attention (e.g. the trip has already ended): printed in the accent colour.</summary>
        public bool Alert => _alert;

        public string Baggage => _baggage;
        public string Phone => _phone;
        public string Note => _note;
    }
}
