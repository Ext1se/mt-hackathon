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
        public const int ScaleMin = 0;
        public const int ScaleMax = 100;
    }
}
