using Game.Scenarios.Core;

namespace Game.Scenarios.Presentation.Results
{
    /// <summary>Where finished playthroughs go: a local file now, the backend API later.</summary>
    public interface IScenarioResultSink
    {
        /// <returns>Where the result was stored, for logs.</returns>
        string Save(ScenarioResult result);
    }
}
