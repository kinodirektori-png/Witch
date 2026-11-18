using System;
using System.Collections.Generic;
using UnityEngine;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Counts wrong drops into the cauldron and, after a few in a row, makes a hint available. Requesting the hint
    /// highlights the next ingredient the active recipe still needs (via <see cref="IHintHighlightable"/> components
    /// on that ingredient). Any progress (an accepted ingredient, a brew, a new goal) resets the count.
    /// </summary>
    public class HintTracker : MonoBehaviour
    {
        [Tooltip("Cauldron to listen to. Defaults to one on this GameObject or its parents.")]
        [SerializeField] private CauldronManager cauldron;

        [Tooltip("Wrong drops in a row before the hint button appears.")]
        [SerializeField, Min(1)] private int wrongAttemptsBeforeHint = 2;

        [Tooltip("How long the ingredient stays highlighted, in seconds.")]
        [SerializeField, Min(0.1f)] private float highlightDuration = 4f;

        /// <summary>Raised when <see cref="HintAvailable"/> changes.</summary>
        public event Action<bool> HintAvailabilityChanged;

        /// <summary>Raised when a hint is shown, with the ingredient that was highlighted.</summary>
        public event Action<IngredientItem> HintShown;

        private readonly List<IHintHighlightable> highlightBuffer = new List<IHintHighlightable>();
        private IngredientItem highlightedItem;

        /// <summary>Wrong drops since the last progress.</summary>
        public int WrongAttempts { get; private set; }

        public bool HintAvailable => WrongAttempts >= wrongAttemptsBeforeHint && GetHintIngredient() != null;

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
                Debug.LogWarning($"{nameof(HintTracker)} on '{name}' has no {nameof(CauldronManager)} to listen to.", this);
                return;
            }

            cauldron.onFailedReaction.AddListener(HandleFailedReaction);
            cauldron.ContentsChanged += HandleContentsChanged;
            cauldron.ActiveRecipeChanged += HandleActiveRecipeChanged;
            cauldron.SuccessfulReaction += HandleSuccessfulReaction;
        }

        private void OnDisable()
        {
            if (cauldron == null)
            {
                return;
            }

            cauldron.onFailedReaction.RemoveListener(HandleFailedReaction);
            cauldron.ContentsChanged -= HandleContentsChanged;
            cauldron.ActiveRecipeChanged -= HandleActiveRecipeChanged;
            cauldron.SuccessfulReaction -= HandleSuccessfulReaction;
        }

        /// <summary>The next ingredient the active recipe still needs, or null if there is no goal.</summary>
        public RecipeData GetHintIngredient()
        {
            if (cauldron == null || cauldron.ActiveRecipe == null)
            {
                return null;
            }

            List<RecipeData> missing = cauldron.GetMissingIngredients();
            if (missing.Count > 0)
            {
                return missing[0];
            }

            // Contents don't fit the goal at all (free-brew mode): hint the goal's first ingredient.
            List<RecipeData> required = RecipeDatabase.GetRequiredIngredients(cauldron.ActiveRecipe);
            return required.Count > 0 ? required[0] : null;
        }

        /// <summary>
        /// Highlights the ingredient to use next. Hook this to the hint button. Returns false if no matching
        /// ingredient is in the scene (e.g. it hasn't been harvested yet).
        /// </summary>
        public bool RequestHint()
        {
            RecipeData ingredient = GetHintIngredient();
            IngredientItem item = IngredientItem.FindActive(ingredient);
            if (item == null)
            {
                Debug.Log($"Hint: no '{(ingredient != null ? ingredient.commonName : "?")}' ingredient in the scene to highlight.", this);
                return false;
            }

            ClearHighlight();
            highlightedItem = item;
            item.GetComponents(highlightBuffer);
            foreach (IHintHighlightable highlight in highlightBuffer)
            {
                highlight.ShowHint(highlightDuration);
            }

            HintShown?.Invoke(item);
            return true;
        }

        private void HandleFailedReaction(GameObject droppedItem)
        {
            bool wasAvailable = HintAvailable;
            WrongAttempts++;
            NotifyIfChanged(wasAvailable);
        }

        private void HandleContentsChanged(IReadOnlyList<RecipeData> contents) => ResetAttempts();

        private void HandleActiveRecipeChanged(RecipeData recipe) => ResetAttempts();

        private void HandleSuccessfulReaction(RecipeData recipe) => ResetAttempts();

        private void ResetAttempts()
        {
            ClearHighlight();
            if (WrongAttempts == 0)
            {
                return;
            }

            bool wasAvailable = HintAvailable;
            WrongAttempts = 0;
            NotifyIfChanged(wasAvailable);
        }

        private void NotifyIfChanged(bool wasAvailable)
        {
            bool isAvailable = HintAvailable;
            if (isAvailable != wasAvailable)
            {
                HintAvailabilityChanged?.Invoke(isAvailable);
            }
        }

        private void ClearHighlight()
        {
            if (highlightedItem == null)
            {
                return;
            }

            highlightedItem.GetComponents(highlightBuffer);
            foreach (IHintHighlightable highlight in highlightBuffer)
            {
                highlight.HideHint();
            }

            highlightedItem = null;
        }
    }
}
