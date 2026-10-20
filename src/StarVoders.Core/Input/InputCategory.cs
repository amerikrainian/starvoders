namespace StarVoders.Core.Input
{
    /// <summary>
    /// The input layer an action belongs to. Screens declare which categories they use
    /// (<see cref="Screens.Screen.InputCategories"/>, in priority order); the live set each frame is the
    /// union of every active screen's declaration walked focus-first, plus <see cref="Global"/>, which is
    /// always on. Within the live set, an identical chord bound in two categories resolves to the
    /// higher-priority (deeper) one (shadowing), so the same arrows can navigate a menu when focused and
    /// drive something else when not. Conflict prevention therefore only applies WITHIN a category.
    /// </summary>
    public enum InputCategory
    {
        /// <summary>Always live, even when focus mode is off (focus toggle, mod menu, reload).</summary>
        Global,
        /// <summary>Screen/menu navigation: live when the focused screen declares it.</summary>
        UI,
        /// <summary>The fight's gameplay keys (the board, the hand, the fight's glances), declared by the fight screen.</summary>
        Game,
        /// <summary>Keys that read the run itself (Doom), live anywhere in a run: declared by the fight,
        /// the run's screens between fights and the overlays above them while a run is underneath.</summary>
        Run,
    }
}
