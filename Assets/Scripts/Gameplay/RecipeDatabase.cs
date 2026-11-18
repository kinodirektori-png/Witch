using System.Collections.Generic;
using UnityEngine;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// The cauldron's recipe dictionary: maps each ingredient combination to the <see cref="RecipeData"/> it brews.
    /// Plain C# (not a component) so it can be unit-tested and reused by the recipe book later.
    /// </summary>
    /// <remarks>
    /// A recipe's combination is its <see cref="RecipeData.reaction"/>'s reactants (repeated by coefficient) if it has
    /// one, otherwise its <see cref="RecipeData.ingredients"/> list. A recipe without either is a basic ingredient
    /// whose combination is just itself, so dropping "Table Salt" in on its own brews Table Salt.
    /// Most recipes are order-independent; a reaction with <see cref="ReactionData.reactantsInOrder"/> must be
    /// filled in exactly the listed order. The dictionary key is always the unordered bag, so two recipes can't
    /// differ only by order.
    /// </remarks>
    public class RecipeDatabase
    {
        private readonly Dictionary<IngredientCombination, RecipeData> recipesByCombination =
            new Dictionary<IngredientCombination, RecipeData>();

        private readonly List<RecipeData> recipes = new List<RecipeData>();
        private readonly List<string> warnings = new List<string>();

        /// <summary>Builds the dictionary. Nulls are skipped; for duplicate combinations the first recipe wins.</summary>
        public RecipeDatabase(IEnumerable<RecipeData> sourceRecipes)
        {
            if (sourceRecipes == null)
            {
                return;
            }

            foreach (RecipeData recipe in sourceRecipes)
            {
                if (recipe == null || recipes.Contains(recipe))
                {
                    continue;
                }

                var combination = new IngredientCombination(GetRequiredIngredients(recipe));
                if (recipesByCombination.TryGetValue(combination, out RecipeData existing))
                {
                    warnings.Add($"'{recipe.name}' uses the same ingredients as '{existing.name}', so it can never be " +
                                 $"brewed. Give it a different ingredient list.");
                    continue;
                }

                recipesByCombination.Add(combination, recipe);
                recipes.Add(recipe);
            }
        }

        /// <summary>Every recipe in the dictionary, in the order they were added.</summary>
        public IReadOnlyList<RecipeData> Recipes => recipes;

        public int Count => recipes.Count;

        /// <summary>Setup problems found while building (e.g. duplicate combinations). Empty when all is well.</summary>
        public IReadOnlyList<string> Warnings => warnings;

        public bool Contains(RecipeData recipe) => recipe != null && recipes.Contains(recipe);

        /// <summary>
        /// Looks up the recipe brewed by exactly these ingredients. Order doesn't matter unless the recipe's
        /// reaction requires it, in which case <paramref name="ingredients"/> must be in the order they went in.
        /// </summary>
        public bool TryGetRecipe(IEnumerable<RecipeData> ingredients, out RecipeData recipe)
        {
            var ingredientList = ingredients != null ? new List<RecipeData>(ingredients) : new List<RecipeData>();
            var combination = new IngredientCombination(ingredientList);
            if (combination.IsEmpty || !recipesByCombination.TryGetValue(combination, out recipe))
            {
                recipe = null;
                return false;
            }

            if (recipe.RequiresOrder && !IsComplete(ingredientList, recipe))
            {
                recipe = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// True if adding more ingredients to <paramref name="contents"/> could still brew some recipe in the
        /// dictionary, i.e. the contents are part of at least one recipe.
        /// </summary>
        public bool IsPartOfAnyRecipe(IReadOnlyList<RecipeData> contents)
        {
            foreach (RecipeData recipe in recipes)
            {
                if (CanStillBecome(contents, recipe))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The ingredients needed to brew <paramref name="recipe"/>: its reaction's reactants (each repeated by its
        /// coefficient, in listed order), else its ingredient list, else the recipe itself for a basic ingredient.
        /// Never null.
        /// </summary>
        public static List<RecipeData> GetRequiredIngredients(RecipeData recipe)
        {
            var required = new List<RecipeData>();
            if (recipe == null)
            {
                return required;
            }

            if (recipe.reaction != null)
            {
                required.AddRange(recipe.reaction.GetIngredientSequence());
            }

            if (required.Count == 0 && recipe.ingredients != null)
            {
                foreach (RecipeData ingredient in recipe.ingredients)
                {
                    if (ingredient != null)
                    {
                        required.Add(ingredient);
                    }
                }
            }

            if (required.Count == 0)
            {
                required.Add(recipe);
            }

            return required;
        }

        /// <summary>
        /// True if every item in <paramref name="contents"/> is still needed by <paramref name="recipe"/>
        /// (counting repeats, and in order for ordered reactions), so the player is on the right track.
        /// Empty contents can become any recipe.
        /// </summary>
        public static bool CanStillBecome(IReadOnlyList<RecipeData> contents, RecipeData recipe)
        {
            return recipe != null && TryGetMissing(contents, recipe, out _);
        }

        /// <summary>True if <paramref name="contents"/> is exactly the recipe's ingredients (in order if the recipe requires it).</summary>
        public static bool IsComplete(IReadOnlyList<RecipeData> contents, RecipeData recipe)
        {
            return recipe != null && TryGetMissing(contents, recipe, out List<RecipeData> missing) && missing.Count == 0;
        }

        /// <summary>
        /// Ingredients still needed to finish <paramref name="recipe"/> given what is already in the cauldron.
        /// Returns an empty list if the contents don't fit the recipe at all.
        /// </summary>
        public static List<RecipeData> GetMissingIngredients(IReadOnlyList<RecipeData> contents, RecipeData recipe)
        {
            return recipe != null && TryGetMissing(contents, recipe, out List<RecipeData> missing)
                ? missing
                : new List<RecipeData>();
        }

        /// <summary>
        /// Removes each item in <paramref name="contents"/> from the recipe's required list (one at a time, so
        /// repeats count). Fails if the contents hold something the recipe doesn't need or too many of something.
        /// For ordered recipes the contents must match the start of the required sequence.
        /// </summary>
        private static bool TryGetMissing(IReadOnlyList<RecipeData> contents, RecipeData recipe, out List<RecipeData> missing)
        {
            missing = GetRequiredIngredients(recipe);
            if (contents == null)
            {
                return true;
            }

            if (recipe.RequiresOrder)
            {
                foreach (RecipeData item in contents)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (missing.Count == 0 || missing[0] != item)
                    {
                        return false;
                    }

                    missing.RemoveAt(0);
                }

                return true;
            }

            foreach (RecipeData item in contents)
            {
                if (item == null)
                {
                    continue;
                }

                if (!missing.Remove(item))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Logs any <see cref="Warnings"/> to the Unity console.</summary>
        public void LogWarnings(Object context)
        {
            foreach (string warning in warnings)
            {
                Debug.LogWarning($"Recipe dictionary: {warning}", context);
            }
        }
    }
}
