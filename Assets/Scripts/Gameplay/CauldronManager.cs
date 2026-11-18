using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Lives on the cauldron. Keeps a recipe dictionary (ingredient combination -> <see cref="RecipeData"/>), remembers
    /// what has been dropped in so far, and brews a recipe once the contents match it. Put it next to an
    /// <see cref="ItemDropZone"/>: ingredients that fit are accepted, ones that don't snap back to the shelf.
    /// </summary>
    /// <remarks>
    /// Drag-and-drop stays in <see cref="DraggableItem"/> / <see cref="ItemDropZone"/>; this class only decides
    /// what a dropped ingredient means. Visuals and HUD listen to its events.
    /// </remarks>
    public class CauldronManager : MonoBehaviour, IItemDropReceiver
    {
        /// <summary>Which recipes a dropped ingredient is checked against.</summary>
        public enum BrewingMode
        {
            /// <summary>Guided: only ingredients for the active recipe are accepted, recipes are brewed in order.</summary>
            ActiveRecipe,
            /// <summary>Free brewing: anything that is part of a known recipe is accepted; any finished recipe brews.</summary>
            AnyKnownRecipe
        }

        [Tooltip("The recipe dictionary. Each recipe's Reaction reactants (or its Ingredients list) is its combination; order " +
                 "does not matter unless the reaction says so. A recipe without either is brewed by dropping it in on its own. " +
                 "In Active Recipe mode they are brewed in this order.")]
        [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();

        [Tooltip("Active Recipe: only accept ingredients for the current goal. Any Known Recipe: free brewing.")]
        [SerializeField] private BrewingMode brewingMode = BrewingMode.ActiveRecipe;

        [Tooltip("Index into Recipes of the recipe the cauldron currently wants.")]
        [SerializeField, Min(0)] private int activeRecipeIndex;

        [Tooltip("Move on to the next recipe after the active recipe is brewed.")]
        [SerializeField] private bool advanceOnSuccess = true;

        [Tooltip("Start again from the first recipe after the last one is brewed.")]
        [SerializeField] private bool loopRecipes = true;

        [Tooltip("Hide each ingredient once the cauldron accepts it. Turn off to keep ingredients reusable (e.g. for testing).")]
        [FormerlySerializedAs("consumeIngredientOnSuccess")]
        [SerializeField] private bool consumeIngredientsOnDrop = true;

        [Header("Events")]
        [Tooltip("Raised after a successful reaction with the recipe that was brewed.")]
        public UnityEvent<RecipeData> onSuccessfulReaction = new UnityEvent<RecipeData>();

        [Tooltip("Raised when an ingredient is accepted but the recipe is not finished yet.")]
        public UnityEvent<RecipeData> onIngredientAdded = new UnityEvent<RecipeData>();

        [Tooltip("Raised when a wrong or unknown ingredient is dropped in.")]
        public UnityEvent<GameObject> onFailedReaction = new UnityEvent<GameObject>();

        /// <summary>C# event for code listeners (effects, HUD, recipe book). Same timing as onSuccessfulReaction.</summary>
        public event Action<RecipeData> SuccessfulReaction;

        /// <summary>Raised when the active recipe changes.</summary>
        public event Action<RecipeData> ActiveRecipeChanged;

        /// <summary>Raised whenever an ingredient is added or the cauldron is emptied (including after a brew).</summary>
        public event Action<IReadOnlyList<RecipeData>> ContentsChanged;

        private readonly List<RecipeData> contents = new List<RecipeData>();
        private RecipeDatabase database;

        /// <summary>The recipe the cauldron currently wants, or null if there is none.</summary>
        public RecipeData ActiveRecipe =>
            activeRecipeIndex >= 0 && activeRecipeIndex < recipes.Count ? recipes[activeRecipeIndex] : null;

        /// <summary>The most recently brewed recipe, or null before the first success.</summary>
        public RecipeData LastBrewedRecipe { get; private set; }

        public IReadOnlyList<RecipeData> Recipes => recipes;

        /// <summary>Ingredients currently in the cauldron, in the order they were added.</summary>
        public IReadOnlyList<RecipeData> Contents => contents;

        public BrewingMode Mode
        {
            get => brewingMode;
            set
            {
                brewingMode = value;
                EmptyCauldron();
            }
        }

        /// <summary>The recipe dictionary, built from <see cref="Recipes"/> on first use.</summary>
        public RecipeDatabase Database
        {
            get
            {
                if (database == null)
                {
                    RebuildDatabase();
                }

                return database;
            }
        }

        private void Awake()
        {
            RebuildDatabase();
        }

        /// <summary>Rebuilds the recipe dictionary. Call after changing <see cref="Recipes"/> or recipe ingredients at runtime.</summary>
        public void RebuildDatabase()
        {
            database = new RecipeDatabase(recipes);
            database.LogWarnings(this);
        }

        /// <summary>Adds a recipe to the dictionary at runtime (e.g. one unlocked in the recipe book).</summary>
        public void AddRecipe(RecipeData recipe)
        {
            if (recipe == null || recipes.Contains(recipe))
            {
                return;
            }

            recipes.Add(recipe);
            RebuildDatabase();
        }

        /// <summary>
        /// Checks the ingredient on <paramref name="droppedItem"/> (via its <see cref="IngredientItem"/>) against the
        /// recipe dictionary. Returns true if the cauldron accepts it: either it brought the contents one step closer
        /// to a recipe, or it completed one, in which case <see cref="OnSuccessfulReaction"/> is called.
        /// Returns false (and the item snaps back) if it doesn't fit.
        /// </summary>
        public bool CheckReaction(GameObject droppedItem)
        {
            RecipeData ingredient = GetIngredient(droppedItem);
            if (ingredient == null)
            {
                OnFailedReaction(droppedItem);
                return false;
            }

            // What the cauldron would hold if we accepted this ingredient.
            var candidate = new List<RecipeData>(contents) { ingredient };

            if (!TryMatch(candidate, out RecipeData matchedRecipe))
            {
                OnFailedReaction(droppedItem);
                return false;
            }

            contents.Add(ingredient);

            if (consumeIngredientsOnDrop)
            {
                droppedItem.SetActive(false);
            }

            if (matchedRecipe == null)
            {
                // On the right track, but the recipe needs more ingredients.
                OnIngredientAdded(ingredient);
                ContentsChanged?.Invoke(contents);
                return true;
            }

            Brew(matchedRecipe);
            return true;
        }

        bool IItemDropReceiver.TryReceiveItem(GameObject droppedItem) => CheckReaction(droppedItem);

        /// <summary>Ingredients still needed for <paramref name="recipe"/> (defaults to the active recipe).</summary>
        public List<RecipeData> GetMissingIngredients(RecipeData recipe = null)
        {
            return RecipeDatabase.GetMissingIngredients(contents, recipe != null ? recipe : ActiveRecipe);
        }

        /// <summary>Throws away whatever is in the cauldron without brewing.</summary>
        public void EmptyCauldron()
        {
            if (contents.Count == 0)
            {
                return;
            }

            contents.Clear();
            ContentsChanged?.Invoke(contents);
        }

        /// <summary>Makes the recipe at <paramref name="index"/> the active one.</summary>
        public void SetActiveRecipe(int index)
        {
            if (recipes.Count == 0)
            {
                activeRecipeIndex = 0;
                return;
            }

            activeRecipeIndex = Mathf.Clamp(index, 0, recipes.Count - 1);

            // Half-finished ingredients for the old goal don't count towards the new one.
            if (brewingMode == BrewingMode.ActiveRecipe)
            {
                EmptyCauldron();
            }

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
        /// Overrides should call base so listeners are still notified.
        /// </summary>
        protected virtual void OnSuccessfulReaction(RecipeData matchedRecipe)
        {
            Debug.Log($"Cauldron brewed {matchedRecipe.commonName} ({matchedRecipe.technicalName}, {matchedRecipe.chemicalFormula}).", this);
            SuccessfulReaction?.Invoke(matchedRecipe);
            onSuccessfulReaction.Invoke(matchedRecipe);
        }

        /// <summary>Hook for an accepted ingredient that doesn't finish a recipe yet. Override for custom feedback.</summary>
        protected virtual void OnIngredientAdded(RecipeData ingredient)
        {
            onIngredientAdded.Invoke(ingredient);
        }

        /// <summary>Placeholder hook for a wrong ingredient. Override for custom feedback.</summary>
        protected virtual void OnFailedReaction(GameObject droppedItem)
        {
            onFailedReaction.Invoke(droppedItem);
        }

        /// <summary>
        /// Decides whether <paramref name="candidate"/> contents are allowed and whether they finish a recipe.
        /// Returns false if the contents fit no allowed recipe. <paramref name="matchedRecipe"/> is the finished
        /// recipe, or null if more ingredients are needed.
        /// </summary>
        private bool TryMatch(List<RecipeData> candidate, out RecipeData matchedRecipe)
        {
            matchedRecipe = null;
            RecipeDatabase db = Database;

            if (brewingMode == BrewingMode.ActiveRecipe)
            {
                RecipeData active = ActiveRecipe;
                if (active == null || !db.Contains(active) || !RecipeDatabase.CanStillBecome(candidate, active))
                {
                    return false;
                }

                if (RecipeDatabase.IsComplete(candidate, active))
                {
                    matchedRecipe = active;
                }

                return true;
            }

            // Free brewing: an exact dictionary hit brews straight away, even if a bigger recipe also starts this way.
            if (db.TryGetRecipe(candidate, out RecipeData found))
            {
                matchedRecipe = found;
                return true;
            }

            return db.IsPartOfAnyRecipe(candidate);
        }

        private void Brew(RecipeData matchedRecipe)
        {
            LastBrewedRecipe = matchedRecipe;
            contents.Clear();
            ContentsChanged?.Invoke(contents);

            OnSuccessfulReaction(matchedRecipe);

            if (advanceOnSuccess && matchedRecipe == ActiveRecipe)
            {
                AdvanceRecipe();
            }
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

        private static RecipeData GetIngredient(GameObject item)
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

            // Pick up Inspector edits made during Play mode.
            if (Application.isPlaying)
            {
                database = null;
            }
        }
    }
}
