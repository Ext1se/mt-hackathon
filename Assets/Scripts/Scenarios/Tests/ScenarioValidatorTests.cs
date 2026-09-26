using System.Collections.Generic;
using Game.Scenarios.Core;
using NUnit.Framework;

namespace Game.Scenarios.Tests
{
    public sealed class ScenarioValidatorTests
    {
        private const string MissingTargetJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""nowhere"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string TimedWithoutTimeoutJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""timeLimit"": 5, ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string ConditionalLastEndingJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"", ""conditions"": [ { ""key"": ""flag.x"" } ] } ]
}";

        private const string UnknownOperatorJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""options"": [
    { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"", ""conditions"": [ { ""key"": ""flag.x"", ""op"": ""between"" } ] } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string TargetOutsideRoamJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""target"": ""seat"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        [Test]
        public void Fixture_HasNoErrors()
        {
            List<string> errors = ScenarioValidator.Validate(ScenarioLoader.Deserialize(TestScenarios.ReadFixtureJson()));

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [TestCase(MissingTargetJson, "does not exist")]
        [TestCase(TimedWithoutTimeoutJson, "timeoutOption")]
        [TestCase(ConditionalLastEndingJson, "last ending")]
        [TestCase(UnknownOperatorJson, "unknown operator")]
        [TestCase(TargetOutsideRoamJson, "roam")]
        public void BrokenScenario_IsReported(string json, string expectedFragment)
        {
            List<string> errors = ScenarioValidator.Validate(ScenarioLoader.Deserialize(json));

            Assert.That(errors.Exists(error => error.Contains(expectedFragment)), Is.True, string.Join("\n", errors));
        }

        [Test]
        public void Parse_InvalidScenario_ThrowsWithErrors()
        {
            ScenarioFormatException exception = Assert.Throws<ScenarioFormatException>(() => ScenarioLoader.Parse(MissingTargetJson));

            Assert.That(exception.Errors.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Parse_Fixture_Succeeds()
        {
            Assert.That(ScenarioLoader.Parse(TestScenarios.ReadFixtureJson()).Id, Is.EqualTo("fixture"));
        }
    }
}
