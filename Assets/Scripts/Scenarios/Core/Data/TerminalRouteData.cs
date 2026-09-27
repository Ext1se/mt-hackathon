using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>The terminal's route tab: the train's stops in order and where the train is now.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TerminalRouteData
    {
        [JsonProperty("stations")] private List<TerminalStationData> _stations = new List<TerminalStationData>();
        [JsonProperty("status")] private string _status = string.Empty;

        public IReadOnlyList<TerminalStationData> Stations => _stations;

        /// <summary>Printed on the stretch the train is on, right after the last passed stop.</summary>
        public string Status => _status;

        /// <summary>Index of the last passed stop; -1 before the first one.</summary>
        public int LastPassedIndex
        {
            get
            {
                int last = -1;
                for (int i = 0; i < _stations.Count; i++)
                {
                    if (_stations[i].Passed)
                    {
                        last = i;
                    }
                }

                return last;
            }
        }
    }
}
