using System;
using System.Collections.Generic;

namespace Witch.Gameplay
{
    /// <summary>
    /// Everything Medea's kitchen remembers between sessions, written as JSON by <see cref="SaveFile"/>.
    /// Assets are stored by asset name so a save keeps working when assets are moved between folders.
    /// No account, device or personal data is stored.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Bump this when the layout changes, and teach <see cref="SaveFile"/> to migrate the old one.</summary>
        public const int CurrentVersion = 1;

        [Serializable]
        public class StockEntry
        {
            public string item;
            public int count;
        }

        public int version = CurrentVersion;

        /// <summary>Recipe asset names, in the order they were first brewed.</summary>
        public List<string> discoveredRecipes = new List<string>();

        /// <summary>Counts of the kitchen items that run out (lemons, salt scoops).</summary>
        public List<StockEntry> stock = new List<StockEntry>();

        /// <summary>Lore page asset names the player has unlocked.</summary>
        public List<string> unlockedLore = new List<string>();

        /// <summary>Recipe the player was working on, so the book opens where they left it.</summary>
        public string activeRecipe;
    }
}
