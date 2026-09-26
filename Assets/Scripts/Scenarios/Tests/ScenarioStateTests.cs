using System.Collections.Generic;
using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using NUnit.Framework;

namespace Game.Scenarios.Tests
{
    public sealed class ScenarioStateTests
    {
        [Test]
        public void Get_UnknownKey_ReturnsZero()
        {
            ScenarioState state = new ScenarioState();

            Assert.That(state.Get("flag.unknown"), Is.EqualTo(0));
        }

        [Test]
        public void Set_WithRange_Clamps()
        {
            ScenarioState state = new ScenarioState();
            state.DefineRange("var.panic", 0, 3);

            state.Set("var.panic", 7);
            Assert.That(state.Get("var.panic"), Is.EqualTo(3));

            state.Add("var.panic", -10);
            Assert.That(state.Get("var.panic"), Is.EqualTo(0));
        }

        [Test]
        public void IsMet_EmptyConditions_IsTrue()
        {
            Assert.That(ConditionEvaluator.IsMet(new List<ConditionData>(), new ScenarioState()), Is.True);
        }

        [Test]
        public void IsMet_DefaultOperatorMeansFlagIsSet()
        {
            IReadOnlyList<ConditionData> conditions = ConditionsOf("[ { \"key\": \"flag.reported\" } ]");
            ScenarioState state = new ScenarioState();

            Assert.That(ConditionEvaluator.IsMet(conditions, state), Is.False);
            state.Set("flag.reported", 1);
            Assert.That(ConditionEvaluator.IsMet(conditions, state), Is.True);
        }

        [Test]
        public void IsMet_SameGroupIsAnd_DifferentGroupsAreOr()
        {
            IReadOnlyList<ConditionData> conditions = ConditionsOf(
                "[ { \"key\": \"a\", \"group\": 0 }, { \"key\": \"b\", \"group\": 0 }, { \"key\": \"c\", \"group\": 1 } ]");
            ScenarioState state = new ScenarioState();

            state.Set("a", 1);
            Assert.That(ConditionEvaluator.IsMet(conditions, state), Is.False, "a alone is not enough for group 0");

            state.Set("b", 1);
            Assert.That(ConditionEvaluator.IsMet(conditions, state), Is.True, "group 0 holds");

            state.Set("a", 0);
            state.Set("c", 1);
            Assert.That(ConditionEvaluator.IsMet(conditions, state), Is.True, "group 1 holds");
        }

        [TestCase("eq", 2, 2, true)]
        [TestCase("ne", 2, 2, false)]
        [TestCase("gt", 3, 2, true)]
        [TestCase("ge", 2, 2, true)]
        [TestCase("lt", 1, 2, true)]
        [TestCase("le", 3, 2, false)]
        public void Compare_Operators(string op, int actual, int expected, bool result)
        {
            Assert.That(ConditionEvaluator.Compare(actual, op, expected), Is.EqualTo(result));
        }

        [Test]
        public void Apply_AddSetAndConditionalEffects()
        {
            IReadOnlyList<EffectData> effects = EffectsOf(
                "[ { \"key\": \"loyalty\", \"value\": 10 }," +
                "  { \"key\": \"flag.x\", \"op\": \"set\", \"value\": 1 }," +
                "  { \"key\": \"safety\", \"value\": -5, \"conditions\": [ { \"key\": \"flag.x\" } ] }," +
                "  { \"key\": \"var.never\", \"value\": 1, \"conditions\": [ { \"key\": \"flag.y\" } ] } ]");
            ScenarioState state = new ScenarioState();
            state.Set("loyalty", 50);
            state.Set("safety", 50);
            List<AppliedEffect> applied = new List<AppliedEffect>();

            EffectApplier.Apply(effects, state, applied);

            Assert.That(state.Get("loyalty"), Is.EqualTo(60));
            Assert.That(state.Get("flag.x"), Is.EqualTo(1));
            Assert.That(state.Get("safety"), Is.EqualTo(45), "later effects see earlier ones");
            Assert.That(state.Get("var.never"), Is.EqualTo(0));
            Assert.That(applied.Count, Is.EqualTo(3));
            Assert.That(applied[2].Key, Is.EqualTo("safety"));
            Assert.That(applied[2].Delta, Is.EqualTo(-5));
        }

        [Test]
        public void Apply_ClampedDeltaIsRecordedAsActualChange()
        {
            IReadOnlyList<EffectData> effects = EffectsOf("[ { \"key\": \"safety\", \"value\": -80 } ]");
            ScenarioState state = new ScenarioState();
            state.DefineRange("safety", 0, 100);
            state.Set("safety", 50);
            List<AppliedEffect> applied = new List<AppliedEffect>();

            EffectApplier.Apply(effects, state, applied);

            Assert.That(state.Get("safety"), Is.EqualTo(0));
            Assert.That(applied[0].Delta, Is.EqualTo(-50));
        }

        private static IReadOnlyList<ConditionData> ConditionsOf(string conditionsJson)
        {
            return OptionOf("\"conditions\": " + conditionsJson).Conditions;
        }

        private static IReadOnlyList<EffectData> EffectsOf(string effectsJson)
        {
            return OptionOf("\"effects\": " + effectsJson).Effects;
        }

        private static OptionData OptionOf(string fieldJson)
        {
            string json = "{ \"nodes\": [ { \"id\": \"n\", \"options\": [ { \"id\": \"o\", " + fieldJson + " } ] } ] }";
            return ScenarioLoader.Deserialize(json).Nodes[0].Options[0];
        }
    }
}
