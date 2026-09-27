using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>Root of a scenario JSON file.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ScenarioData
    {
        [JsonProperty("id")] private string _id = string.Empty;
        [JsonProperty("title")] private string _title = string.Empty;
        [JsonProperty("summary")] private string _summary = string.Empty;
        [JsonProperty("intro")] private string _intro = string.Empty;
        [JsonProperty("loyalty")] private int _loyalty = 50;
        [JsonProperty("safety")] private int _safety = 50;
        [JsonProperty("variables")] private List<VariableData> _variables = new List<VariableData>();
        [JsonProperty("variants")] private List<VariantData> _variants = new List<VariantData>();
        [JsonProperty("start")] private string _start = string.Empty;
        [JsonProperty("nodes")] private List<NodeData> _nodes = new List<NodeData>();
        [JsonProperty("triggers")] private List<TriggerData> _triggers = new List<TriggerData>();
        [JsonProperty("endings")] private List<EndingData> _endings = new List<EndingData>();
        [JsonProperty("terminal")] private TerminalData _terminal;

        public string Id => _id;
        public string Title => _title;
        public string Summary => _summary;

        /// <summary>
        /// General description for the intro screen: the situation and the player's role, without the story branches
        /// or the right decisions. Empty means the summary is shown instead.
        /// </summary>
        public string Intro => _intro;

        public int Loyalty => _loyalty;
        public int Safety => _safety;
        public IReadOnlyList<VariableData> Variables => _variables;
        public IReadOnlyList<VariantData> Variants => _variants;
        public string Start => _start;
        public IReadOnlyList<NodeData> Nodes => _nodes;
        public IReadOnlyList<TriggerData> Triggers => _triggers;
        public IReadOnlyList<EndingData> Endings => _endings;

        /// <summary>
        /// Optional ticket terminal (MMT) the conductor carries: opened from anywhere while roaming, read-only unless
        /// the current node shows it (<see cref="NodeData.ShowsTerminal"/>). Null when the scenario has none.
        /// </summary>
        public TerminalData Terminal => _terminal;
    }
}
