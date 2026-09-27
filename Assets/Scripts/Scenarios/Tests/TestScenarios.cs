using System.IO;
using Game.Scenarios.Core;
using UnityEngine;

namespace Game.Scenarios.Tests
{
    /// <summary>Shared access to the engine test fixture.</summary>
    internal static class TestScenarios
    {
        private const string FixtureRelativePath = "Scripts/Scenarios/Tests/Fixtures/fixture_scenario.json";
        private const string PartsRelativeFolder = "Data/Scenarios/Resources";

        public static string ReadFixtureJson()
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, FixtureRelativePath));
        }

        /// <summary>Reads a scenario part from disk the way the runtime reads it from Resources; null when missing.</summary>
        public static string ReadScenarioPart(string name)
        {
            string path = Path.Combine(Application.dataPath, PartsRelativeFolder, ScenarioLoader.PartsResourcesFolder, name + ".json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }
}
