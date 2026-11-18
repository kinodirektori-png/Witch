using System;
using System.Collections.Generic;
using UnityEngine;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Remembers which recipes the player has brewed at least once, for the HUD ("3 / 4 found") and the recipe book.
    /// Listens to a <see cref="CauldronManager"/>; the total is the cauldron's recipe dictionary.
    /// </summary>
    /// <remarks>
    /// In memory only for now. The save system (#19) can restore progress with <see cref="MarkDiscovered"/>.
    /// </remarks>
    public class RecipeDiscovery : MonoBehaviour
    {
        [Tooltip("Cauldron to listen to. Defaults to one on this GameObject or its parents.")]
        [SerializeField] private CauldronManager cauldron;

        /// <summary>Raised when a recipe is brewed for the first time (or restored), with that recipe.</summary>
        public event Action<RecipeData> RecipeDiscovered;

        /// <summary>Raised whenever the discovered count or the total may have changed.</summary>
        public event Action ProgressChanged;

        private readonly HashSet<RecipeData> discovered = new HashSet<RecipeData>();
        private readonly List<RecipeData> discoveredInOrder = new List<RecipeData>();

        public CauldronManager Cauldron => cauldron;

        /// <summary>Recipes brewed so far, in the order they were first brewed.</summary>
        public IReadOnlyList<RecipeData> Discovered => discoveredInOrder;

        public int DiscoveredCount => discoveredInOrder.Count;

        /// <summary>Number of recipes in the cauldron's dictionary.</summary>
        public int TotalCount => cauldron != null ? cauldron.Database.Count : 0;

        public bool IsDiscovered(RecipeData recipe) => recipe != null && discovered.Contains(recipe);

        private void Awake()
        {
            if (cauldron == null)
            {
                cauldron = GetComponentInParent<CauldronManager>();
            }
        }

        private void OnEnable()
        {
            if (cauldron == null)
            {
                Debug.LogWarning($"{nameof(RecipeDiscovery)} on '{name}' has no {nameof(CauldronManager)} to listen to.", this);
                return;
            }

            cauldron.SuccessfulReaction += MarkDiscovered;
        }

        private void OnDisable()
        {
            if (cauldron != null)
            {
                cauldron.SuccessfulReaction -= MarkDiscovered;
            }
        }

        /// <summary>Marks a recipe as discovered. Safe to call more than once; only the first call raises events.</summary>
        public void MarkDiscovered(RecipeData recipe)
        {
            if (recipe == null || !discovered.Add(recipe))
            {
                return;
            }

            discoveredInOrder.Add(recipe);
            RecipeDiscovered?.Invoke(recipe);
            ProgressChanged?.Invoke();
        }

        /// <summary>Forgets all discoveries (e.g. "new game").</summary>
        public void ResetProgress()
        {
            discovered.Clear();
            discoveredInOrder.Clear();
            ProgressChanged?.Invoke();
        }
    }
}
