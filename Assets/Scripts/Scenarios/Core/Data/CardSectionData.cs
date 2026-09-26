using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>A headed block of a card: a paragraph, a bulleted list, or both.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CardSectionData
    {
        [JsonProperty("title")] private string _title = string.Empty;
        [JsonProperty("text")] private string _text = string.Empty;
        [JsonProperty("items")] private List<string> _items = new List<string>();

        public string Title => _title;
        public string Text => _text;
        public IReadOnlyList<string> Items => _items;
    }
}
