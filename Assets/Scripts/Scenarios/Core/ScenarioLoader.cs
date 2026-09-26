using System.Collections.Generic;
using Game.Scenarios.Core.Data;
using Newtonsoft.Json;

namespace Game.Scenarios.Core
{
    /// <summary>Reads scenario JSON. Unknown keys are errors, so typos in hand-written files surface immediately.</summary>
    public static class ScenarioLoader
    {
        private static readonly JsonSerializerSettings s_settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>Deserializes and validates. Throws <see cref="ScenarioFormatException"/> listing every problem.</summary>
        public static ScenarioData Parse(string json)
        {
            ScenarioData data = Deserialize(json);
            List<string> errors = ScenarioValidator.Validate(data);
            if (errors.Count > 0)
            {
                throw new ScenarioFormatException(data.Id, errors);
            }

            return data;
        }

        /// <summary>Deserializes without validation.</summary>
        public static ScenarioData Deserialize(string json)
        {
            ScenarioData data;
            try
            {
                data = JsonConvert.DeserializeObject<ScenarioData>(json, s_settings);
            }
            catch (JsonException exception)
            {
                throw new ScenarioFormatException($"Scenario JSON could not be read: {exception.Message}", exception);
            }

            if (data == null)
            {
                throw new ScenarioFormatException("Scenario JSON is empty.");
            }

            return data;
        }
    }
}
