using System.Collections.Generic;
using Game.Scenarios.Core;
using NUnit.Framework;

namespace Game.Scenarios.Tests
{
    public sealed class ScenarioSessionTests
    {
        private const string SignalJson = @"{
  ""id"": ""signal"", ""start"": ""hub"",
  ""nodes"": [
    { ""id"": ""hub"", ""kind"": ""hub"", ""maxActions"": 5, ""exitNext"": ""@end"",
      ""options"": [ { ""id"": ""open"", ""text"": ""Open menu"", ""free"": true, ""repeatable"": true, ""next"": ""menu"" },
                   { ""id"": ""wait"", ""text"": ""Wait"", ""next"": ""@end"" } ] },
    { ""id"": ""menu"",
      ""options"": [ { ""id"": ""talk"", ""text"": ""Talk"", ""next"": ""menu"" },
                   { ""id"": ""back"", ""text"": ""Back"", ""free"": true, ""repeatable"": true, ""next"": ""@hub"" } ] },
    { ""id"": ""event"", ""options"": [ { ""id"": ""ok"", ""text"": ""Ok"", ""next"": ""@return"" } ] }
  ],
  ""triggers"": [ { ""id"": ""crossed"", ""hub"": ""hub"", ""conditions"": [ { ""key"": ""flag.crossed"" } ], ""node"": ""event"" } ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        private const string RevisitJson = @"{
  ""id"": ""revisit"", ""start"": ""hub"",
  ""nodes"": [
    { ""id"": ""hub"", ""kind"": ""hub"", ""maxActions"": 5, ""exitNext"": ""@end"",
      ""options"": [ { ""id"": ""open"", ""text"": ""Open menu"", ""free"": true, ""repeatable"": true, ""next"": ""menu"",
                       ""done"": [ { ""key"": ""flag.asked"" } ] } ] },
    { ""id"": ""menu"",
      ""options"": [ { ""id"": ""ask"", ""text"": ""Ask"", ""revisit"": true, ""next"": ""menu"",
                       ""conditions"": [ { ""key"": ""flag.asked"", ""op"": ""eq"", ""value"": 0 } ],
                       ""lines"": [ { ""text"": ""Answer"" } ],
                       ""effects"": [ { ""key"": ""loyalty"", ""value"": 10 }, { ""key"": ""flag.asked"", ""op"": ""set"", ""value"": 1 } ] },
                     { ""id"": ""shout"", ""text"": ""Shout"", ""revisit"": true, ""next"": ""menu"",
                       ""conditions"": [ { ""key"": ""flag.asked"", ""op"": ""eq"", ""value"": 0 } ],
                       ""effects"": [ { ""key"": ""flag.asked"", ""op"": ""set"", ""value"": 1 } ] },
                     { ""id"": ""act"", ""text"": ""Act"", ""next"": ""menu"" },
                     { ""id"": ""back"", ""text"": ""Back"", ""free"": true, ""repeatable"": true, ""next"": ""@hub"" } ] }
  ],
  ""endings"": [ { ""id"": ""end"", ""title"": ""End"" } ]
}";

        [TestCase("A", "Intro A")]
        [TestCase("B", "Intro B")]
        public void Start_ForcedVariant_EntersStartNodeWithVariantText(string variant, string expectedText)
        {
            ScenarioSession session = StartFixture(variant);

            Assert.That(session.Current.Node.Id, Is.EqualTo("intro"));
            Assert.That(session.Current.Text, Is.EqualTo(expectedText));
            Assert.That(session.VariantId, Is.EqualTo(variant));
            Assert.That(session.State.Get("variant." + variant), Is.EqualTo(1));
            Assert.That(session.State.Get(ScenarioKeys.Loyalty), Is.EqualTo(50));
        }

        [Test]
        public void Start_RandomVariant_SetsExactlyOneVariantFlag()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                ScenarioSession session = new ScenarioSession(ScenarioLoader.Parse(TestScenarios.ReadFixtureJson()),
                    new System.Random(seed));
                session.Start();

                Assert.That(session.State.Get("variant.A") + session.State.Get("variant.B"), Is.EqualTo(1));
            }
        }

        [Test]
        public void Options_HiddenAndConditionalOptionsAreFiltered()
        {
            CollectionAssert.AreEqual(new[] { "good", "bad" }, OptionIds(StartFixture("A")));
            CollectionAssert.AreEqual(new[] { "good", "bad", "onlyB" }, OptionIds(StartFixture("B")));
        }

        [Test]
        public void Choose_AppliesEffectsAndEntersNextNode()
        {
            ScenarioSession session = StartFixture("A");

            Assert.That(session.Choose("good"), Is.True);

            Assert.That(session.State.Get(ScenarioKeys.Loyalty), Is.EqualTo(60));
            Assert.That(session.State.Get("comp.comm"), Is.EqualTo(1));
            Assert.That(session.Current.Node.Id, Is.EqualTo("hub"));
            Assert.That(session.Decisions[0].Feedback, Is.EqualTo("Well done"));
            Assert.That(session.Decisions[0].IsReference, Is.True);
        }

        [Test]
        public void Enter_MarksNodeVisitedWithoutRecordingAnEffect()
        {
            ScenarioSession session = StartFixture("A");

            Assert.That(session.State.Get(ScenarioKeys.VisitedPrefix + "intro"), Is.EqualTo(1));
            Assert.That(session.State.Get(ScenarioKeys.VisitedPrefix + "hub"), Is.EqualTo(0));

            session.Choose("good");

            Assert.That(session.State.Get(ScenarioKeys.VisitedPrefix + "hub"), Is.EqualTo(1));
            Assert.That(DeltaOf(session.Decisions[0].Effects, ScenarioKeys.VisitedPrefix + "hub"), Is.EqualTo(0));
        }

        [Test]
        public void Choose_EffectsAreClampedToRanges()
        {
            ScenarioSession session = StartFixture("A");

            session.Choose("bad");

            Assert.That(session.State.Get(ScenarioKeys.Safety), Is.EqualTo(0));
            Assert.That(session.State.Get("var.panic"), Is.EqualTo(3));
            Assert.That(DeltaOf(session.Decisions[0].Effects, ScenarioKeys.Safety), Is.EqualTo(-50));
        }

        [Test]
        public void Choose_UnavailableOption_ReturnsFalseAndKeepsNode()
        {
            ScenarioSession session = StartFixture("A");

            Assert.That(session.Choose("freeze"), Is.False, "hidden");
            Assert.That(session.Choose("onlyB"), Is.False, "condition fails");
            Assert.That(session.Choose("missing"), Is.False, "unknown");
            Assert.That(session.Current.Node.Id, Is.EqualTo("intro"));
        }

        [Test]
        public void Tick_Timeout_ResolvesTimeoutOptionWithSpeedPenalty()
        {
            ScenarioSession session = StartFixture("A");

            session.Tick(10.5f);

            Assert.That(session.Current.Node.Id, Is.EqualTo("hub"));
            Assert.That(session.State.Get("var.panic"), Is.EqualTo(1));
            Assert.That(session.State.Get(ScenarioKeys.Speed), Is.EqualTo(-1));
            Assert.That(session.Decisions[0].OptionId, Is.EqualTo("freeze"));
            Assert.That(session.Decisions[0].TimedOut, Is.True);
        }

        [Test]
        public void Tick_TimersDisabled_NeverTimesOut()
        {
            ScenarioSession session = StartFixture("A");
            session.TimersEnabled = false;

            session.Tick(100f);

            Assert.That(session.Current.Node.Id, Is.EqualTo("intro"));
            Assert.That(session.TimeLimit, Is.EqualTo(0f));
            session.Choose("good");
            session.Tick(100f);
            Assert.That(session.Current.Node.Id, Is.EqualTo("hub"), "hub timer is off too");
        }

        [Test]
        public void Choose_ReferenceOnCriticalNode_GivesSpeedBonus()
        {
            ScenarioSession session = StartFixture("A");

            session.Choose("good");

            Assert.That(session.State.Get(ScenarioKeys.Speed), Is.EqualTo(1));
        }

        [Test]
        public void Hints_AutoAfterDelay_ConditionalSkipped_RequestedCounted()
        {
            ScenarioSession session = StartFixture("A");
            List<string> shown = new List<string>();
            session.HintShown += shown.Add;

            session.Tick(5.5f);
            string requested = session.RequestHint();
            string none = session.RequestHint();
            session.Tick(1f);

            CollectionAssert.AreEqual(new[] { "Auto hint", "Requested hint" }, shown);
            Assert.That(requested, Is.EqualTo("Requested hint"));
            Assert.That(none, Is.Null);

            session.Choose("good");
            session.Tick(31f);
            Assert.That(session.Result.HintsUsed, Is.EqualTo(1));
        }

        [Test]
        public void Hub_TakenOptionsDisappearAndCountDown()
        {
            ScenarioSession session = StartFixture("A");
            session.Choose("good");
            Assert.That(session.Current.HubActionsLeft, Is.EqualTo(3));

            session.Choose("water");

            Assert.That(session.Current.Node.Id, Is.EqualTo("hub"));
            CollectionAssert.AreEqual(new[] { "report", "ask" }, OptionIds(session));
            Assert.That(session.Current.HubActionsLeft, Is.EqualTo(2));
        }

        [Test]
        public void Hub_ResponseTextAndSpeakerComeFromOption()
        {
            ScenarioSession session = StartFixture("A");
            session.Choose("good");
            ChoiceOutcome outcome = null;
            session.ChoiceResolved += result => outcome = result;

            session.Choose("ask");

            Assert.That(outcome.ResponseText, Is.EqualTo("Answer A"));
            Assert.That(outcome.Speaker, Is.EqualTo("patient"));
        }

        [Test]
        public void Hub_ConditionalOptionAppearsAfterFlag()
        {
            ScenarioSession session = StartFixture("A");
            session.Choose("good");

            session.Choose("report");

            CollectionAssert.AreEqual(new[] { "ask", "water", "secret" }, OptionIds(session));
        }

        [Test]
        public void Trigger_InterruptsHubAndReturnsToIt()
        {
            ScenarioSession session = StartFixture("A");
            session.Choose("good");
            session.Choose("water");

            session.Choose("ask");
            Assert.That(session.Current.Node.Id, Is.EqualTo("event"));

            session.Choose("calm");
            Assert.That(session.Current.Node.Id, Is.EqualTo("hub"));
            CollectionAssert.AreEqual(new[] { "report" }, OptionIds(session));
        }

        [Test]
        public void Signal_WhileRoamingHub_FiresTriggerAndReturnsToHub()
        {
            ScenarioSession session = StartJson(SignalJson);

            Assert.That(session.Signal("flag.crossed"), Is.True);
            Assert.That(session.Current.Node.Id, Is.EqualTo("event"));

            session.Choose("ok");
            Assert.That(session.Current.Node.Id, Is.EqualTo("hub"));
        }

        [Test]
        public void Signal_InsideMenu_WaitsForTheWayBackToHub()
        {
            ScenarioSession session = StartJson(SignalJson);
            session.Choose("open");

            Assert.That(session.Signal("flag.crossed"), Is.False);
            session.Choose("talk");
            Assert.That(session.Current.Node.Id, Is.EqualTo("menu"), "the conversation is not interrupted");

            session.Choose("back");
            Assert.That(session.Current.Node.Id, Is.EqualTo("event"));
        }

        [Test]
        public void Signal_DuringAnotherInterrupt_FiresOnReturnToHub()
        {
            ScenarioSession session = StartFixture("A");
            session.Choose("good");
            session.Choose("water");
            session.Choose("ask");
            Assert.That(session.Current.Node.Id, Is.EqualTo("event"));

            Assert.That(session.Signal("flag.crossed"), Is.False);
            session.Choose("calm");

            Assert.That(session.Current.Node.Id, Is.EqualTo("event_signal"));
        }

        [Test]
        public void Trigger_TimeoutReturnsWithoutSpendingHubTime()
        {
            ScenarioSession session = StartFixture("A");
            session.Choose("good");
            session.Choose("water");
            session.Choose("ask");
            Assert.That(session.TimeLeft, Is.EqualTo(5f).Within(0.001f));

            session.Tick(5.5f);

            Assert.That(session.Current.Node.Id, Is.EqualTo("hub"));
            Assert.That(session.TimeLeft, Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void Hub_ExhaustedHubUsesExitRulesAndOnEnterEffects()
        {
            ScenarioSession session = StartFixture("A");
            session.Choose("good");
            session.Choose("water");
            session.Choose("ask");
            session.Choose("calm");

            session.Choose("report");

            Assert.That(session.Current.Node.Id, Is.EqualTo("handover"));
            Assert.That(session.State.Get(ScenarioKeys.Safety), Is.EqualTo(55));
            DecisionRecord consequence = session.Decisions[session.Decisions.Count - 1];
            Assert.That(consequence.IsConsequence, Is.True);
            Assert.That(consequence.Feedback, Is.EqualTo("Handover done"));
        }

        [Test]
        public void HubTimer_ExitsToFallbackAndEnds()
        {
            ScenarioSession session = StartFixture("A");
            int endedCount = 0;
            session.Ended += result => endedCount++;
            session.Choose("good");

            session.Tick(31f);

            Assert.That(session.IsRunning, Is.False);
            Assert.That(endedCount, Is.EqualTo(1));
            Assert.That(session.Result.EndingId, Is.EqualTo("calm"));
            Assert.That(session.Result.EndingText, Is.EqualTo("Calm A"));
            Assert.That(session.Result.Loyalty, Is.EqualTo(60));
            Assert.That(session.Result.Competencies["comp.comm"], Is.EqualTo(1));
            Assert.That(session.Choose("report"), Is.False, "finished sessions ignore input");
        }

        [Test]
        public void Ending_FirstMatchingWins()
        {
            ScenarioSession session = StartFixture("A");
            session.Choose("bad");

            session.Tick(31f);

            Assert.That(session.Result.EndingId, Is.EqualTo("panic"));
            Assert.That(session.Result.SafetyGrade, Is.EqualTo(ScaleGrade.Fail));
        }

        [Test]
        public void Revisit_ChosenOptionStaysListedAsSeen_OthersFollowTheirRules()
        {
            ScenarioSession session = StartJson(RevisitJson);
            session.Choose("open");

            session.Choose("ask");
            session.Choose("act");

            CollectionAssert.AreEqual(new[] { "ask", "back" }, OptionIds(session));
            Assert.That(session.Current.Options[0].IsSeen, Is.True);
            Assert.That(session.Current.Options[1].IsSeen, Is.False);
        }

        [Test]
        public void Revisit_ChoosingASeenOption_RepeatsTheAnswerWithoutEffectsOrActions()
        {
            ScenarioSession session = StartJson(RevisitJson);
            session.Choose("open");
            session.Choose("ask");
            int decisions = session.Decisions.Count;
            ChoiceOutcome outcome = null;
            session.ChoiceResolved += result => outcome = result;

            Assert.That(session.Choose("ask"), Is.True);

            Assert.That(outcome.ResponseText, Is.EqualTo("Answer"));
            Assert.That(outcome.Effects, Is.Empty);
            Assert.That(outcome.IsFree, Is.True);
            Assert.That(session.State.Get(ScenarioKeys.Loyalty), Is.EqualTo(60));
            Assert.That(session.State.Get(ScenarioKeys.HubActions), Is.EqualTo(1));
            Assert.That(session.Decisions.Count, Is.EqualTo(decisions));
            Assert.That(session.Current.Node.Id, Is.EqualTo("menu"));
        }

        [Test]
        public void Done_OptionIsMarkedDoneOnceItsConditionsHold()
        {
            ScenarioSession session = StartJson(RevisitJson);
            Assert.That(session.Current.Options[0].IsDone, Is.False);

            session.Choose("open");
            session.Choose("ask");
            session.Choose("back");

            Assert.That(session.Current.Node.Id, Is.EqualTo("hub"));
            Assert.That(session.Current.Options[0].IsDone, Is.True);
        }

        [TestCase(79, ScaleGrade.Good)]
        [TestCase(80, ScaleGrade.Excellent)]
        [TestCase(40, ScaleGrade.Satisfactory)]
        [TestCase(39, ScaleGrade.Fail)]
        public void ScaleGrades_Thresholds(int value, ScaleGrade expected)
        {
            Assert.That(ScaleGrades.From(value), Is.EqualTo(expected));
        }

        private static ScenarioSession StartFixture(string variantId)
        {
            ScenarioSession session = new ScenarioSession(ScenarioLoader.Parse(TestScenarios.ReadFixtureJson()),
                new System.Random(0), variantId);
            session.Start();
            return session;
        }

        private static ScenarioSession StartJson(string json)
        {
            ScenarioSession session = new ScenarioSession(ScenarioLoader.Parse(json), new System.Random(0));
            session.Start();
            return session;
        }

        private static List<string> OptionIds(ScenarioSession session)
        {
            List<string> ids = new List<string>();
            foreach (OptionView option in session.Current.Options)
            {
                ids.Add(option.Id);
            }

            return ids;
        }

        private static int DeltaOf(IReadOnlyList<AppliedEffect> effects, string key)
        {
            foreach (AppliedEffect effect in effects)
            {
                if (effect.Key == key)
                {
                    return effect.Delta;
                }
            }

            return 0;
        }
    }
}
