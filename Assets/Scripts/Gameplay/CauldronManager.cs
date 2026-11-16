using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Lives on the cauldron. Holds the list of recipes the player brews in order and checks each dropped
    /// ingredient against the active one. Put it next to an <see cref="ItemDropZone"/>: correct ingredients are
    /// accepted (and consumed), wrong ones are rejected and snap back to the shelf.
    /// </summary>
    public class CauldronManager : MonoBehaviour, IItemDropReceiver
    {
        [Tooltip("Recipes the cauldron accepts, brewed in this order.")]
        [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();

        [Tooltip("Index into Recipes of the recipe the cauldron currently wants.")]
        [SerializeField, Min(0)] private int activeRecipeIndex;

        [Tooltip("Move on to the next recipe after a successful reaction.")]
        [SerializeField] private bool advanceOnSuccess = true;

        [Tooltip("Start again from the first recipe after the last one is brewed.")]
        [SerializeField] private bool loopRecipes = true;

        [Tooltip("Hide the ingredient once it has been used in a successful reaction.")]
        [SerializeField] private bool consumeIngredientOnSuccess = true;

        [Header("Events")]
        [Tooltip("Raised after a successful reaction with the recipe that was brewed.")]
        public UnityEvent<RecipeData> onSuccessfulReaction = new UnityEvent<RecipeData>();

        [Tooltip("Raised when a wrong or unknown ingredient is dropped in.")]
        public UnityEvent<GameObject> onFailedReaction = new UnityEvent<GameObject>();

        /// <summary>C# event for code listeners (effects, HUD, recipe book). Same timing as onSuccessfulReaction.</summary>
        public event Action<RecipeData> SuccessfulReaction;

        /// <summary>Raised when the active recipe changes.</summary>
        public event Action<RecipeData> ActiveRecipeChanged;

        /// <summary>The recipe the cauldron currently wants, or null if there is none.</summary>
        public RecipeData ActiveRecipe =>
            activeRecipeIndex >= 0 && activeRecipeIndex < recipes.Count ? recipes[activeRecipeIndex] : null;

        /// <summary>The most recently brewed recipe, or null before the first success.</summary>
        public RecipeData LastBrewedRecipe { get; private set; }

        public IReadOnlyList<RecipeData> Recipes => recipes;

        /// <summary>
        /// Checks whether <paramref name="droppedItem"/> matches the active recipe. On a match it calls
        /// <see cref="OnSuccessfulReaction"/> and returns true; otherwise it returns false.
        /// </summary>
        public bool CheckReaction(GameObject droppedItem)
        {
            RecipeData activeRecipe = ActiveRecipe;
            RecipeData droppedRecipe = GetRecipe(droppedItem);

            if (activeRecipe == null || droppedRecipe == null || droppedRecipe != activeRecipe)
            {
                OnFailedReaction(droppedItem);
                return false;
            }

            LastBrewedRecipe = activeRecipe;

            if (consumeIngredientOnSuccess)
            {
                droppedItem.SetActive(false);
            }

            OnSuccessfulReaction(activeRecipe);

            if (advanceOnSuccess)
            {
                AdvanceRecipe();
            }

            return true;
        }

        bool IItemDropReceiver.TryReceiveItem(GameObject droppedItem) => CheckReaction(droppedItem);

        /// <summary>Makes the recipe at <paramref name="index"/> the active one.</summary>
        public void SetActiveRecipe(int index)
        {
            if (recipes.Count == 0)
            {
                activeRecipeIndex = 0;
                return;
            }

            activeRecipeIndex = Mathf.Clamp(index, 0, recipes.Count - 1);
            ActiveRecipeChanged?.Invoke(ActiveRecipe);
        }

        /// <summary>Makes <paramref name="recipe"/> the active one if it is in the list.</summary>
        public void SetActiveRecipe(RecipeData recipe)
        {
            int index = recipes.IndexOf(recipe);
            if (index >= 0)
            {
                SetActiveRecipe(index);
            }
        }

        /// <summary>
        /// Placeholder hook for a successful brew. Override in a subclass for custom behaviour, or listen to
        /// <see cref="onSuccessfulReaction"/> / <see cref="SuccessfulReaction"/> (e.g. particle effects).
        /// </summary>
        protected virtual void OnSuccessfulReaction(RecipeData recipe)
        {
            Debug.Log($"Cauldron brewed {recipe.commonName} ({recipe.technicalName}, {recipe.chemicalFormula}).", this);
            SuccessfulReaction?.Invoke(recipe);
            onSuccessfulReaction.Invoke(recipe);
        }

        /// <summary>Placeholder hook for a wrong ingredient. Override for custom feedback.</summary>
        protected virtual void OnFailedReaction(GameObject droppedItem)
        {
            onFailedReaction.Invoke(droppedItem);
        }

        private void AdvanceRecipe()
        {
            if (recipes.Count == 0)
            {
                return;
            }

            int next = activeRecipeIndex + 1;
            if (next >= recipes.Count)
            {
                if (!loopRecipes)
                {
                    return;
                }

                next = 0;
            }

            SetActiveRecipe(next);
        }

        private static RecipeData GetRecipe(GameObject item)
        {
            if (item == null)
            {
                return null;
            }

            var ingredient = item.GetComponent<IngredientItem>();
            return ingredient != null ? ingredient.Recipe : null;
        }

        private void OnValidate()
        {
            if (recipes.Count > 0)
            {
                activeRecipeIndex = Mathf.Clamp(activeRecipeIndex, 0, recipes.Count - 1);
            }
        }
    }
}
