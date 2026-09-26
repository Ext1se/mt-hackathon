using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>One step of a camera look sequence on node entry: turn to an actor or a world target for a while.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class LookStepData
    {
        [JsonProperty("target")] private string _target = string.Empty;
        [JsonProperty("seconds")] private float _seconds = 1.5f;
        [JsonProperty("hold")] private bool _hold;

        /// <summary>Actor id or interaction target id.</summary>
        public string Target => _target;

        public float Seconds => _seconds;

        /// <summary>The camera stays on this target until the node is left; only the last step may hold.</summary>
        public bool Hold => _hold;
    }
}
