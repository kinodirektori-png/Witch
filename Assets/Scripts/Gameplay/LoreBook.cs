using System;
using System.Collections.Generic;
using UnityEngine;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Stage 2 data model for the recipe and lore book: which <see cref="LoreEntryData"/> pages are unlocked. Listens to
    /// <see cref="RecipeDiscovery"/> and unlocks entries whose recipes have been brewed. The book UI (#14) and
    /// harvesting (#18) read from it; the save system (#19) restores it with <see cref="Unlock"/>.
    /// </summary>
    public class LoreBook : MonoBehaviour
    {
        [Tooltip("Every lore page in the game, in book order.")]
        [SerializeField] private List<LoreEntryData> entries = new List<LoreEntryData>();

        [Tooltip("Tells the book which recipes have been brewed. Defaults to one on this GameObject or its parents.")]
        [SerializeField] private RecipeDiscovery discovery;

        /// <summary>Raised when a page unlocks (by brewing or <see cref="Unlock"/>).</summary>
        public event Action<LoreEntryData> EntryUnlocked;

        private readonly HashSet<LoreEntryData> unlocked = new HashSet<LoreEntryData>();

        public IReadOnlyList<LoreEntryData> Entries => entries;

        public bool IsUnlocked(LoreEntryData entry) => entry != null && unlocked.Contains(entry);

        /// <summary>Unlocked entries that can be gathered (have a yield), for harvesting (#18).</summary>
        public List<LoreEntryData> GetGatherableEntries()
        {
            var result = new List<LoreEntryData>();
            foreach (LoreEntryData entry in entries)
            {
                if (IsUnlocked(entry) && entry.gatherYield != null)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>Entries of one category, in book order (for book tabs).</summary>
        public List<LoreEntryData> GetEntries(LoreCategory category)
        {
            return entries.FindAll(entry => entry != null && entry.category == category);
        }

        /// <summary>Unlocks a page (e.g. restored from a save). Does nothing if already unlocked.</summary>
        public void Unlock(LoreEntryData entry)
        {
            if (entry != null && unlocked.Add(entry))
            {
                EntryUnlocked?.Invoke(entry);
            }
        }

        /// <summary>Every unlocked page, in book order (for the save system).</summary>
        public List<LoreEntryData> GetUnlockedEntries()
        {
            return entries.FindAll(entry => entry != null && unlocked.Contains(entry));
        }

        /// <summary>
        /// Restores unlocked pages from a save without raising <see cref="EntryUnlocked"/>, so they don't show as new.
        /// </summary>
        public void RestoreUnlocked(IEnumerable<LoreEntryData> restored)
        {
            if (restored == null)
            {
                return;
            }

            foreach (LoreEntryData entry in restored)
            {
                if (entry != null)
                {
                    unlocked.Add(entry);
                }
            }
        }

        /// <summary>Locks everything again except pages unlocked at the start (e.g. "new game").</summary>
        public void ResetProgress()
        {
            unlocked.Clear();
            foreach (LoreEntryData entry in entries)
            {
                if (entry != null && entry.unlockedAtStart)
                {
                    unlocked.Add(entry);
                }
            }
        }

        private void Awake()
        {
            if (discovery == null)
            {
                discovery = GetComponentInParent<RecipeDiscovery>();
            }

            foreach (LoreEntryData entry in entries)
            {
                if (entry != null && entry.unlockedAtStart)
                {
                    unlocked.Add(entry);
                }
            }
        }

        private void OnEnable()
        {
            if (discovery != null)
            {
                discovery.RecipeDiscovered += HandleRecipeDiscovered;
                RefreshUnlocks();
            }
        }

        private void OnDisable()
        {
            if (discovery != null)
            {
                discovery.RecipeDiscovered -= HandleRecipeDiscovered;
            }
        }

        private void HandleRecipeDiscovered(RecipeData recipe) => RefreshUnlocks();

        private void RefreshUnlocks()
        {
            Func<RecipeData, bool> isBrewed = recipe => discovery != null && discovery.IsDiscovered(recipe);
            foreach (LoreEntryData entry in entries)
            {
                if (entry != null && !unlocked.Contains(entry) && entry.IsUnlockedBy(isBrewed))
                {
                    Unlock(entry);
                }
            }
        }
    }
}
