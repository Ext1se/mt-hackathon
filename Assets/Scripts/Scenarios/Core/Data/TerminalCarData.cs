using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>One car of the terminal: its seat map and sold seats. Seats missing from <see cref="Seats"/> are free.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TerminalCarData
    {
        [JsonProperty("id")] private string _id = string.Empty;
        [JsonProperty("title")] private string _title = string.Empty;
        [JsonProperty("rows")] private int _rows = 0;
        [JsonProperty("columns")] private List<string> _columns = new List<string>();
        [JsonProperty("aisleAfter")] private int _aisleAfter = 2;
        [JsonProperty("seats")] private List<TerminalSeatData> _seats = new List<TerminalSeatData>();

        /// <summary>The car number as the car picker prints it.</summary>
        public string Id => _id;

        /// <summary>Shown in the status bar after the terminal title, e.g. "Car 7 - Comfort".</summary>
        public string Title => _title;

        public int Rows => _rows;

        /// <summary>Seat letters across the car, left to right.</summary>
        public IReadOnlyList<string> Columns => _columns;

        /// <summary>How many columns stand left of the aisle.</summary>
        public int AisleAfter => _aisleAfter;

        public IReadOnlyList<TerminalSeatData> Seats => _seats;

        public TerminalSeatData FindSeat(string seatId)
        {
            for (int i = 0; i < _seats.Count; i++)
            {
                if (_seats[i].Seat == seatId)
                {
                    return _seats[i];
                }
            }

            return null;
        }

        /// <summary>True when <paramref name="seatId"/> is one of the map's seats.</summary>
        public bool IsOnMap(string seatId)
        {
            for (int row = 1; row <= _rows; row++)
            {
                for (int column = 0; column < _columns.Count; column++)
                {
                    if (TerminalData.SeatId(row, _columns[column]) == seatId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
