namespace Game.Scenarios.Core
{
    /// <summary>State keys the engine reads or writes itself.</summary>
    public static class ScenarioKeys
    {
        public const string Loyalty = "loyalty";
        public const string Safety = "safety";
        public const string HubActions = "hub.actions";
        public const string Speed = "comp.speed";
        public const string CompetencyPrefix = "comp.";
        public const string VariantPrefix = "variant.";

        /// <summary>"visited.NODE" is set to 1 when a node is entered for the first time; never recorded as an effect.</summary>
        public const string VisitedPrefix = "visited.";

        /// <summary>
        /// 1 when the box-breathing mini-game is switched on for the playthrough (set by the presentation before start);
        /// options that open it use it as a condition.
        /// </summary>
        public const string BreathingGame = "setting.breathing_game";
        public const int ScaleMin = 0;
        public const int ScaleMax = 100;
    }
}
