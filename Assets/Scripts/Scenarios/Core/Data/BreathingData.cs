using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>
    /// A box-breathing mini-game shown instead of the dialogue on a node (see <see cref="BoxBreathing"/>). When it ends,
    /// the node's <see cref="Success"/> or <see cref="Fail"/> option is chosen by the score.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BreathingData
    {
        [JsonProperty("title")] private string _title = string.Empty;
        [JsonProperty("cycles")] private int _cycles = 2;
        [JsonProperty("phaseSeconds")] private float _phaseSeconds = 4f;
        [JsonProperty("passScore")] private float _passScore = 0.7f;
        [JsonProperty("success")] private string _success = string.Empty;
        [JsonProperty("fail")] private string _fail = string.Empty;

        public string Title => _title;
        public int Cycles => _cycles;
        public float PhaseSeconds => _phaseSeconds;

        /// <summary>Lowest score (0..1) that counts as keeping the rhythm.</summary>
        public float PassScore => _passScore;

        /// <summary>Option chosen when the score reaches <see cref="PassScore"/>.</summary>
        public string Success => _success;

        /// <summary>Option chosen when it does not.</summary>
        public string Fail => _fail;
    }
}
