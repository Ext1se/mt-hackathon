using System;
using System.Collections.Generic;

namespace Game.Scenarios.Core
{
    /// <summary>A scenario file that cannot be read or does not pass validation.</summary>
    public sealed class ScenarioFormatException : Exception
    {
        public ScenarioFormatException(string scenarioId, IReadOnlyList<string> errors)
            : base(BuildMessage(scenarioId, errors))
        {
            Errors = errors;
        }

        public ScenarioFormatException(string message)
            : base(message)
        {
            Errors = new[] { message };
        }

        public ScenarioFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
            Errors = new[] { message };
        }

        public IReadOnlyList<string> Errors { get; }

        private static string BuildMessage(string scenarioId, IReadOnlyList<string> errors)
        {
            return $"Scenario '{scenarioId}' is invalid:\n- " + string.Join("\n- ", errors);
        }
    }
}
