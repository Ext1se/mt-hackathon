using System.Collections.Generic;

namespace Game.Scenarios.Core
{
    /// <summary>
    /// One line of the debrief: a player decision, or a consequence applied on entering a node (empty OptionId).
    /// </summary>
    public sealed class DecisionRecord
    {
        public DecisionRecord(string nodeId, string prompt, string optionId, string optionText, string feedback,
            bool isReference, bool timedOut, IReadOnlyList<AppliedEffect> effects, float secondsTaken)
        {
            NodeId = nodeId;
            Prompt = prompt;
            OptionId = optionId;
            OptionText = optionText;
            Feedback = feedback;
            IsReference = isReference;
            TimedOut = timedOut;
            Effects = effects;
            SecondsTaken = secondsTaken;
        }

        public string NodeId { get; }
        public string Prompt { get; }
        public string OptionId { get; }
        public string OptionText { get; }
        public string Feedback { get; }
        public bool IsReference { get; }
        public bool TimedOut { get; }
        public IReadOnlyList<AppliedEffect> Effects { get; }
        public float SecondsTaken { get; }
        public bool IsConsequence => string.IsNullOrEmpty(OptionId);
    }
}
