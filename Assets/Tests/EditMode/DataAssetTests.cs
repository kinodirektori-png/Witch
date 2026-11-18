using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Witch.Data;
using Witch.Gameplay;

namespace Witch.Tests
{
    /// <summary>
    /// Checks every data asset in the project. Recipes, reactions and kitchen items are created by
    /// Witch > Build MainLab Test Scene; until that has run, these tests are ignored rather than passing silently.
    /// </summary>
    public class DataAssetTests
    {
        private static List<T> LoadAll<T>() where T : Object
        {
            var assets = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            if (assets.Count == 0)
            {
                Assert.Ignore($"No {typeof(T).Name} assets yet. Run Witch > Build MainLab Test Scene first.");
            }

            return assets;
        }

        [Test]
        public void EveryRecipe_HasNameFormulaAndSource()
        {
            var problems = new List<string>();
            foreach (RecipeData recipe in LoadAll<RecipeData>())
            {
                if (string.IsNullOrWhiteSpace(recipe.commonName)) problems.Add($"{recipe.name}: no common name");
                if (string.IsNullOrWhiteSpace(recipe.technicalName)) problems.Add($"{recipe.name}: no technical name");
                if (string.IsNullOrWhiteSpace(recipe.chemicalFormula)) problems.Add($"{recipe.name}: no formula");
                if (string.IsNullOrWhiteSpace(recipe.biologicalSource)) problems.Add($"{recipe.name}: no source");
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EveryRecipeFormula_CanBeRead()
        {
            var problems = new List<string>();
            foreach (RecipeData recipe in LoadAll<RecipeData>())
            {
                if (!string.IsNullOrWhiteSpace(recipe.chemicalFormula) &&
                    !ChemicalFormula.TryCountAtoms(StripNotation(recipe.chemicalFormula), out _, out string error))
                {
                    problems.Add($"{recipe.name} \"{recipe.chemicalFormula}\": {error}");
                }
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>
        /// Removes notation that isn't atoms, so it can be counted: state symbols like "(aq)" in "NaCl(aq)" and the
        /// polymer "n" in "(C6H10O5)n".
        /// </summary>
        private static string StripNotation(string formula)
        {
            string result = formula.Trim();
            foreach (string state in new[] { "(aq)", "(s)", "(l)", "(g)" })
            {
                result = result.Replace(state, string.Empty);
            }

            if (result.EndsWith(")n"))
            {
                result = result.Substring(0, result.Length - 1);
            }

            return result;
        }

        [Test]
        public void EveryStateRule_NamesOneOfItsOwnIngredients()
        {
            var problems = new List<string>();
            foreach (RecipeData recipe in LoadAll<RecipeData>())
            {
                if (recipe.conditions?.ingredientStates == null)
                {
                    continue;
                }

                List<RecipeData> required = RecipeDatabase.GetRequiredIngredients(recipe);
                foreach (IngredientStateRule rule in recipe.conditions.ingredientStates)
                {
                    if (rule?.ingredient != null && !required.Contains(rule.ingredient))
                    {
                        problems.Add($"{recipe.name}: state rule for '{rule.ingredient.name}', which it doesn't use");
                    }
                }
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EveryReaction_IsBalanced_AndHasReactants()
        {
            var problems = new List<string>();
            foreach (ReactionData reaction in LoadAll<ReactionData>())
            {
                if (reaction.GetIngredientSequence().Count == 0)
                {
                    problems.Add($"{reaction.name}: no reactant the player can drop in");
                }

                if (!reaction.IsBalanced(out string problem))
                {
                    problems.Add($"{reaction.name}: {problem}");
                }
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EverySqueezeYield_IsALiquid()
        {
            var problems = new List<string>();
            foreach (KitchenItemData item in LoadAll<KitchenItemData>())
            {
                if (item.squeezeYield != null && item.squeezeYield.startState != PhysicalState.Liquid)
                {
                    problems.Add($"{item.name}: squeezing gives '{item.squeezeYield.name}', which isn't a liquid");
                }

                if (!item.isWater && item.substance == null)
                {
                    problems.Add($"{item.name}: not water and has no substance");
                }
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
