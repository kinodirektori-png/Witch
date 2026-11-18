namespace Witch.Data
{
    /// <summary>
    /// The form an everyday item is in. The same substance can be in different forms: citric acid is inside a
    /// whole lemon (Solid) and in lemon juice (Liquid), and a lemon that has been squeezed is spent (Squeezed).
    /// </summary>
    public enum PhysicalState
    {
        /// <summary>A whole object, e.g. a lemon or a salt crystal. Has to be transformed before it can be poured.</summary>
        Solid = 0,

        /// <summary>Grains or powder that can be poured or scooped, e.g. baking soda, salt, flour.</summary>
        Powder = 1,

        /// <summary>Something runny that can be poured, e.g. lemon juice or water.</summary>
        Liquid = 2,

        /// <summary>A fruit whose juice has been squeezed out. Nothing useful is left in it.</summary>
        Squeezed = 3
    }

    /// <summary>Helpers for <see cref="PhysicalState"/>.</summary>
    public static class PhysicalStates
    {
        /// <summary>True for states that can be poured into a pot or bowl (powders and liquids).</summary>
        public static bool CanPour(PhysicalState state) => state == PhysicalState.Powder || state == PhysicalState.Liquid;

        /// <summary>A short kid-friendly word for the state, e.g. "juice" style labels and hints.</summary>
        public static string KidName(PhysicalState state)
        {
            switch (state)
            {
                case PhysicalState.Solid: return "whole";
                case PhysicalState.Powder: return "powder";
                case PhysicalState.Liquid: return "liquid";
                case PhysicalState.Squeezed: return "squeezed";
                default: return state.ToString().ToLowerInvariant();
            }
        }
    }
}
