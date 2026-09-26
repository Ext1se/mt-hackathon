using System.IO;
using UnityEngine;

namespace Game.Scenarios.Tests
{
    /// <summary>Shared access to the engine test fixture.</summary>
    internal static class TestScenarios
    {
        private const string FixtureRelativePath = "Scripts/Scenarios/Tests/Fixtures/fixture_scenario.json";

        public static string ReadFixtureJson()
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, FixtureRelativePath));
        }
    }
}
