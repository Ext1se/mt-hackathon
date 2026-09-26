using System;
using System.IO;
using Game.Scenarios.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Game.Scenarios.Presentation.Results
{
    /// <summary>Writes each result as an indented camelCase JSON file; the same shape the backend API will accept.</summary>
    public sealed class LocalResultSink : IScenarioResultSink
    {
        private static readonly JsonSerializerSettings s_settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented
        };

        private readonly string _directory;

        public LocalResultSink(string directory)
        {
            _directory = directory;
        }

        public string Save(ScenarioResult result)
        {
            Directory.CreateDirectory(_directory);
            string fileName = $"{result.ScenarioId}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json";
            string path = Path.Combine(_directory, fileName);
            File.WriteAllText(path, JsonConvert.SerializeObject(result, s_settings));
            return path;
        }
    }
}
