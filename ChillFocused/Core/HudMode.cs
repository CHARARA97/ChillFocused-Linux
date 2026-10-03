namespace ChillFocused.Core
{
    /// <summary>
    /// How much the in-game overlay shows.
    /// </summary>
    /// <remarks>
    /// Unity-free so it can live in the settings snapshot and be unit tested.
    /// The default is <see cref="Minimal"/>: the only question the player usually
    /// has is "is it intercepting right now", and answering it in one line keeps
    /// ten rows of technical detail off their screen.
    /// </remarks>
    public enum HudMode
    {
        /// <summary>One line: state and, optionally, how many were closed.</summary>
        Minimal = 0,

        /// <summary>The grouped multi-line view, for when something looks wrong.</summary>
        Detailed = 1,

        /// <summary>Nothing is drawn.</summary>
        Off = 2,
    }
}
