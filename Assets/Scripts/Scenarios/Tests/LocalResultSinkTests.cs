using System;
using System.IO;
using Game.Scenarios.Core;
using Game.Scenarios.Presentation.Results;
using NUnit.Framework;

namespace Game.Scenarios.Tests
{
    public sealed class LocalResultSinkTests
    {
        [Test]
        public void Save_WritesCamelCaseJson()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ScenarioSinkTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                ScenarioSession session = new ScenarioSession(ScenarioLoader.Parse(TestScenarios.ReadFixtureJson()),
                    new System.Random(0), "A");
                session.Start();
                session.Choose("good");
                session.Tick(31f);

                string path = new LocalResultSink(directory).Save(session.Result);
                string json = File.ReadAllText(path);

                StringAssert.Contains("\"scenarioId\": \"fixture\"", json);
                StringAssert.Contains("\"endingId\": \"calm\"", json);
                StringAssert.Contains("\"optionId\": \"good\"", json);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
