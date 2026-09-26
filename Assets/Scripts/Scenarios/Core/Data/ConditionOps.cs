namespace Game.Scenarios.Core.Data
{
    /// <summary>Comparison operators accepted in scenario conditions.</summary>
    public static class ConditionOps
    {
        public const string Eq = "eq";
        public const string Ne = "ne";
        public const string Gt = "gt";
        public const string Ge = "ge";
        public const string Lt = "lt";
        public const string Le = "le";

        public static bool IsKnown(string op)
        {
            return op == Eq || op == Ne || op == Gt || op == Ge || op == Lt || op == Le;
        }
    }
}
