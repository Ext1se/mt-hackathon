using System.Collections.Generic;
using System.IO;
using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Scenarios.Tests
{
    /// <summary>Guards hand-written scenarios: every file validates, and random playthroughs of every variant finish.</summary>
    public sealed class ScenarioContentTests
    {
        private const string ScenariosFolder = "Data/Scenarios";
        private const int SeedsPerVariant = 30;
        private const int MaxSteps = 500;

        [Test]
        public void ScenarioFolder_HasScenarios()
        {
            Assert.That(ScenarioFiles(), Is.Not.Empty);
        }

        [Test]
        public void EveryScenario_IsValid()
        {
            foreach (string path in ScenarioFiles())
            {
                try
                {
                    ScenarioLoader.Parse(File.ReadAllText(path), TestScenarios.ReadScenarioPart);
                }
                catch (ScenarioFormatException exception)
                {
                    Assert.Fail($"{Path.GetFileName(path)}: {exception.Message}");
                }
            }
        }

        [Test]
        public void EveryVariant_RandomPlaythroughsReachAnEnding()
        {
            foreach (string path in ScenarioFiles())
            {
                ScenarioData data = ScenarioLoader.Parse(File.ReadAllText(path), TestScenarios.ReadScenarioPart);
                List<string> variants = new List<string>();
                foreach (VariantData variant in data.Variants)
                {
                    variants.Add(variant.Id);
                }

                if (variants.Count == 0)
                {
                    variants.Add(null);
                }

                foreach (string variantId in variants)
                {
                    for (int seed = 0; seed < SeedsPerVariant; seed++)
                    {
                        PlayRandomly(data, variantId, seed, Path.GetFileName(path));
                    }
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void UnattendedItem_BreathingOffer_FollowsTheSetting(int isEnabled)
        {
            ScenarioSession session = StartUnattendedItemAtPanic(isEnabled);

            Assert.That(HasOption(session, "breathe"), Is.EqualTo(isEnabled == 1));
        }

        [TestCase("good")]
        [TestCase("poor")]
        public void UnattendedItem_Breathing_GoesThroughTheCardAndBackToTheHub(string result)
        {
            ScenarioSession session = StartUnattendedItemAtPanic(1);

            Assert.That(session.Choose("breathe"), Is.True);
            Assert.That(session.Current.Node.Id, Is.EqualTo("V5_breath_intro"));
            Assert.That(session.Current.Node.Card, Is.Not.Null);
            Assert.That(session.Choose("understood"), Is.True);
            Assert.That(session.Current.Node.Breathing, Is.Not.Null);
            Assert.That(session.Choose(result), Is.True);
            Assert.That(session.Current.Node.Id, Is.EqualTo("V2"));
        }

        [Test]
        public void UnattendedItem_Panic_WaitsForThePlayerNearFiveB()
        {
            ScenarioSession session = StartUnattendedItemAtHub(0);

            session.Signal("flag.near_5b");
            session.ClearSignal("flag.near_5b");
            session.Signal("flag.said_bomb");
            Assert.That(session.Current.Node.Id, Is.EqualTo("V2"), "5B is far away: she must not interrupt.");

            session.Signal("flag.near_5b");
            Assert.That(session.Current.Node.Id, Is.EqualTo("V5"));
        }

        // Plays the reference (else the first) answers up to the roam hub, then raises the panic of 5B next to her.
        private static ScenarioSession StartUnattendedItemAtPanic(int breathingGame)
        {
            ScenarioSession session = StartUnattendedItemAtHub(breathingGame);
            session.Signal("flag.said_bomb");
            session.Signal("flag.near_5b");
            Assert.That(session.Current.Node.Id, Is.EqualTo("V5"));
            return session;
        }

        private static ScenarioSession StartUnattendedItemAtHub(int breathingGame)
        {
            string path = Path.Combine(Application.dataPath, ScenariosFolder, "UnattendedItem.json");
            ScenarioSession session = new ScenarioSession(ScenarioLoader.Parse(File.ReadAllText(path), TestScenarios.ReadScenarioPart), new System.Random(0), "A");
            session.State.Set(ScenarioKeys.BreathingGame, breathingGame);
            session.Start();
            for (int step = 0; step < MaxSteps && !session.Current.IsRoam; step++)
            {
                string pick = session.Current.Options[0].Id;
                foreach (OptionData option in session.Current.Node.Options)
                {
                    if (option.Reference)
                    {
                        pick = option.Id;
                        break;
                    }
                }

                session.Choose(pick);
            }

            Assert.That(session.Current.Node.Id, Is.EqualTo("V2"));
            return session;
        }

        private static bool HasOption(ScenarioSession session, string optionId)
        {
            foreach (OptionView option in session.Current.Options)
            {
                if (option.Id == optionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void PlayRandomly(ScenarioData data, string variantId, int seed, string fileName)
        {
            System.Random random = new System.Random(seed);
            ScenarioSession session = new ScenarioSession(data, random, variantId);
            // Half of the runs switch the breathing mini-game on, so the branches behind it are played too.
            session.State.Set(ScenarioKeys.BreathingGame, seed % 2);
            // Keys raised by the scene (the player next to 5B) never come from a choice; set them in half of the runs,
            // so the triggers behind them are played too.
            session.State.Set("flag.near_5b", seed % 2);
            session.Start();
            for (int step = 0; step < MaxSteps && session.IsRunning; step++)
            {
                IReadOnlyList<OptionView> options = session.Current.Options;
                // Sometimes let the clock run out, so timeout branches are exercised too.
                if (options.Count == 0 || random.Next(6) == 0)
                {
                    session.Tick(1000f);
                    continue;
                }

                session.Choose(options[random.Next(options.Count)].Id);
            }

            Assert.That(session.IsRunning, Is.False,
                $"{fileName}, variant {variantId}, seed {seed}: stuck at node '{session.Current.Node.Id}'.");
        }

        private static string[] ScenarioFiles()
        {
            string directory = Path.Combine(Application.dataPath, ScenariosFolder);
            return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : new string[0];
        }
    }
}
