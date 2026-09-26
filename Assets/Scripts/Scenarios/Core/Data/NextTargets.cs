namespace Game.Scenarios.Core.Data
{
    /// <summary>Special jump targets that are not node ids.</summary>
    public static class NextTargets
    {
        /// <summary>Finish the scenario and pick an ending.</summary>
        public const string End = "@end";

        /// <summary>Go back to the active hub.</summary>
        public const string Hub = "@hub";

        /// <summary>Leave a trigger interrupt and continue where the scenario was heading before it.</summary>
        public const string Return = "@return";

        public static bool IsSpecial(string target)
        {
            return target == End || target == Hub || target == Return;
        }
    }
}
