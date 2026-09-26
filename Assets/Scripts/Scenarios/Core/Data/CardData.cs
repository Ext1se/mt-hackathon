using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>
    /// An info panel shown instead of the dialogue on a node, e.g. "Problem" and "Recommended actions" after the first
    /// conversation. The node's options become the panel's buttons.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CardData
    {
        [JsonProperty("title")] private string _title = string.Empty;
        [JsonProperty("sections")] private List<CardSectionData> _sections = new List<CardSectionData>();

        public string Title => _title;
        public IReadOnlyList<CardSectionData> Sections => _sections;
    }
}
