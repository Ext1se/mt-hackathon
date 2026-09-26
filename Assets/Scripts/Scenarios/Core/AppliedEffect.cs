namespace Game.Scenarios.Core
{
    /// <summary>The actual change of one state value after clamping.</summary>
    public readonly struct AppliedEffect
    {
        public AppliedEffect(string key, int delta)
        {
            Key = key;
            Delta = delta;
        }

        public string Key { get; }
        public int Delta { get; }
    }
}
