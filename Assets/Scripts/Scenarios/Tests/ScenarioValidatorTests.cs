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

        private const string HoldNotLastJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""look"": [ { ""target"": ""bag"", ""hold"": true }, { ""target"": ""seat"", ""seconds"": 1 } ],
    ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string EmptyCardJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""card"": { ""title"": ""Card"", ""sections"": [ { ""title"": ""Problem"" } ] },
    ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string TerminalUnknownOptionJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""terminal"": { ""defaultCar"": ""7"", ""cars"": [ { ""id"": ""7"", ""rows"": 2, ""columns"": [ ""A"", ""B"" ],
    ""seats"": [ { ""seat"": ""1A"", ""option"": ""missing"", ""records"": [ { ""name"": ""X"" } ] } ] } ] },
  ""nodes"": [ { ""id"": ""a"", ""terminal"": true,
    ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string TerminalWithoutCloseJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""terminal"": { ""defaultCar"": ""9"", ""cars"": [ { ""id"": ""7"", ""rows"": 2, ""columns"": [ ""A"", ""B"" ],
    ""seats"": [ { ""seat"": ""3A"", ""option"": ""go"", ""records"": [ { ""name"": ""X"" } ] } ] } ] },
  ""nodes"": [ { ""id"": ""a"", ""terminal"": true,
    ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string TerminalMissingJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""terminal"": true,
    ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string LongHudMeterJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""variables"": [ { ""key"": ""var.panic"", ""min"": 0, ""max"": 10, ""hud"": true } ],
  ""nodes"": [ { ""id"": ""a"", ""options"": [ { ""id"": ""go"", ""text"": ""Go"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string BreathingBrokenJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""timeLimit"": 5, ""timeoutOption"": ""good"",
    ""breathing"": { ""cycles"": 0, ""passScore"": 1.5, ""success"": ""good"", ""fail"": ""missing"" },
    ""options"": [ { ""id"": ""good"", ""text"": ""Good"", ""next"": ""@end"" } ] } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string BreathingHiddenResultJson = @"{
  ""id"": ""broken"", ""start"": ""a"",
  ""nodes"": [ { ""id"": ""a"", ""card"": { ""sections"": [ { ""title"": ""Card"", ""text"": ""Text"" } ] },
    ""breathing"": { ""success"": ""good"", ""fail"": ""poor"" },
    ""options"": [ { ""id"": ""good"", ""text"": ""Good"", ""next"": ""@end"" },
      { ""id"": ""poor"", ""hidden"": true, ""text"": ""Poor"", ""next"": ""@end"" } ] } ],
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
        [TestCase(HoldNotLastJson, "only the last look step may hold")]
        [TestCase(EmptyCardJson, "a card section needs a title and a text or items")]
        [TestCase(LongHudMeterJson, "HUD meter")]
        [TestCase(TerminalUnknownOptionJson, "unknown option 'missing'")]
        [TestCase(TerminalWithoutCloseJson, "not on the map")]
        [TestCase(TerminalWithoutCloseJson, "not bound to a seat")]
        [TestCase(TerminalMissingJson, "the scenario has none")]
        [TestCase(TerminalWithoutCloseJson, "default car '9' does not exist")]
        [TestCase(BreathingBrokenJson, "a breathing node cannot be timed")]
        [TestCase(BreathingBrokenJson, "cycles >= 1")]
        [TestCase(BreathingBrokenJson, "passScore in (0, 1]")]
        [TestCase(BreathingBrokenJson, "breathing result option 'missing' does not exist")]
        [TestCase(BreathingHiddenResultJson, "breathing result option 'poor' must be visible")]
        [TestCase(BreathingHiddenResultJson, "without a card or the terminal")]
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
