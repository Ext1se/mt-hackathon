using System.Collections.Generic;
using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using NUnit.Framework;

namespace Game.Scenarios.Tests
{
    public sealed class ScenarioLoaderTests
    {
        [Test]
        public void Deserialize_ReadsFixtureFields()
        {
            ScenarioData data = ScenarioLoader.Deserialize(TestScenarios.ReadFixtureJson());

            Assert.That(data.Id, Is.EqualTo("fixture"));
            Assert.That(data.Start, Is.EqualTo("intro"));
            Assert.That(data.Nodes.Count, Is.EqualTo(5));
            Assert.That(data.Variants.Count, Is.EqualTo(2));

            NodeData intro = data.Nodes[0];
            Assert.That(intro.Kind, Is.EqualTo(NodeKinds.Choice));
            Assert.That(intro.TimeLimit, Is.EqualTo(10f));
            Assert.That(intro.Critical, Is.True);
            Assert.That(intro.Hints[0].AfterSeconds, Is.EqualTo(5f));
            Assert.That(intro.Hints[2].AfterSeconds, Is.EqualTo(-1f));

            OptionData good = intro.Options[0];
            Assert.That(good.Reference, Is.True);
            Assert.That(good.Effects[0].Key, Is.EqualTo("loyalty"));
            Assert.That(good.Effects[0].Op, Is.EqualTo(EffectOps.Add));
            Assert.That(good.Effects[0].Value, Is.EqualTo(10));

            ConditionData variantCondition = intro.Lines[0].Conditions[0];
            Assert.That(variantCondition.Op, Is.EqualTo(ConditionOps.Ge));
            Assert.That(variantCondition.Value, Is.EqualTo(1));
            Assert.That(data.Nodes[1].Kind, Is.EqualTo(NodeKinds.Hub));
        }

        [Test]
        public void Deserialize_UnknownKey_Throws()
        {
            const string json = "{ \"id\": \"x\", \"strat\": \"a\" }";

            Assert.Throws<ScenarioFormatException>(() => ScenarioLoader.Deserialize(json));
        }

        [Test]
        public void Deserialize_EmptyText_Throws()
        {
            Assert.Throws<ScenarioFormatException>(() => ScenarioLoader.Deserialize(string.Empty));
        }

        [Test]
        public void Deserialize_Include_AddsPartKeysAndConcatenatesLists()
        {
            const string root = "{ \"id\": \"x\", \"include\": [\"more\"], \"start\": \"a\", \"nodes\": [ { \"id\": \"a\" } ] }";
            Dictionary<string, string> parts = new Dictionary<string, string>
            {
                { "more", "{ \"title\": \"T\", \"nodes\": [ { \"id\": \"b\" } ] }" }
            };

            ScenarioData data = ScenarioLoader.Deserialize(root, name => parts.TryGetValue(name, out string json) ? json : null);

            Assert.That(data.Title, Is.EqualTo("T"));
            Assert.That(data.Nodes.Count, Is.EqualTo(2));
            Assert.That(data.Nodes[0].Id, Is.EqualTo("a"));
            Assert.That(data.Nodes[1].Id, Is.EqualTo("b"));
        }

        [Test]
        public void Deserialize_IncludeKeyInBothFiles_Throws()
        {
            const string root = "{ \"id\": \"x\", \"include\": [\"more\"] }";

            Assert.Throws<ScenarioFormatException>(() => ScenarioLoader.Deserialize(root, name => "{ \"id\": \"y\" }"));
        }

        [Test]
        public void Deserialize_IncludeMissingPart_Throws()
        {
            const string root = "{ \"id\": \"x\", \"include\": [\"more\"] }";

            Assert.Throws<ScenarioFormatException>(() => ScenarioLoader.Deserialize(root, name => null));
            Assert.Throws<ScenarioFormatException>(() => ScenarioLoader.Deserialize(root));
        }

        [Test]
        public void Deserialize_UnknownKeyInPart_Throws()
        {
            const string root = "{ \"id\": \"x\", \"include\": [\"more\"] }";

            Assert.Throws<ScenarioFormatException>(() => ScenarioLoader.Deserialize(root, name => "{ \"strat\": \"a\" }"));
        }
    }
}
