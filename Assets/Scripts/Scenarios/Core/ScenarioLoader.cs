using System;
using System.Collections.Generic;
using Game.Scenarios.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Scenarios.Core
{
    /// <summary>
    /// Reads scenario JSON. Unknown keys are errors, so typos in hand-written files surface immediately.
    /// A scenario may be split into parts: the root lists part names in "include", each part is a JSON object whose
    /// keys are added to the root before deserialization. A key present in both is an error unless both values are
    /// lists, which are concatenated (root items first). Parts cannot include other parts.
    /// </summary>
    public static class ScenarioLoader
    {
        /// <summary>Folder under a Resources folder that holds scenario parts, file name = part name.</summary>
        public const string PartsResourcesFolder = "ScenarioParts";

        private const string IncludeKey = "include";

        private static readonly JsonSerializerSettings s_settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>Deserializes and validates. Throws <see cref="ScenarioFormatException"/> listing every problem.</summary>
        public static ScenarioData Parse(string json)
        {
            return Parse(json, null);
        }

        /// <summary>
        /// Deserializes and validates a scenario whose parts are read by <paramref name="readPart"/>
        /// (part name to JSON text, null when the part does not exist).
        /// </summary>
        public static ScenarioData Parse(string json, Func<string, string> readPart)
        {
            ScenarioData data = Deserialize(json, readPart);
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
            return Deserialize(json, null);
        }

        /// <summary>Deserializes without validation, merging the parts listed in "include".</summary>
        public static ScenarioData Deserialize(string json, Func<string, string> readPart)
        {
            ScenarioData data;
            try
            {
                JObject composed = Compose(json, readPart);
                data = composed != null
                    ? composed.ToObject<ScenarioData>(JsonSerializer.Create(s_settings))
                    : JsonConvert.DeserializeObject<ScenarioData>(json, s_settings);
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

        /// <summary>Returns the root merged with its parts, or null when the scenario has no "include".</summary>
        private static JObject Compose(string json, Func<string, string> readPart)
        {
            // Single-file scenarios skip the JObject pass, so their errors keep the plain deserializer messages.
            if (string.IsNullOrEmpty(json) || !json.Contains("\"" + IncludeKey + "\""))
            {
                return null;
            }

            JObject root = JObject.Parse(json);
            JToken include = root[IncludeKey];
            if (include == null)
            {
                return null;
            }

            root.Remove(IncludeKey);
            if (!(include is JArray names))
            {
                throw new ScenarioFormatException($"\"{IncludeKey}\" must be a list of part names.");
            }

            if (readPart == null)
            {
                throw new ScenarioFormatException($"The scenario has \"{IncludeKey}\", but no part reader was given.");
            }

            foreach (JToken nameToken in names)
            {
                if (nameToken.Type != JTokenType.String)
                {
                    throw new ScenarioFormatException($"\"{IncludeKey}\" must be a list of part names.");
                }

                string name = (string)nameToken;
                string partJson = readPart(name);
                if (partJson == null)
                {
                    throw new ScenarioFormatException($"Scenario part '{name}' was not found.");
                }

                MergePart(root, JObject.Parse(partJson), name);
            }

            return root;
        }

        private static void MergePart(JObject root, JObject part, string name)
        {
            foreach (JProperty property in part.Properties())
            {
                if (property.Name == IncludeKey)
                {
                    throw new ScenarioFormatException($"Scenario part '{name}' cannot include other parts.");
                }

                JToken existing = root[property.Name];
                if (existing == null)
                {
                    root.Add(property.Name, property.Value);
                }
                else if (existing is JArray target && property.Value is JArray extra)
                {
                    foreach (JToken item in extra)
                    {
                        target.Add(item);
                    }
                }
                else
                {
                    throw new ScenarioFormatException(
                        $"Key '{property.Name}' is defined both in the scenario and in part '{name}'.");
                }
            }
        }
    }
}
