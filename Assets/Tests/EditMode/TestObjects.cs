using System.Collections.Generic;
using UnityEngine;
using Witch.Data;

namespace Witch.Tests
{
    /// <summary>
    /// Creates in-memory recipes, reactions, kitchen items and GameObjects for a test, and destroys them all in
    /// <see cref="DestroyAll"/>. Nothing is saved as an asset.
    /// </summary>
    public sealed class TestObjects
    {
        private readonly List<Object> created = new List<Object>();

        public RecipeData Recipe(string name, string formula = "X", params RecipeData[] ingredients)
        {
            var recipe = Track(ScriptableObject.CreateInstance<RecipeData>());
            recipe.name = name;
            recipe.commonName = name;
            recipe.technicalName = name;
            recipe.chemicalFormula = formula;
            recipe.biologicalSource = "Test kitchen";
            recipe.ingredients = new List<RecipeData>(ingredients);
            return recipe;
        }

        /// <summary>A reaction whose reactants are (coefficient, substance) pairs, with one named product.</summary>
        public ReactionData Reaction(string name, bool inOrder, params (int coefficient, RecipeData substance)[] reactants)
        {
            var reaction = Track(ScriptableObject.CreateInstance<ReactionData>());
            reaction.name = name;
            reaction.reactantsInOrder = inOrder;
            foreach ((int coefficient, RecipeData substance) in reactants)
            {
                reaction.reactants.Add(new ReactionData.Term(coefficient, substance));
            }

            return reaction;
        }

        public KitchenItemData KitchenItem(string name, RecipeData substance, PhysicalState startState, bool isWater = false)
        {
            var item = Track(ScriptableObject.CreateInstance<KitchenItemData>());
            item.name = name;
            item.displayName = name;
            item.substance = substance;
            item.startState = startState;
            item.isWater = isWater;
            return item;
        }

        public GameObject NewGameObject(string name)
        {
            return Track(new GameObject(name));
        }

        public T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        public void DestroyAll()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }
    }
}
