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
                    ScenarioLoader.Parse(File.ReadAllText(path));
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
                ScenarioData data = ScenarioLoader.Parse(File.ReadAllText(path));
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

        private static void PlayRandomly(ScenarioData data, string variantId, int seed, string fileName)
        {
            System.Random random = new System.Random(seed);
            ScenarioSession session = new ScenarioSession(data, random, variantId);
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
