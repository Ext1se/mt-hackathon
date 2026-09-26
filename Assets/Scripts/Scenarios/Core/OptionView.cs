namespace Game.Scenarios.Core
{
    /// <summary>An option the player can pick right now.</summary>
    public sealed class OptionView
    {
        public OptionView(string id, string text, string target, string objective)
        {
            Id = id;
            Text = text;
            Target = target;
            Objective = string.IsNullOrEmpty(objective) ? text : objective;
        }

        public string Id { get; }
        public string Text { get; }
        public string Target { get; }
        public string Objective { get; }
        public bool IsWorld => !string.IsNullOrEmpty(Target);
    }
}
