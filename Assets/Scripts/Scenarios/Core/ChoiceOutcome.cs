using System.Collections.Generic;

namespace Game.Scenarios.Core
{
    /// <summary>What a decision changed and how the NPC answers it.</summary>
    public sealed class ChoiceOutcome
    {
        public ChoiceOutcome(string nodeId, string optionId, string optionText, string speaker, string responseText,
            bool timedOut, bool isFree, IReadOnlyList<AppliedEffect> effects)
        {
            IsFree = isFree;
            NodeId = nodeId;
            OptionId = optionId;
            OptionText = optionText;
            Speaker = speaker;
            ResponseText = responseText;
            TimedOut = timedOut;
            Effects = effects;
        }

        public string NodeId { get; }
        public string OptionId { get; }
        public string OptionText { get; }
        public string Speaker { get; }
        public string ResponseText { get; }
        public bool TimedOut { get; }

        /// <summary>Navigation inside a hub: nothing worth pausing on.</summary>
        public bool IsFree { get; }
        public IReadOnlyList<AppliedEffect> Effects { get; }
    }
}
