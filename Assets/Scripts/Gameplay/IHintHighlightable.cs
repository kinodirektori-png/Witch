namespace Witch.Gameplay
{
    /// <summary>
    /// Implemented by visuals on an ingredient (e.g. a pulsing glow) that show the player "use this one".
    /// <see cref="HintTracker"/> calls it on the right ingredient when a hint is requested, so hint logic
    /// doesn't depend on how the highlight looks.
    /// </summary>
    public interface IHintHighlightable
    {
        /// <summary>Starts the highlight. It should stop on its own after <paramref name="duration"/> seconds.</summary>
        void ShowHint(float duration);

        /// <summary>Stops the highlight early, e.g. when the hint is no longer relevant.</summary>
        void HideHint();
    }
}
