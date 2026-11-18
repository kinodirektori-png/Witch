using System.Collections.Generic;
using UnityEngine;

namespace Witch.Data
{
    /// <summary>What kind of natural thing a lore entry is about.</summary>
    public enum LoreCategory
    {
        Plant,
        Crystal,
        Herb,
        Mineral
    }

    /// <summary>
    /// A page in the Stage 2 recipe and lore book about a plant, crystal, herb or mineral: what it is, where it is
    /// found and what gathering it gives. Entries unlock as kids brew, and unlocked entries become things to gather.
    /// Create assets via: Assets > Create > Witch > Lore Entry Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLoreEntry", menuName = "Witch/Lore Entry Data", order = 3)]
    public class LoreEntryData : ScriptableObject
    {
        public LoreCategory category = LoreCategory.Plant;

        [Tooltip("Kid-friendly name, e.g. \"Lemon Tree\".")]
        public string commonName = "Lemon Tree";

        [Tooltip("Scientific name, e.g. \"Citrus limon\". Shown in italics.")]
        public string scientificName = "Citrus limon";

        [Tooltip("Two or three short sentences a child can read.")]
        [TextArea(3, 6)] public string description = "";

        [Tooltip("Where it is found in nature, e.g. \"Warm, sunny places like the Mediterranean\".")]
        public string whereFound = "";

        [Tooltip("Optional safety note, e.g. \"Never eat plants you find without asking an adult.\"")]
        [TextArea(1, 3)] public string safetyNote = "";

        [Tooltip("Picture for the book page.")]
        public Sprite illustration;

        [Header("Gathering")]
        [Tooltip("The ingredient gathering this gives, e.g. Citric Acid for the lemon tree. Empty for a future stage.")]
        public RecipeData gatherYield;

        [Tooltip("The kitchen item gathering restocks, e.g. the lemon bowl (#18).")]
        public KitchenItemData restocksKitchenItem;

        [Header("Unlocking")]
        [Tooltip("Readable from the start, without brewing anything.")]
        public bool unlockedAtStart;

        [Tooltip("Unlocks once every recipe in this list has been brewed at least once.")]
        public List<RecipeData> unlockAfterBrewing = new List<RecipeData>();

        [Tooltip("Shown on the locked page, e.g. \"Brew Salt Water to learn where salt comes from.\" Leave empty to build one.")]
        public string unlockHint = "";

        /// <summary>True if <paramref name="isBrewed"/> says every recipe in <see cref="unlockAfterBrewing"/> was brewed.</summary>
        public bool IsUnlockedBy(System.Func<RecipeData, bool> isBrewed)
        {
            if (unlockedAtStart)
            {
                return true;
            }

            if (unlockAfterBrewing == null || !unlockAfterBrewing.Exists(recipe => recipe != null))
            {
                return false; // nothing to unlock it yet: a teaser for a later stage
            }

            foreach (RecipeData recipe in unlockAfterBrewing)
            {
                if (recipe != null && (isBrewed == null || !isBrewed(recipe)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The hint for the locked page.</summary>
        public string GetUnlockHint()
        {
            if (!string.IsNullOrEmpty(unlockHint))
            {
                return unlockHint;
            }

            var names = new List<string>();
            if (unlockAfterBrewing != null)
            {
                foreach (RecipeData recipe in unlockAfterBrewing)
                {
                    if (recipe != null)
                    {
                        names.Add(recipe.commonName);
                    }
                }
            }

            return names.Count > 0 ? $"Brew {string.Join(" and ", names)} to unlock." : "Coming in a later adventure!";
        }

        private void OnValidate()
        {
            commonName = commonName?.Trim();
            scientificName = scientificName?.Trim();
            whereFound = whereFound?.Trim();
        }
    }
}
