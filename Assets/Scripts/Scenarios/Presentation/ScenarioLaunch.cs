namespace Game.Scenarios.Presentation
{
    /// <summary>What the menu asked for before loading a scenario scene; read by the scene's starter.</summary>
    public static class ScenarioLaunch
    {
        /// <summary>
        /// Story variant the menu asked for: a variant id, empty for the weighted random pick,
        /// null when no menu request was made (the scene was opened directly) and the scene's own default applies.
        /// </summary>
        public static string ForcedVariant { get; set; }
    }
}
