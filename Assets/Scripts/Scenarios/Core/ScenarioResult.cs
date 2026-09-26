using System.Collections.Generic;

namespace Game.Scenarios.Core
{
    /// <summary>Outcome of a finished playthrough; also the payload sent to profile and analytics storage.</summary>
    public sealed class ScenarioResult
    {
        public ScenarioResult(string scenarioId, string variantId, string endingId, string endingTitle, string endingText,
            int loyalty, int safety, IReadOnlyDictionary<string, int> competencies, IReadOnlyList<DecisionRecord> decisions,
            int hintsUsed, float durationSeconds)
        {
            ScenarioId = scenarioId;
            VariantId = variantId;
            EndingId = endingId;
            EndingTitle = endingTitle;
            EndingText = endingText;
            Loyalty = loyalty;
            Safety = safety;
            Competencies = competencies;
            Decisions = decisions;
            HintsUsed = hintsUsed;
            DurationSeconds = durationSeconds;
        }

        public string ScenarioId { get; }
        public string VariantId { get; }
        public string EndingId { get; }
        public string EndingTitle { get; }
        public string EndingText { get; }
        public int Loyalty { get; }
        public int Safety { get; }
        public IReadOnlyDictionary<string, int> Competencies { get; }
        public IReadOnlyList<DecisionRecord> Decisions { get; }
        public int HintsUsed { get; }
        public float DurationSeconds { get; }
        public ScaleGrade LoyaltyGrade => ScaleGrades.From(Loyalty);
        public ScaleGrade SafetyGrade => ScaleGrades.From(Safety);
    }
}
