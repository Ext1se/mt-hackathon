using System;
using System.Collections.Generic;

namespace Game.Scenarios.Core
{
    /// <summary>
    /// All numeric state of a playthrough: scales, variables, flags, competencies and variant markers.
    /// Unknown keys read as 0, so flags need no declaration. A declared range clamps every write.
    /// </summary>
    public sealed class ScenarioState
    {
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>();
        private readonly Dictionary<string, (int Min, int Max)> _ranges = new Dictionary<string, (int Min, int Max)>();

        public IReadOnlyDictionary<string, int> Values => _values;

        public int Get(string key)
        {
            return _values.TryGetValue(key, out int value) ? value : 0;
        }

        public void DefineRange(string key, int min, int max)
        {
            _ranges[key] = (min, max);
        }

        public void Set(string key, int value)
        {
            if (_ranges.TryGetValue(key, out (int Min, int Max) range))
            {
                value = Math.Max(range.Min, Math.Min(range.Max, value));
            }

            _values[key] = value;
        }

        public void Add(string key, int delta)
        {
            Set(key, Get(key) + delta);
        }
    }
}
