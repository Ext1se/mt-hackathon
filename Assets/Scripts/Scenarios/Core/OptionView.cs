namespace Game.Scenarios.Core
{
    /// <summary>An option the player can pick right now.</summary>
    public sealed class OptionView
    {
        public OptionView(string id, string text, string target, string objective, bool isQuiet = false)
        {
            Id = id;
            Text = text;
            Target = target;
            Objective = string.IsNullOrEmpty(objective) ? text : objective;
            IsQuiet = isQuiet;
        }

        public string Id { get; }
        public string Text { get; }
        public string Target { get; }
        public string Objective { get; }
        public bool IsWorld => !string.IsNullOrEmpty(Target);

        /// <summary>Usable at its target, but not listed as an objective and not marked in the world.</summary>
        public bool IsQuiet { get; }
    }
}
