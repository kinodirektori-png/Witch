namespace Witch.Gameplay
{
    /// <summary>
    /// Optional check on a kitchen item before it is poured into a vessel, e.g. a salt jar that has run out.
    /// <see cref="CauldronManager"/> asks every gate on the dropped item and tells it when the pour happened.
    /// </summary>
    public interface IPourGate
    {
        /// <summary>Return false (with a kid hint) to refuse the pour.</summary>
        bool CanPour(out string hint);

        /// <summary>Called once the vessel accepted the pour.</summary>
        void OnPoured();
    }
}
