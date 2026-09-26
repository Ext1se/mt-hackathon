namespace Game.Scenarios.Core.Data
{
    /// <summary>Operators accepted in scenario effects.</summary>
    public static class EffectOps
    {
        public const string Add = "add";
        public const string Set = "set";

        public static bool IsKnown(string op)
        {
            return op == Add || op == Set;
        }
    }
}
