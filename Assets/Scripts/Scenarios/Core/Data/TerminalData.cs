using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Scenarios.Core.Data
{
    /// <summary>
    /// The ticket terminal (MMT) the conductor carries: the train's cars, each with a seat map and the passenger records
    /// of its sold seats, and the train's route. It opens on <see cref="DefaultCar"/>, the conductor's own car.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TerminalData
    {
        [JsonProperty("title")] private string _title = string.Empty;
        [JsonProperty("subtitle")] private string _subtitle = string.Empty;
        [JsonProperty("clock")] private string _clock = string.Empty;
        [JsonProperty("prompt")] private string _prompt = string.Empty;
        [JsonProperty("defaultCar")] private string _defaultCar = string.Empty;
        [JsonProperty("cars")] private List<TerminalCarData> _cars = new List<TerminalCarData>();
        [JsonProperty("route")] private TerminalRouteData _route;

        public string Title => _title;
        public string Subtitle => _subtitle;

        /// <summary>Time in the status bar; the scenario's story time, not the real clock.</summary>
        public string Clock => _clock;

        /// <summary>Shown in place of a passenger record until a seat is picked.</summary>
        public string Prompt => _prompt;

        /// <summary>Id of the car the terminal opens on; empty means the first car.</summary>
        public string DefaultCarId => _defaultCar;

        /// <summary>Cars in train order, as the car picker lists them.</summary>
        public IReadOnlyList<TerminalCarData> Cars => _cars;

        /// <summary>Optional route tab; null hides the tab.</summary>
        public TerminalRouteData Route => _route;

        /// <summary>The conductor's own car: the one named by defaultCar, else the first; null without cars.</summary>
        public TerminalCarData DefaultCar
        {
            get
            {
                TerminalCarData car = FindCar(_defaultCar);
                return car != null ? car : _cars.Count > 0 ? _cars[0] : null;
            }
        }

        /// <summary>The seat id as the map prints it: row number, then the column letter.</summary>
        public static string SeatId(int row, string column)
        {
            return row + column;
        }

        public TerminalCarData FindCar(string carId)
        {
            for (int i = 0; i < _cars.Count; i++)
            {
                if (_cars[i].Id == carId)
                {
                    return _cars[i];
                }
            }

            return null;
        }

        /// <summary>True when opening some seat of any car chooses <paramref name="optionId"/>.</summary>
        public bool IsBoundToSeat(string optionId)
        {
            for (int i = 0; i < _cars.Count; i++)
            {
                IReadOnlyList<TerminalSeatData> seats = _cars[i].Seats;
                for (int j = 0; j < seats.Count; j++)
                {
                    if (seats[j].Option == optionId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
