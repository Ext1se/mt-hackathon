namespace Game.Scenarios.Core
{
    /// <summary>An option the player can pick right now.</summary>
    public sealed class OptionView
    {
        public OptionView(string id, string text, string target, string objective, bool isQuiet = false,
            bool isSeen = false, bool isDone = false)
        {
            Id = id;
            Text = text;
            Target = target;
            Objective = string.IsNullOrEmpty(objective) ? text : objective;
            IsQuiet = isQuiet;
            IsSeen = isSeen;
            IsDone = isDone;
        }

        public string Id { get; }
        public string Text { get; }
        public string Target { get; }
        public string Objective { get; }
        public bool IsWorld => !string.IsNullOrEmpty(Target);

        /// <summary>Usable at its target, but not listed as an objective and not marked in the world.</summary>
        public bool IsQuiet { get; }

        /// <summary>Chosen before in this hub and kept for another look: choosing it again only repeats the response.</summary>
        public bool IsSeen { get; }

        /// <summary>A world objective whose goal is reached; the tracker shows it crossed out.</summary>
        public bool IsDone { get; }
    }
}
