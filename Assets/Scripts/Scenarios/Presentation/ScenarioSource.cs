using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using UnityEngine;

namespace Game.Scenarios.Presentation
{
    /// <summary>
    /// Parses a scenario TextAsset together with the parts it lists in "include". Parts are loaded from
    /// Resources/<see cref="ScenarioLoader.PartsResourcesFolder"/> by name, so scenes keep a single reference to the
    /// root file and splitting a scenario never needs a scene rebuild.
    /// </summary>
    public static class ScenarioSource
    {
        /// <summary>Throws <see cref="ScenarioFormatException"/> like <see cref="ScenarioLoader.Parse(string)"/>.</summary>
        public static ScenarioData Parse(TextAsset scenario)
        {
            return ScenarioLoader.Parse(scenario.text, ReadPart);
        }

        private static string ReadPart(string name)
        {
            TextAsset part = Resources.Load<TextAsset>(ScenarioLoader.PartsResourcesFolder + "/" + name);
            return part != null ? part.text : null;
        }
    }
}
